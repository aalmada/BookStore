using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Tests to verify that user accounts are properly isolated between tenants.
/// CRITICAL: These tests expose a security vulnerability where users can authenticate across tenants.
/// </summary>
public class AccountIsolationTests
{
    [Test]
    public async Task User_RegisteredOnTenantA_CannotLoginOnTenantB()
    {
        // Arrange: two fresh isolated tenants and a unique user
        var tenantA = FakeDataGenerators.GenerateFakeTenantId();
        var tenantB = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenantA);
        await DatabaseHelpers.CreateTenantViaApiAsync(tenantB);

        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var clientA = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantA));
        var clientB = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantB));

        // Act 1: Register user on tenant A
        _ = await clientA.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Attempt login on tenant B with same credentials
        var exception = await Assert.That(async () =>
            await clientB.LoginAsync(new LoginRequest(userEmail, password)))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task User_RegisteredOnTenant_CanLoginOnSameTenant()
    {
        // Arrange: fresh tenant and unique user
        var tenantId = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenantId);

        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var client = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenantId));

        // Act: register then login on the same tenant
        _ = await client.RegisterAsync(new RegisterRequest(userEmail, password));
        var loginResult = await client.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: should succeed
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task User_RegisteredOnDefault_CannotLoginOnAnotherTenant()
    {
        // Arrange: a fresh non-default tenant and a unique user
        var otherTenant = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(otherTenant);

        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var defaultClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient());
        var otherClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(otherTenant));

        // Act 1: Register on default tenant
        _ = await defaultClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Attempt login on the other tenant
        var exception = await Assert.That(async () =>
            await otherClient.LoginAsync(new LoginRequest(userEmail, password)))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
