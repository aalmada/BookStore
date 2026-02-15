using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Marten;
using Refit;
using BookStore.AppHost.Tests.Helpers;

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

        await DatabaseHelpers.SeedTenantAsync(store, "acme");
        await DatabaseHelpers.SeedTenantAsync(store, "contoso");
    }

    [Test]
    public async Task EntitiesAreIsolatedByTenant()
    {
        // 1. Setup Clients
        // Login as Acme Admin
        var acmeLogin = await AuthenticationHelpers.LoginAsAdminAsync("acme");
        _ = await Assert.That(acmeLogin).IsNotNull();
        var acmeClient =
            RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(acmeLogin!.AccessToken, "acme"));

        // Login as Contoso Admin
        var contosoLogin = await AuthenticationHelpers.LoginAsAdminAsync("contoso");
        _ = await Assert.That(contosoLogin).IsNotNull();
        var contosoClient =
            RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(contosoLogin!.AccessToken, "contoso"));

        // 2. Create Book in ACME
        var createRequest = FakeDataGenerators.GenerateFakeBookRequest();
        // Use CreateBookAsync helper that handles dependencies and SSE waiting
        // Wait, BookHelpers.CreateBookAsync takes IBooksClient and CreateBookRequest.
        // It should handle it.
        var createdBook = await BookHelpers.CreateBookAsync(acmeClient, createRequest);
        _ = await Assert.That(createdBook).IsNotNull();
        var bookId = createdBook.Id;

        // 3. Verify visible in ACME
        var acmeBook = await acmeClient.GetBookAsync(bookId);
        _ = await Assert.That(acmeBook).IsNotNull();
        _ = await Assert.That(acmeBook.Id).IsEqualTo(bookId);

        // 4. Verify NOT visible in CONTOSO
        var exception = await Assert.That(async () => await contosoClient.GetBookAsync(bookId)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        // 5. Verify Search Isolation
        var acmeSearch = await acmeClient.GetBooksAsync(new BookSearchRequest { Search = createdBook.Title });
        _ = await Assert.That(acmeSearch).IsNotNull();
        _ = await Assert.That(acmeSearch.Items.Any(b => b.Id == bookId)).IsTrue();

        var contosoSearch = await contosoClient.GetBooksAsync(new BookSearchRequest { Search = createdBook.Title });
        _ = await Assert.That(contosoSearch).IsNotNull();
        _ = await Assert.That(contosoSearch.Items.Any(b => b.Id == bookId)).IsFalse();
    }

    [Test]
    public async Task InvalidTenantReturns400()
    {
        // For invalid tenant, we can stick to HttpClient or simpler check.
        // Or create a client with bad tenant ID and expect failure on all calls?
        // But the middleware validates tenant existence or format.
        // If we use IBooksClient with bad tenant, standard Refit call handles it.

        var client = RestService.For<IBooksClient>(HttpClientHelpers.GetUnauthenticatedClient("garbage-tenant-id"));

        var exception = await Assert.That(async () => await client.GetBooksAsync(new BookSearchRequest()))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
