using Aspire.Hosting;
using Aspire.Hosting.Testing;
using System.Net;
using System.Net.Http.Json;
using BookStore.Shared.Models;
using Marten;
using Projects;
using Weasel.Core;

namespace BookStore.AppHost.Tests;

public class RefitMartenRegressionTests
{
    [Test]
    public async Task GetPublishers_ShouldReturnPagedListDto_MatchingRefitExpectation()
    {
        // Arrange
        var httpClient = TestHelpers.GetUnauthenticatedClient();

        // Act
        // This effectively tests that the server response structure matches PagedListDto<T>
        // which was the core of the Refit deserialization issue.
        var response = await httpClient.GetFromJsonAsync<PagedListDto<PublisherDto>>("/api/publishers");

        // Assert
        _ = await Assert.That(response).IsNotNull();
        _ = await Assert.That(response!.Items).IsNotNull();
        // Even if empty, it should have deserialized the PagedList structure (PageSize, TotalItemCount, etc)
        _ = await Assert.That(response.PageSize).IsGreaterThan(0);
    }

    [Test]
    public async Task SearchBooks_WithPriceFilter_ShouldNotThrow500()
    {
        // Arrange
        // We need an authenticated client to create data efficiently via helper, 
        // but search can be unauthenticated.
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        // Create a book with a specific price to ensure we have data to query against
        var uniqueTitle = $"PriceTest-{Guid.NewGuid()}";
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Test description" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
        };
        _ = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Act
        // This query caused Marten.Exceptions.BadLinqExpressionException before the fix
        // because it was trying to query the Dictionary directly.
        var response = await publicClient.GetAsync($"/api/books?search={uniqueTitle}&minPrice=5&maxPrice=15");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
        _ = await Assert.That(content).IsNotNull();
        _ = await Assert.That(content!.Items.Any(b => b.Title == uniqueTitle)).IsTrue();
    }

    [Test]
    public async Task SearchBooks_WithPriceFilter_ShouldExcludeOutOfRange()
    {
        // Arrange
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        // Create a book with price 20.0 (outside range 5-15)
        var uniqueTitle = $"OutOfRange-{Guid.NewGuid()}";
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Test description" } },
            PublicationDate = new { Year = 2024 },
            Prices = new Dictionary<string, decimal> { ["USD"] = 20.0m }
        };
        _ = await TestHelpers.CreateBookAsync(authClient, createRequest);

        // Act
        var response = await publicClient.GetAsync($"/api/books?search={uniqueTitle}&minPrice=5&maxPrice=15");

        // Assert
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
        _ = await Assert.That(content).IsNotNull();
        _ = await Assert.That(content!.Items.Any(b => b.Title == uniqueTitle)).IsFalse();
    }

    [Test]
    public async Task SearchBooks_WithDateSort_ShouldNotThrow500()
    {
        // Arrange
        // Create a book to ensure data exists with the new PublicationDateString field populated
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var uniqueTitle = $"DateSort-{Guid.NewGuid()}";
        _ = await TestHelpers.CreateBookAsync(authClient,
            new
            {
                Title = uniqueTitle,
                Isbn = "978-0-00-000000-0",
                Language = "en",
                Translations = new Dictionary<string, object> { ["en"] = new { Description = "Test description" } },
                PublicationDate = new { Year = 2024 },
                Prices = new Dictionary<string, decimal> { ["USD"] = 10.0m }
            });

        var publicClient = TestHelpers.GetUnauthenticatedClient();

        // Act
        // This query caused Marten.Exceptions.BadLinqExpressionException before the fix
        // because it was trying to sort by PartialDate components.
        var response = await publicClient.GetAsync("/api/books?sortBy=date&sortOrder=asc");

        // Assert
        _ = await Assert.That((int)response.StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task SearchBooks_WithPriceFilter_ShouldExcludeBooksWithHighPrimaryPrice_ButLowSecondaryPrice()
    {
        // Debugging Reproduction Test
        // "I tried it manually and its not working correctly."
        // Hypothesis: Filtering ignores currency. If a book has { USD: 100, EUR: 10 } and we filter MaxPrice=15 (assuming USD context),
        // it matches because 10 <= 15, even though the USD price is 100.

        // Arrange
        var authClient = await TestHelpers.GetAuthenticatedClientAsync();
        var publicClient = TestHelpers.GetUnauthenticatedClient();

        var uniqueTitle = $"CurrencyMismatch-{Guid.NewGuid()}";
        var createRequest = new
        {
            Title = uniqueTitle,
            Isbn = "978-0-00-000000-0",
            Language = "en",
            Translations = new Dictionary<string, object> { ["en"] = new { Description = "Test description" } },
            PublicationDate = new { Year = 2024 },
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
        var response = await publicClient.GetAsync($"/api/books?search={uniqueTitle}&maxPrice=15&currency=USD");

        // Assert
        _ = await Assert.That((int)response.StatusCode).IsEqualTo(200);

        var content = await response.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
        _ = await Assert.That(content).IsNotNull();

        // Assertion: Book should NOT be present because USD price (100) > MaxPrice (15) and EUR is ignored due to currency filter
        _ = await Assert.That(content!.Items.Any(b => b.Title == uniqueTitle)).IsFalse();
    }

    [Test]
    public async Task SearchBooks_InNonDefaultTenant_WithAuthorFilter_ShouldReturnBooks()
    {
        // Debugging Reproduction Test
        // "the author filter works fine in the default tenant but not in other tenants"

        // Arrange
        var tenantId = $"author-filter-test-{Guid.NewGuid():N}";

        // We need to seed the tenant manually effectively because GlobalHooks doesn't expose the Store.
        // We can get the connection string and create a temporary store.
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
        // LoginAsAdminAsync returns a LoginResponse (Token), we need to create a client with it.
        // TestHelpers.GetTenantClientAsync takes a token.
        // But TestHelpers.GetTenantClientAsync also takes tenantId.
        // Let's use TestHelpers.LoginAsAdminAsync and then create the client.

        // We need a client to login first - can use default one.
        var defaultClient = TestHelpers.GetUnauthenticatedClient();
        var loginRes = await TestHelpers.LoginAsAdminAsync(defaultClient, tenantId);
        _ = await Assert.That(loginRes).IsNotNull();

        var adminClient = await TestHelpers.GetTenantClientAsync(tenantId, loginRes!.AccessToken);

        // 2. Create an Author in this tenant
        var authorReq = TestHelpers.GenerateFakeAuthorRequest();
        var authorRes = await adminClient.PostAsJsonAsync("/api/admin/authors", authorReq);
        _ = await Assert.That(authorRes.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var authorJson = await authorRes.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        // Assuming response is an object with Id property or similar, or checking if it is the ID itself wrapping in quotes?
        // Exception said "StartObject", so it is { ... }.
        // Let's try to find "id".
        if (!authorJson.TryGetProperty("id", out var idProp))
        {
            // Maybe it returned the whole object and property is "Id" (case sensitive)?
            if (!authorJson.TryGetProperty("Id", out idProp))
            {
                Assert.Fail($"Could not find 'id' property in response: {authorJson}");
            }
        }

        var authorId = idProp.GetGuid();

        // 3. Create a Book linked to this Author
        var bookReq = TestHelpers.GenerateFakeBookRequest(authorIds: new[] { authorId });
        // Use CreateBookAsync helper but ensure we pass the adminClient which is scoped to valid tenant
        var book = await TestHelpers.CreateBookAsync(adminClient, bookReq);

        // 4. Search for the book using the Author Filter
        // We can use an unauthenticated client or the same admin client. 
        // Let's use unauthenticated client for the tenant to simulate public search.
        var publicClient = GlobalHooks.App!.CreateHttpClient("apiservice");
        publicClient.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);

        // Act
        var response = await publicClient.GetAsync($"/api/books?authorId={authorId}");

        // Assert

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PagedListDto<BookDto>>();
        _ = await Assert.That(content).IsNotNull();
        _ = await Assert.That(content!.Items.Any(b => b.Id == book.Id)).IsTrue();
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

        var defaultClient = TestHelpers.GetUnauthenticatedClient();

        var loginResA = await TestHelpers.LoginAsAdminAsync(defaultClient, tenantA);
        var adminClientA = await TestHelpers.GetTenantClientAsync(tenantA, loginResA!.AccessToken);

        var loginResB = await TestHelpers.LoginAsAdminAsync(defaultClient, tenantB);
        var adminClientB = await TestHelpers.GetTenantClientAsync(tenantB, loginResB!.AccessToken);

        // Create Unique Authors and wait for projection
        var authorReqA = TestHelpers.GenerateFakeAuthorRequest();
        var authorIdA = Guid.Empty;

        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "AuthorCreated", async () =>
        {
            var authorResA = await adminClientA.PostAsJsonAsync("/api/admin/authors", authorReqA);
            _ = await Assert.That(authorResA.StatusCode).IsEqualTo(HttpStatusCode.Created);

            // Extract ID to confirm specific event if needed, but sequential is fine
            var json = await authorResA.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            // Helper to get ID if needed, but we trust the event for now
        }, TimeSpan.FromSeconds(5));

        var authorReqB = TestHelpers.GenerateFakeAuthorRequest();
        _ = await TestHelpers.ExecuteAndWaitForEventAsync(Guid.Empty, "AuthorUpdated", async () =>
        {
            var authorResB = await adminClientB.PostAsJsonAsync("/api/admin/authors", authorReqB);
            _ = await Assert.That(authorResB.StatusCode).IsEqualTo(HttpStatusCode.Created);
        }, TimeSpan.FromSeconds(5));

        // Act & Assert
        // 1. Get Authors from Tenant A. Should contain Author A.
        var publicClientA = GlobalHooks.App!.CreateHttpClient("apiservice");
        publicClientA.DefaultRequestHeaders.Add("X-Tenant-ID", tenantA);

        dynamic reqBodyA = authorReqA;
        string nameA = reqBodyA.Name;

        // Poll for consistency
        var foundA = false;
        for (var i = 0; i < 20; i++)
        {
            var resA = await publicClientA.GetAsync("/api/authors");
            if (resA.IsSuccessStatusCode)
            {
                var listA = await resA.Content.ReadFromJsonAsync<PagedListDto<AuthorDto>>();
                if (listA!.Items.Any(a => a.Name == nameA))
                {
                    foundA = true;
                    break;
                }
            }

            await Task.Delay(1000);
        }

        _ = await Assert.That(foundA).IsTrue();

        // 2. Get Authors from Tenant B. Should contain Author B, AND NOT Author A.
        // If cache leaks, we might get List A again if cache key is same.
        var publicClientB = GlobalHooks.App!.CreateHttpClient("apiservice");
        publicClientB.DefaultRequestHeaders.Add("X-Tenant-ID", tenantB);

        dynamic reqBodyB = authorReqB;
        string nameB = reqBodyB.Name;

        // Poll for Author B presence (wait for consistency)
        var foundB = false;

        PagedListDto<AuthorDto>? listB = null;
        for (var i = 0; i < 10; i++)
        {
            var resB = await publicClientB.GetAsync("/api/authors");
            if (resB.IsSuccessStatusCode)
            {
                listB = await resB.Content.ReadFromJsonAsync<PagedListDto<AuthorDto>>();
                if (listB!.Items.Any(a => a.Name == nameB))
                {
                    foundB = true;
                    break;
                }
            }

            await Task.Delay(500);
        }

        _ = await Assert.That(foundB).IsTrue();

        // Assert List B does NOT contain Author A.
        // We use the FINAL list captured from loop (or fresh fetch).
        // Since we confirmed B is present, let's check A presence in THAT list.
        var containsAInB = listB!.Items.Any(a => a.Name == nameA);
        _ = await Assert.That(containsAInB).IsFalse();
    }

// Internal DTO for the Publisher test if not available globally
    internal record PublisherDto(Guid Id, string Name);
}
