
using BookStore.Shared.Models;
using Marten;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class BookFilterRegressionTests
{
    [Test]
    public async Task SearchBooks_InNonDefaultTenant_ShouldRespectAuthorFilter()
    {
        // Debugging Multi-Tenant Author Filter
        var tenantId = "book-filter-test-tenant";

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
        var defaultClient = TestHelpers.GetUnauthenticatedClient();
        var loginRes = await TestHelpers.LoginAsAdminAsync(defaultClient, tenantId);
        var adminClient = await TestHelpers.GetTenantClientAsync(tenantId, loginRes!.AccessToken);

        // Create Author in this tenant
        var authorReq = TestHelpers.GenerateFakeAuthorRequest();
        var authorId = Guid.Empty;

        // Use ExecuteAndWaitForEventAsync to ensure projection consistency
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "AuthorUpdated", async () =>
        {
            var authorRes = await adminClient.PostAsJsonAsync("/api/admin/authors", authorReq);
            _ = await Assert.That(authorRes.StatusCode).IsEqualTo(HttpStatusCode.Created);

            var authorJson = await authorRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            if (authorJson.TryGetProperty("id", out var idProp) || authorJson.TryGetProperty("Id", out idProp))
            {
                authorId = idProp.GetGuid();
            }
            else
            {
                Assert.Fail("Could not retrieve Author ID");
            }
        }, TimeSpan.FromSeconds(5));

        // Create Book linked to this Author
        var bookReq = TestHelpers.GenerateFakeBookRequest(authorIds: new[] { authorId });
        var book = await TestHelpers.CreateBookAsync(adminClient, bookReq);

        // Search in correct tenant
        var tenantClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        tenantClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);

        // Poll for consistency (Book projection)
        var foundInTenant = false;
        for (var i = 0; i < 10; i++)
        {
            var res = await tenantClient.GetAsync($"/api/books?authorId={authorId}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
                if (list != null && list.Items.Any(b => b.Id == book.Id))
                {
                    foundInTenant = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundInTenant).IsTrue();

        // Search in WRONG tenant (Default)
        var defaultTenantClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        // No X-Tenant-ID header implies default tenant

        var resDefault = await defaultTenantClient.GetAsync($"/api/books?authorId={authorId}");
        // Should be 200 OK but empty result, because Author doesn't exist in default tenant context
        // OR it might ignore the author filter if author not found? Ideally it respects the filter and returns 0.

        _ = await Assert.That(resDefault.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var listDefault = await resDefault.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();

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
        // We reuse an authenticated client and public client
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var uniqueTitle = $"MultiCurrency-{Guid.NewGuid()}";
        // Create book with: USD=10, EUR=50
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Test description" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m, ["EUR"] = 50.0m }
        };

        // Wait for projection
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "BookUpdated", async () =>
        {
            var res = await authClient.PostAsJsonAsync("/api/admin/books", createRequest);
            _ = await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }, TimeSpan.FromSeconds(5));

        // Poll wait for search index/projection availability
        // Only need to do this once per test ideally, but for parameterized tests each run is independent.
        // We can check if ANY variant of the book is visible (e.g. searching by title without filters)
        var ready = false;
        for (var i = 0; i < 10; i++)
        {
            var r = await publicClient.GetAsync($"/api/books?search={uniqueTitle}");
            if (r.IsSuccessStatusCode)
            {
                var c = await r.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
                if (c != null && c.Items.Any(b => b.Title == uniqueTitle))
                {
                    ready = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(ready).IsTrue();

        // Build Query String
        var queryString = $"/api/books?search={uniqueTitle}&currency={currency}";
        if (minPrice.HasValue)
        {
            queryString += $"&minPrice={minPrice.Value}";
        }

        if (maxPrice.HasValue)
        {
            queryString += $"&maxPrice={maxPrice.Value}";
        }

        // Execute Search
        var resFilter = await publicClient.GetAsync(queryString);
        _ = await Assert.That(resFilter.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await resFilter.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();

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
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var uniqueTitle = $"SaleBook-{Guid.NewGuid()}";
        // Create book with Price=50 USD
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Sales test" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 50.0m }
        };

        var bookId = Guid.Empty;
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "BookUpdated", async () =>
        {
            var res = await authClient.PostAsJsonAsync("/api/admin/books", createRequest);
            var content = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            bookId = content.GetProperty("id").GetGuid();
            _ = await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }, TimeSpan.FromSeconds(5));

        // Verify initially NOT found with MaxPrice=40 (Price is 50)
        var preSaleRes = await publicClient.GetAsync($"/api/books?search={uniqueTitle}&maxPrice=40&currency=USD");
        var preSaleList = await preSaleRes.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
        _ = await Assert.That(preSaleList!.Items.Any(b => b.Title == uniqueTitle)).IsFalse();

        // Schedule 50% Sale
        // 50 * 0.5 = 25. Should be found with MaxPrice=40.
        var saleRequest = new
        {
            Percentage = 0.5m,
            Start = DateTimeOffset.UtcNow.AddDays(-1),
            End = DateTimeOffset.UtcNow.AddDays(1)
        };

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(bookId, "BookSaleScheduled", async () =>
        {
            var res = await authClient.PostAsJsonAsync($"/api/books/{bookId}/sales", saleRequest);
            _ = await Assert.That(res.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }, TimeSpan.FromSeconds(5));

        // Poll for search projection to update with Sale
        // Note: Sale application usually triggers re-indexing or at least projection update.
        // BookSearchProjection handles BookSaleScheduled.
        var foundOnSale = false;
        for (var i = 0; i < 10; i++)
        {
            // Search with MaxPrice=40. Desired price is 25.
            var res = await publicClient.GetAsync($"/api/books?search={uniqueTitle}&maxPrice=40&currency=USD");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
                if (list != null && list.Items.Any(b => b.Id == bookId))
                {
                    foundOnSale = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundOnSale).IsTrue();
    }
}
