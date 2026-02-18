using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class MultiTenancyTests
{
    static string _tenant1 = string.Empty;
    static string _tenant2 = string.Empty;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        _tenant2 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(_tenant1);
        await DatabaseHelpers.CreateTenantViaApiAsync(_tenant2);
    }

    [Test]
    public async Task EntitiesAreIsolatedByTenant()
    {
        // 1. Setup Clients
        var tenant1Login = await AuthenticationHelpers.LoginAsAdminAsync(_tenant1);
        _ = await Assert.That(tenant1Login).IsNotNull();
        var tenant1Client =
            RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(tenant1Login!.AccessToken, _tenant1));

        var tenant2Login = await AuthenticationHelpers.LoginAsAdminAsync(_tenant2);
        _ = await Assert.That(tenant2Login).IsNotNull();
        var tenant2Client =
            RestService.For<IBooksClient>(HttpClientHelpers.GetAuthenticatedClient(tenant2Login!.AccessToken, _tenant2));

        // 2. Create Book in tenant1
        var createRequest = FakeDataGenerators.GenerateFakeBookRequest();
        var createdBook = await BookHelpers.CreateBookAsync(tenant1Client, createRequest);
        _ = await Assert.That(createdBook).IsNotNull();
        var bookId = createdBook.Id;

        // 3. Verify visible in tenant1
        var tenant1Book = await tenant1Client.GetBookAsync(bookId);
        _ = await Assert.That(tenant1Book).IsNotNull();
        _ = await Assert.That(tenant1Book.Id).IsEqualTo(bookId);

        // 4. Verify NOT visible in tenant2
        var exception = await Assert.That(async () => await tenant2Client.GetBookAsync(bookId)).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

        // 5. Verify search isolation
        var tenant1Search = await tenant1Client.GetBooksAsync(new BookSearchRequest { Search = createdBook.Title });
        _ = await Assert.That(tenant1Search).IsNotNull();
        _ = await Assert.That(tenant1Search.Items.Any(b => b.Id == bookId)).IsTrue();

        var tenant2Search = await tenant2Client.GetBooksAsync(new BookSearchRequest { Search = createdBook.Title });
        _ = await Assert.That(tenant2Search).IsNotNull();
        _ = await Assert.That(tenant2Search.Items.Any(b => b.Id == bookId)).IsFalse();
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
