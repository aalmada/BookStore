using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Marten;
using Refit;
using Weasel.Core;
using CreateAuthorRequest = BookStore.Client.CreateAuthorRequest;
using CreateBookRequest = BookStore.Client.CreateBookRequest;

namespace BookStore.AppHost.Tests;

public class BookFilterRegressionTests
{
    [Test]
    public async Task SearchBooks_InNonDefaultTenant_ShouldRespectAuthorFilter()
    {
        // Debugging Multi-Tenant Author Filter
        var tenantId = $"book-filter-test-{Guid.NewGuid():N}";

        // Seed Tenant
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using (var store = DocumentStore.For(opts =>
               {
                   opts.Connection(connectionString!);
                   _ = opts.Policies.AllDocumentsAreMultiTenanted();
                   opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
                   opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
               }))
        {
            await TestHelpers.SeedTenantAsync(store, tenantId);
        }

        // Authenticate as Admin in the new tenant
        var loginRes = await TestHelpers.LoginAsAdminAsync(tenantId);
        var adminClient =
            RestService.For<IAuthorsClient>(TestHelpers.GetAuthenticatedClient(loginRes!.AccessToken, tenantId));
        var adminBooksClient =
            RestService.For<IBooksClient>(TestHelpers.GetAuthenticatedClient(loginRes!.AccessToken, tenantId));

        // Create Author in this tenant
        var authorReq = TestHelpers.GenerateFakeAuthorRequest();
        var author = await TestHelpers.CreateAuthorAsync(adminClient, authorReq);
        var authorId = author.Id;

        // Create Book linked to this Author
        var bookReq = TestHelpers.GenerateFakeBookRequest(authorIds: new[] { authorId });
        var book = await TestHelpers.CreateBookAsync(adminBooksClient, bookReq);

        // Search in correct tenant
        var tenantClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient(tenantId));

        // Poll for consistency (Book projection)
        var foundInTenant = false;
        for (var i = 0; i < TestConstants.DefaultMaxRetries; i++)
        {
            var list = await tenantClient.GetBooksAsync(new BookSearchRequest { AuthorId = authorId });
            if (list != null && list.Items.Any(b => b.Id == book.Id))
            {
                foundInTenant = true;
                break;
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundInTenant).IsTrue();

        // Search in WRONG tenant (Default)
        var defaultTenantClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());
        // No X-Tenant-ID header implies default tenant

        var listDefault = await defaultTenantClient.GetBooksAsync(new BookSearchRequest { AuthorId = authorId });
        // Should be 200 OK but empty result, because Author doesn't exist in default tenant context

        // Should NOT find the book because it belongs to another tenant
        _ = await Assert.That(listDefault!.Items.Any(b => b.Id == book.Id)).IsFalse();
    }

    [Test]
    [Arguments("EUR", 40.0, null, true)] // High price in EUR (50), Min 40 -> Found
    [Arguments("USD", 40.0, null, false)] // Low price in USD (10), Min 40 -> Not Found
    [Arguments("USD", null, 20.0, true)] // Low price in USD (10), Max 20 -> Found
    [Arguments("EUR", null, 20.0, false)] // High price in EUR (50), Max 20 -> Not Found
    [Arguments("EUR", 40.0, 60.0, true)] // EUR 50 is inside 40-60 -> Found
    [Arguments("EUR", 10.0, 40.0, false)] // EUR 50 is outside 10-40 -> Not Found
    [Arguments("USD", 5.0, 15.0, true)] // USD 10 is inside 5-15 -> Found
    [Arguments("USD", 12.0, 20.0, false)] // USD 10 is outside 12-20 -> Not Found
    [Arguments("GBP", null, null, false)] // No price in GBP -> Not Found
    public async Task SearchBooks_WithMultiCurrencyPrices_ShouldRespectCurrencyFilter(string currency, double? minPrice,
        double? maxPrice, bool expectedFound)
    {
        // Debugging Multi-Currency Price Filter
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());

        var uniqueTitle = $"MultiCurrency-{Guid.NewGuid()}";
        // Create book with: USD=10, EUR=50
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Test description") },
            PublicationDate = new PartialDate(2024, 1, 1),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m, ["EUR"] = 50.0m }
        };

        // Wait for projection
        _ = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Poll wait for search index/projection availability
        var ready = false;
        for (var i = 0; i < TestConstants.DefaultMaxRetries; i++)
        {
            var c = await publicClient.GetBooksAsync(new BookSearchRequest { Search = uniqueTitle });
            if (c != null && c.Items.Any(b => b.Title == uniqueTitle))
            {
                ready = true;
                break;
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(ready).IsTrue();

        // Execute Search
        var request = new BookSearchRequest
        {
            Search = uniqueTitle,
            Currency = currency,
            MinPrice = (decimal?)minPrice,
            MaxPrice = (decimal?)maxPrice
        };

        var content = await publicClient.GetBooksAsync(request);

        // Assert
        var actuallyFound = content!.Items.Any(b => b.Title == uniqueTitle);
        if (expectedFound)
        {
            _ = await Assert.That(actuallyFound).IsTrue();
        }
        else
        {
            _ = await Assert.That(actuallyFound).IsFalse();
        }
    }

    [Test]
    public async Task SearchBooks_WithActiveSale_ShouldFilterByDiscountedPrice()
    {
        // Debugging Price Filter taking Sale into account
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());

        var uniqueTitle = $"SaleBook-{Guid.NewGuid()}";
        // Create book with Price=50 USD
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Sales test") },
            PublicationDate = new PartialDate(2024, 1, 1),
            Prices = new Dictionary<string, decimal> { ["USD"] = 50.0m }
        };

        var book = await TestHelpers.CreateBookAsync(authClient, createRequest);
        var bookId = book.Id;

        // Verify initially NOT found with MaxPrice=40 (Price is 50)
        var preSaleList = await publicClient.GetBooksAsync(new BookSearchRequest
        {
            Search = uniqueTitle,
            MaxPrice = 40,
            Currency = "USD"
        });
        _ = await Assert.That(preSaleList!.Items.Any(b => b.Title == uniqueTitle)).IsFalse();

        // Schedule 50% Sale
        // 50 * 0.5 = 25. Should be found with MaxPrice=40.
        var saleRequest =
            new ScheduleSaleRequest(50m, DateTimeOffset.UtcNow.AddSeconds(-5), DateTimeOffset.UtcNow.AddDays(1));

        var putReceived = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookUpdated",
            async () => await authClient.ScheduleBookSaleAsync(bookId, saleRequest, book.ETag),
            TimeSpan.FromSeconds(5));

        _ = await Assert.That(putReceived).IsTrue();

        // Refresh book to get new ETag if we needed it later (not needed here but good practice)
        book = await authClient.GetBookAsync(bookId);

        // Poll for search projection to update with Sale
        var foundOnSale = false;
        for (var i = 0; i < TestConstants.LongRetryCount * 1.5; i++)
        {
            // Search with MaxPrice=40. Desired price is 25.
            var list = await publicClient.GetBooksAsync(new BookSearchRequest
            {
                Search = uniqueTitle,
                MaxPrice = 40,
                Currency = "USD"
            });

            if (list != null && list.Items.Any(b => b.Id == bookId))
            {
                foundOnSale = true;
                break;
            }

            await Task.Delay((int)TestConstants.DefaultRetryDelay.TotalMilliseconds);
        }

        _ = await Assert.That(foundOnSale).IsTrue();
    }
}
