using System.Net;
using System.Net.Http.Headers;
using BookStore.AppHost.Tests.Helpers;
using BookStore.ServiceDefaults;
using BookStore.Shared;

namespace BookStore.AppHost.Tests;

public class MultiTenantAuthenticationTests
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
    public async Task TenantCreation_CreatesAdminForEachTenant()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);

        var defaultLogin = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);
        var tenant1Login = await AuthenticationHelpers.LoginAsUserAsync(
            keycloakClient,
            keycloakUrl,
            $"admin@{_tenant1}.com",
            "Admin123!");
        var tenant2Login = await AuthenticationHelpers.LoginAsUserAsync(
            keycloakClient,
            keycloakUrl,
            $"admin@{_tenant2}.com",
            "Admin123!");

        _ = await Assert.That(defaultLogin).IsNotNull();
        _ = await Assert.That(defaultLogin!.AccessToken).IsNotEmpty();

        _ = await Assert.That(tenant1Login).IsNotNull();
        _ = await Assert.That(tenant1Login!.AccessToken).IsNotEmpty();

        _ = await Assert.That(tenant2Login).IsNotNull();
        _ = await Assert.That(tenant2Login!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task Login_TenantUser_TokenContainsCorrectTenantId()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);
        var keycloakAdminToken = await AuthenticationHelpers.GetKeycloakAdminTokenAsync(keycloakClient, keycloakUrl);

        var tenant1Email = FakeDataGenerators.GenerateFakeEmail();
        var tenant1Password = FakeDataGenerators.GenerateFakePassword();
        var tenant2Email = FakeDataGenerators.GenerateFakeEmail();
        var tenant2Password = FakeDataGenerators.GenerateFakePassword();

        _ = await AuthenticationHelpers.CreateTestUserInKeycloakAsync(
            keycloakClient,
            keycloakAdminToken,
            tenant1Email,
            tenant1Password,
            _tenant1,
            "User");
        _ = await AuthenticationHelpers.CreateTestUserInKeycloakAsync(
            keycloakClient,
            keycloakAdminToken,
            tenant2Email,
            tenant2Password,
            _tenant2,
            "User");

        var tenant1Login = await AuthenticationHelpers.LoginAsUserAsync(
            keycloakClient,
            keycloakUrl,
            tenant1Email,
            tenant1Password);
        var tenant2Login = await AuthenticationHelpers.LoginAsUserAsync(
            keycloakClient,
            keycloakUrl,
            tenant2Email,
            tenant2Password);

        _ = await Assert.That(tenant1Login).IsNotNull();
        _ = await Assert.That(tenant2Login).IsNotNull();

        var tenant1Claim = AuthenticationHelpers.GetStringClaimFromToken(tenant1Login!.AccessToken, "tenant_id");
        var tenant2Claim = AuthenticationHelpers.GetStringClaimFromToken(tenant2Login!.AccessToken, "tenant_id");

        _ = await Assert.That(tenant1Claim).IsEqualTo(_tenant1);
        _ = await Assert.That(tenant2Claim).IsEqualTo(_tenant2);
    }

    [Test]
    public async Task Login_TenantAUser_CannotAccessTenantBData()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);
        var keycloakAdminToken = await AuthenticationHelpers.GetKeycloakAdminTokenAsync(keycloakClient, keycloakUrl);

        var tenantAEmail = FakeDataGenerators.GenerateFakeEmail();
        var tenantAPassword = FakeDataGenerators.GenerateFakePassword();

        _ = await AuthenticationHelpers.CreateTestUserInKeycloakAsync(
            keycloakClient,
            keycloakAdminToken,
            tenantAEmail,
            tenantAPassword,
            _tenant1,
            "User");

        var tenantALogin = await AuthenticationHelpers.LoginAsUserAsync(
            keycloakClient,
            keycloakUrl,
            tenantAEmail,
            tenantAPassword);
        _ = await Assert.That(tenantALogin).IsNotNull();

        using var apiClient = HttpClientHelpers.GetUnauthenticatedClient(_tenant2);
        apiClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tenantALogin!.AccessToken);

        using var response = await apiClient.GetAsync("/api/cart");

        var isRejected = response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Forbidden
            or HttpStatusCode.Unauthorized;

        _ = await Assert.That(isRejected).IsTrue();
    }
}
