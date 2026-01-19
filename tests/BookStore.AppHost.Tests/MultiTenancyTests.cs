using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookStore.Client;
using BookStore.Shared.Models;
using Marten;

namespace BookStore.AppHost.Tests;

public class MultiTenancyTests
{
    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var connectionString = await GlobalHooks.App.GetConnectionStringAsync("bookstore");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not retrieve connection string for 'bookstore' resource.");
        }

        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(connectionString);
            _ = opts.Policies.AllDocumentsAreMultiTenanted();
            opts.Events.TenancyStyle = Marten.Storage.TenancyStyle.Conjoined;
        });

        await TestHelpers.SeedTenantAsync(store, "acme");
        await TestHelpers.SeedTenantAsync(store, "contoso");
    }

    [Test]
    public async Task EntitiesAreIsolatedByTenant()
    {
        // 1. Setup Clients
        var app = GlobalHooks.App!;

        // Login as Acme Admin
        using var acmeHttp = app.CreateHttpClient("apiservice");
        var acmeLogin = await TestHelpers.LoginAsAdminAsync(acmeHttp, "acme");
        _ = await Assert.That(acmeLogin).IsNotNull();

        using var acmeClient = await TestHelpers.GetTenantClientAsync("acme", acmeLogin!.AccessToken);

        // Login as Contoso Admin
        using var contosoHttp = app.CreateHttpClient("apiservice");
        var contosoLogin = await TestHelpers.LoginAsAdminAsync(contosoHttp, "contoso");
        _ = await Assert.That(contosoLogin).IsNotNull();

        using var contosoClient = await TestHelpers.GetTenantClientAsync("contoso", contosoLogin!.AccessToken);

        // 2. Create Book in ACME
        var createRequest = TestHelpers.GenerateFakeBookRequest();
        var createResponse = await acmeClient.PostAsJsonAsync("/api/admin/books", createRequest);
        _ = await Assert.That(createResponse.IsSuccessStatusCode).IsTrue();

        var createdBook = await createResponse.Content.ReadFromJsonAsync<BookDto>();
        _ = await Assert.That(createdBook).IsNotNull();
        var bookId = createdBook!.Id;

        // 3. Verify visible in ACME (wait for eventual consistency/projections)
        await TestHelpers.WaitForConditionAsync(async () =>
        {
            var response = await acmeClient.GetAsync($"/api/books/{bookId}");
            return response.IsSuccessStatusCode;
        }, TimeSpan.FromSeconds(10), "Book not found in ACME tenant");

        var acmeGet = await acmeClient.GetAsync($"/api/books/{bookId}");
        _ = await Assert.That(acmeGet.IsSuccessStatusCode).IsTrue();

        // 4. Verify NOT visible in CONTOSO
        var contosoGet = await contosoClient.GetAsync($"/api/books/{bookId}");
        _ = await Assert.That(contosoGet.StatusCode).IsEqualTo(System.Net.HttpStatusCode.NotFound);

        // 5. Verify Search Isolation
        var acmeSearch = await acmeClient.GetFromJsonAsync<PagedListDto<BookDto>>($"/api/books?query={createdBook.Title}");
        _ = await Assert.That(acmeSearch).IsNotNull();
        _ = await Assert.That(acmeSearch!.Items.Any(b => b.Id == bookId)).IsTrue();

        var contosoSearch = await contosoClient.GetFromJsonAsync<PagedListDto<BookDto>>($"/api/books?query={createdBook.Title}");
        _ = await Assert.That(contosoSearch).IsNotNull();
        _ = await Assert.That(contosoSearch!.Items.Any(b => b.Id == bookId)).IsFalse();
    }

    [Test]
    public async Task InvalidTenantReturns400()
    {
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", "garbage-tenant-id");

        var response = await client.GetAsync("/api/books");
        _ = await Assert.That(response.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
    }
}
