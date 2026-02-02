using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using JasperFx;
using Marten;
using Refit;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Tests to verify that user accounts are properly isolated between tenants.
/// CRITICAL: These tests expose a security vulnerability where users can authenticate across tenants.
/// </summary>
public class AccountIsolationTests
{
    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Ensure tenants exist
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
    public async Task User_RegisteredOnContoso_CannotLoginOnAcme()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        var contosoClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient("contoso"));
        var acmeClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient("acme"));

        // Act 1: Register user on Contoso tenant
        _ = await contosoClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Attempt to login with the same credentials on Acme tenant
        var loginTask = acmeClient.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: Login should FAIL because user is registered on Contoso, not Acme
        var exception = await Assert.That(async () => await loginTask).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task User_RegisteredOnContoso_CanLoginOnContoso()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        var contosoClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient("contoso"));

        // Act 1: Register user on Contoso tenant
        _ = await contosoClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Login with the same credentials on Contoso tenant (correct tenant)
        var loginResult = await contosoClient.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: Login should succeed on the correct tenant
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task User_RegisteredOnAcme_CannotLoginOnContoso()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        var acmeClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient("acme"));
        var contosoClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient("contoso"));

        // Act 1: Register user on Acme tenant
        _ = await acmeClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Attempt to login with the same credentials on Contoso tenant
        var loginTask = contosoClient.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: Login should FAIL because user is registered on Acme, not Contoso
        var exception = await Assert.That(async () => await loginTask).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task User_RegisteredOnDefault_CannotLoginOnAcme()
    {
        // Arrange: Create a unique user email for this test
        var userEmail = $"isolation-test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        var defaultClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient());
        var acmeClient = RestService.For<IIdentityClient>(TestHelpers.GetUnauthenticatedClient("acme"));

        // Act 1: Register user on Default tenant (no X-Tenant-ID header)
        _ = await defaultClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Attempt to login with the same credentials on Acme tenant
        var loginTask = acmeClient.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: Login should FAIL because user is registered on Default, not Acme
        var exception = await Assert.That(async () => await loginTask).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
