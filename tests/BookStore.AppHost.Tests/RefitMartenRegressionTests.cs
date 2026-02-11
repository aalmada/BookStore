using BookStore.Client;
using BookStore.Shared.Models;
using Marten;
using Refit;
using Weasel.Core;
using CreateAuthorRequest = BookStore.Client.CreateAuthorRequest;
using CreateBookRequest = BookStore.Client.CreateBookRequest;

namespace BookStore.AppHost.Tests;

public class RefitMartenRegressionTests
{
    [Test]
    public async Task GetPublishers_ShouldReturnPagedListDto_MatchingRefitExpectation()
    {
        // Arrange
        var client = RestService.For<IPublishersClient>(TestHelpers.GetUnauthenticatedClient());

        // Act
        // This effectively tests that the server response structure matches PagedListDto<T>
        // which was the core of the Refit deserialization issue.
        var response = await client.GetPublishersAsync(null, null);

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Items).IsNotNull();
        // The endpoint might return empty list if no publishers 
    }

    [Test]
    public async Task SearchBooks_WithPriceFilter_ShouldNotThrow500()
    {
        // Arrange
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());

        // Create a book with a specific price to ensure we have data to query against
        var uniqueTitle = $"PriceTest-{Guid.NewGuid()}";
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Test description") },
            PublicationDate = new PartialDate(2024, 1, 1),
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
        };
        _ = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Act
        // This query caused Marten.Exceptions.BadLinqExpressionException before the fix
        // because it was trying to query the Dictionary directly.
        var response = await publicClient.GetBooksAsync(new BookSearchRequest
        {
            Search = uniqueTitle, MinPrice = 5, MaxPrice = 15
        });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Items.Any(b => b.Title == uniqueTitle)).IsTrue();
    }

    [Test]
    public async Task SearchBooks_WithPriceFilter_ShouldExcludeOutOfRange()
    {
        // Arrange
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());

        // Create a book with price 20.0 (outside range 5-15)
        var uniqueTitle = $"OutOfRange-{Guid.NewGuid()}";
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Test description") },
            PublicationDate = new PartialDate(2024, 1, 1),
            Prices = new Dictionary<string, decimal> { ["USD"] = 20.0m }
        };
        _ = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Act
        var response = await publicClient.GetBooksAsync(new BookSearchRequest
        {
            Search = uniqueTitle, MinPrice = 5, MaxPrice = 15
        });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Items.Any(b => b.Title == uniqueTitle)).IsFalse();
    }

    [Test]
    public async Task SearchBooks_WithDateSort_ShouldNotThrow500()
    {
        // Arrange
        // Create a book to ensure data exists with the new PublicationDateString field populated
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var uniqueTitle = $"DateSort-{Guid.NewGuid()}";
        _ = await TestHelpers.CreateBookAsync(authClient,
            new CreateBookRequest
            {
                Id = Guid.CreateVersion7(),
                Title = uniqueTitle,
                Isbn = "978-0-00-000000-0",
                Language = "en",
                Translations =
                    new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Test description") },
                PublicationDate = new PartialDate(2024, 1, 1),
                Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
            });

        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());

        // Act
        // This query caused Marten.Exceptions.BadLinqExpressionException before the fix
        // because it was trying to sort by PartialDate components.
        var response = await publicClient.GetBooksAsync(new BookSearchRequest { SortBy = "date", SortOrder = "asc" });

        // Assert
        _ = await Assert.That(response).IsNotNull();
    }

    [Test]
    public async Task SearchBooks_WithPriceFilter_ShouldExcludeBooksWithHighPrimaryPrice_ButLowSecondaryPrice()
    {
        // Debugging Reproduction Test
        // "I tried it manually and its not working correctly."
        // Hypothesis: Filtering ignores currency. If a book has { USD: 100, EUR: 10 } and we filter MaxPrice=15 (assuming USD context),
        // it matches because 10 <= 15, even though the USD price is 100.

        // Arrange
        var authClient = await TestHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient());

        var uniqueTitle = $"CurrencyMismatch-{Guid.NewGuid()}";
        var createRequest = new CreateBookRequest
        {
            Id = Guid.CreateVersion7(),
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations =
                new Dictionary<string, BookTranslationDto> { ["en"] = new BookTranslationDto("Test description") },
            PublicationDate = new PartialDate(2024, 1, 1),
            Prices = new Dictionary<string, decimal>
            {
                ["USD"] = 100.0m, // Expensive in USD
                ["EUR"] = 10.0m // Cheap in EUR
            }
        };
        _ = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Act
        // Filter: MaxPrice 15 AND Currency=USD.
        // The system should now verify p.Currency == "USD" && p.Value <= maxPrice.
        var response = await publicClient.GetBooksAsync(new BookSearchRequest
        {
            Search = uniqueTitle, MaxPrice = 15, Currency = "USD"
        });

        // Assert
        _ = await Assert.That(response).IsNotNull();

        // Assertion: Book should NOT be present because USD price (100) > MaxPrice (15) and EUR is ignored due to currency filter
        _ = await Assert.That(response.Items.Any(b => b.Title == uniqueTitle)).IsFalse();
    }

    [Test]
    public async Task SearchBooks_InNonDefaultTenant_WithAuthorFilter_ShouldReturnBooks()
    {
        // Debugging Reproduction Test
        // "the author filter works fine in the default tenant but not in other tenants"

        // Arrange
        var tenantId = $"author-filter-test-{Guid.NewGuid():N}";

        // We need to seed the tenant manually effectively because GlobalHooks doesn't expose the Store.
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Fail("Could not retrieve connection string for 'bookstore' resource.");
        }

        using (var store = DocumentStore.For(opts =>
               {
                   opts.Connection(connectionString!);
                   _ = opts.Policies.AllDocumentsAreMultiTenanted();
                   opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
                   // Use SystemTextJson to match the app
                   opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
               }))
        {
            await TestHelpers.SeedTenantAsync(store, tenantId);
        }

        // 1. Authenticate as Admin in the new tenant
        var loginRes = await TestHelpers.LoginAsAdminAsync(tenantId);
        _ = await Assert.That(loginRes).IsNotNull();

        var adminClient =
            RestService.For<IAuthorsClient>(TestHelpers.GetAuthenticatedClient(loginRes!.AccessToken, tenantId));
        var adminBooksClient =
            RestService.For<IBooksClient>(TestHelpers.GetAuthenticatedClient(loginRes!.AccessToken, tenantId));

        // 2. Create an Author in this tenant
        var authorReq = TestHelpers.GenerateFakeAuthorRequest();
        var authorRes = await adminClient.CreateAuthorWithResponseAsync(authorReq);
        _ = await Assert.That(authorRes.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var authorId = authorRes.Content!.Id;

        // 3. Create a Book linked to this Author
        var bookReq = TestHelpers.GenerateFakeBookRequest(authorIds: new[] { authorId });
        var book = await TestHelpers.CreateBookAsync(adminBooksClient, bookReq);

        // 4. Search for the book using the Author Filter
        var publicClient = RestService.For<IBooksClient>(TestHelpers.GetUnauthenticatedClient(tenantId));

        // Act
        var response = await publicClient.GetBooksAsync(new BookSearchRequest { AuthorId = authorId });

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response.Items.Any(b => b.Id == book.Id)).IsTrue();
    }

    [Test]
    public async Task GetAuthors_InDifferentTenants_ShouldNotReturnCachedResultsFromOtherTenant()
    {
        // Debugging Cache Leak
        // "the author filter works fine in the default tenant but not in other tenants"
        // Root cause suspect: GetAuthors cache key does not include TenantId.

        // Arrange
        var tenantA = $"tenant-a-{Guid.NewGuid():N}";
        var tenantB = $"tenant-b-{Guid.NewGuid():N}";

        // Seed Tenants
        var connectionString = await GlobalHooks.App!.GetConnectionStringAsync("bookstore");
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString!);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
            opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
        });

        await TestHelpers.SeedTenantAsync(store, tenantA);
        await TestHelpers.SeedTenantAsync(store, tenantB);

        var loginResA = await TestHelpers.LoginAsAdminAsync(tenantA);
        var adminClientA =
            RestService.For<IAuthorsClient>(TestHelpers.GetAuthenticatedClient(loginResA!.AccessToken, tenantA));

        var loginResB = await TestHelpers.LoginAsAdminAsync(tenantB);
        var adminClientB =
            RestService.For<IAuthorsClient>(TestHelpers.GetAuthenticatedClient(loginResB!.AccessToken, tenantB));

        // Create Unique Authors and wait for projection
        var authorReqA = TestHelpers.GenerateFakeAuthorRequest();
        var authorA = await TestHelpers.CreateAuthorAsync(adminClientA, authorReqA);
        _ = await Assert.That(authorA).IsNotNull();

        var authorReqB = TestHelpers.GenerateFakeAuthorRequest();
        var authorB = await TestHelpers.CreateAuthorAsync(adminClientB, authorReqB);
        _ = await Assert.That(authorB).IsNotNull();

        // Act & Assert
        // 1. Get Authors from Tenant A. Should contain Author A.
        var publicClientA = RestService.For<IAuthorsClient>(TestHelpers.GetUnauthenticatedClient(tenantA));

        var nameA = authorReqA.Name;

        await TestHelpers.WaitForConditionAsync(async () =>
        {
            var listA = await publicClientA.GetAuthorsAsync(null, null);
            return listA.Items.Any(a => a.Name == nameA);
        }, TestConstants.DefaultEventTimeout, "Author A was not found in Tenant A");

        // 2. Get Authors from Tenant B. Should contain Author B, AND NOT Author A.
        var publicClientB = RestService.For<IAuthorsClient>(TestHelpers.GetUnauthenticatedClient(tenantB));

        var nameB = authorReqB.Name;

        PagedListDto<AuthorDto>? listB = null;
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            listB = await publicClientB.GetAuthorsAsync(null, null);
            return listB.Items.Any(a => a.Name == nameB);
        }, TestConstants.DefaultEventTimeout, "Author B was not found in Tenant B");

        // Assert List B does NOT contain Author A.
        var containsAInB = listB!.Items.Any(a => a.Name == nameA);
        _ = await Assert.That(containsAInB).IsFalse();
    }
}
