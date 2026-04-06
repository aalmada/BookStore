using System.Net;
using System.Net.Http.Headers;
using BookStore.AppHost.Tests.Helpers;
using BookStore.ServiceDefaults;
using JasperFx;

namespace BookStore.AppHost.Tests;

public class KeycloakAuthTests
{
    [Test]
    public async Task Login_ValidCredentials_ReturnsAccessToken()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);

        var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);

        _ = await Assert.That(loginResponse).IsNotNull();
        _ = await Assert.That(loginResponse!.AccessToken).IsNotEmpty();
    }

    [Test]
    public async Task Login_InvalidCredentials_Returns401OrError()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);

        using var response = await AuthenticationHelpers.RequestPasswordGrantAsync(
            keycloakClient,
            keycloakUrl,
            "admin@default.com",
            "WrongPassword123!");

        var isRejected = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized;
        _ = await Assert.That(isRejected).IsTrue();
    }

    [Test]
    public async Task AccessToken_ContainsTenantIdClaim()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);

        var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);
        _ = await Assert.That(loginResponse).IsNotNull();

        var tenantId = AuthenticationHelpers.GetStringClaimFromToken(loginResponse!.AccessToken, "tenant_id");

        _ = await Assert.That(tenantId).IsEqualTo(StorageConstants.DefaultTenantId);
    }

    [Test]
    public async Task AccessToken_ContainsAdminRoleClaim()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);

        var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);
        _ = await Assert.That(loginResponse).IsNotNull();

        var roles = AuthenticationHelpers.GetStringArrayClaimFromToken(loginResponse!.AccessToken, "roles");
        if (roles.Length == 0)
        {
            roles = AuthenticationHelpers.GetNestedStringArrayClaimFromToken(
                loginResponse.AccessToken,
                "realm_access",
                "roles");
        }

        _ = await Assert.That(roles.Contains("Admin")).IsTrue();
    }

    [Test]
    public async Task ApiService_AcceptsKeycloakToken_ForProtectedEndpoint()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);
        var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);
        _ = await Assert.That(loginResponse).IsNotNull();

        using var apiClient = HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId);
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse!.AccessToken);

        using var response = await apiClient.GetAsync("/api/admin/tenants");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ApiService_Rejects_RequestWithoutToken_With401()
    {
        using var apiClient = HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId);

        using var response = await apiClient.GetAsync("/api/admin/tenants");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApiService_Rejects_TamperedToken_With401()
    {
        using var keycloakClient = GlobalHooks.App!.CreateHttpClient(ResourceNames.Keycloak);
        var keycloakUrl = AuthenticationHelpers.GetServiceBaseUrl(keycloakClient);
        var loginResponse = await AuthenticationHelpers.LoginAsAdminAsync(keycloakClient, keycloakUrl);
        _ = await Assert.That(loginResponse).IsNotNull();

        var tamperedToken = TamperToken(loginResponse!.AccessToken);

        using var apiClient = HttpClientHelpers.GetUnauthenticatedClient(StorageConstants.DefaultTenantId);
        apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        using var response = await apiClient.GetAsync("/api/admin/tenants");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    static string TamperToken(string accessToken)
    {
        var lastCharacter = accessToken[^1];
        var replacement = lastCharacter == 'a' ? 'b' : 'a';
        return string.Concat(accessToken.AsSpan(0, accessToken.Length - 1), replacement.ToString());
    }
}
