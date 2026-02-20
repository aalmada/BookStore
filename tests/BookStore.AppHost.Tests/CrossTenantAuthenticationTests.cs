using System.Net;
using BookStore.ApiService.Models;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;
using TUnit;

namespace BookStore.AppHost.Tests;

/// <summary>
/// Tests to verify that authentication is properly isolated between tenants.
/// CRITICAL: These tests validate that users cannot authenticate across tenant boundaries.
/// </summary>
public class CrossTenantAuthenticationTests
{
    [Before(Class)]
    public static async Task ClassSetup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        await DatabaseHelpers.CreateTenantViaApiAsync("tenant-a");
        await DatabaseHelpers.CreateTenantViaApiAsync("tenant-b");
    }

    [Test]
    [Arguments("tenant-a", "tenant-b")]
    [Arguments("tenant-a", "default")]
    [Arguments("tenant-b", "default")]
    public async Task User_RegisteredInSourceTenant_CannotLoginToTargetTenant(string sourceTenant, string targetTenant)
    {
        // Arrange: Create a unique user email for this test
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var sourceClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(sourceTenant));
        var targetClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(targetTenant));

        // Act 1: Register user in source tenant
        _ = await sourceClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Attempt to login with the same credentials in target tenant
        var loginTask = targetClient.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: Login should FAIL because user is registered in source tenant, not target
        var exception = await Assert.That(async () => await loginTask).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Arguments("tenant-a")]
    [Arguments("tenant-b")]
    [Arguments("default")]
    public async Task User_RegisteredInSourceTenant_CanLoginInSourceTenant(string sourceTenant)
    {
        // Arrange: Create a unique user email for this test
        var userEmail = FakeDataGenerators.GenerateFakeEmail();
        var password = FakeDataGenerators.GenerateFakePassword();

        var sourceClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(sourceTenant));

        // Act 1: Register user in source tenant
        _ = await sourceClient.RegisterAsync(new RegisterRequest(userEmail, password));

        // Act 2: Login with the same credentials in source tenant (correct tenant)
        var loginResult = await sourceClient.LoginAsync(new LoginRequest(userEmail, password));

        // Assert: Login should succeed in the correct tenant
        _ = await Assert.That(loginResult).IsNotNull();
        _ = await Assert.That(loginResult.AccessToken).IsNotNull().And.IsNotEmpty();
        _ = await Assert.That(loginResult.RefreshToken).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    [Arguments("tenant-a", "tenant-b")]
    [Arguments("tenant-b", "tenant-a")]
    public async Task Passkey_ListWithCrossTenantJWT_IsRejected(string sourceTenant, string targetTenant)
    {
        // Arrange: Create a user with a passkey in the source tenant.
        // This test verifies that a JWT issued for the source tenant is REJECTED when used
        // to list passkeys on the target tenant — it does NOT test passkey-based login.
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync(sourceTenant);
        var credentialId = Guid.CreateVersion7().ToByteArray();
        await PasskeyTestHelpers.AddPasskeyToUserAsync(sourceTenant, email, "Test Passkey", credentialId);

        // Get fresh token after adding passkey (security stamp changed)
        var identityClient = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(sourceTenant));
        var refreshedResponse = await identityClient.LoginAsync(new LoginRequest(email, password));

        // Verify passkey exists in source tenant
        var sourcePasskeyClient = RestService.For<IPasskeyClient>(
            HttpClientHelpers.GetAuthenticatedClient(refreshedResponse.AccessToken, sourceTenant));
        var sourcePasskeys = await sourcePasskeyClient.ListPasskeysAsync();
        _ = await Assert.That(sourcePasskeys.Any(p => p.Name == "Test Passkey")).IsTrue();

        // Act: Send a source-tenant JWT to a target-tenant passkey endpoint.
        var targetPasskeyClient = RestService.For<IPasskeyClient>(
            HttpClientHelpers.GetAuthenticatedClient(refreshedResponse.AccessToken, targetTenant));

        var exception = await Assert.That(async () => await targetPasskeyClient.ListPasskeysAsync()).Throws<ApiException>();

        // Assert: Cross-tenant JWT must be rejected (403 Forbidden or 401 Unauthorized).
        var isRejected = exception!.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task RefreshToken_FromSourceTenant_FailsInTargetTenant()
    {
        // Arrange: Create user and get tokens in tenant-a
        var (email, password, loginResponse, _) = await AuthenticationHelpers.RegisterAndLoginUserAsync("tenant-a");
        var refreshToken = loginResponse.RefreshToken;
        var accessToken = loginResponse.AccessToken;

        // Act: Try to use refresh token in tenant-b (cross-tenant attack scenario)
        var targetClient = RestService.For<IIdentityClient>(
            HttpClientHelpers.GetAuthenticatedClient(accessToken, "tenant-b"));

        var exception = await Assert.That(async () =>
            await targetClient.RefreshTokenAsync(new RefreshRequest(refreshToken)))
            .Throws<ApiException>();

        // Assert: Should fail with Forbidden (tenant mismatch detected)
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("tenant-a", "tenant-b")]
    [Arguments("tenant-b", "default")]
    public async Task SameEmailDifferentTenants_AreIsolated(string tenant1, string tenant2)
    {
        // Arrange: Use the same email address for both tenants
        var sharedEmail = FakeDataGenerators.GenerateFakeEmail();
        var password1 = FakeDataGenerators.GenerateFakePassword();
        var password2 = FakeDataGenerators.GenerateFakePassword(); // Different password for tenant2

        var client1 = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenant1));
        var client2 = RestService.For<IIdentityClient>(HttpClientHelpers.GetUnauthenticatedClient(tenant2));

        // Act: Register the same email in both tenants with different passwords
        _ = await client1.RegisterAsync(new RegisterRequest(sharedEmail, password1));
        _ = await client2.RegisterAsync(new RegisterRequest(sharedEmail, password2));

        // Assert: Login with tenant1 credentials should work in tenant1
        var login1 = await client1.LoginAsync(new LoginRequest(sharedEmail, password1));
        _ = await Assert.That(login1).IsNotNull();
        _ = await Assert.That(login1.AccessToken).IsNotEmpty();

        // Assert: Login with tenant2 credentials should work in tenant2
        var login2 = await client2.LoginAsync(new LoginRequest(sharedEmail, password2));
        _ = await Assert.That(login2).IsNotNull();
        _ = await Assert.That(login2.AccessToken).IsNotEmpty();

        // Assert: Tenant1 password should NOT work in tenant2
        var wrongPasswordException = await Assert.That(async () =>
            await client2.LoginAsync(new LoginRequest(sharedEmail, password1)))
            .Throws<ApiException>();
        _ = await Assert.That(wrongPasswordException!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);

        // Assert: Tenant2 password should NOT work in tenant1
        var wrongPasswordException2 = await Assert.That(async () =>
            await client1.LoginAsync(new LoginRequest(sharedEmail, password2)))
            .Throws<ApiException>();
        _ = await Assert.That(wrongPasswordException2!.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
