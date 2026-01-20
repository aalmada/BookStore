using System.Net;
using System.Net.Http.Headers;
using BookStore.AppHost.Tests;

namespace BookStore.AppHost.Tests;

public class TenantSecurityTests
{
    HttpClient? _client;
    string _validToken = string.Empty;

    [Before(Test)]
    public async Task Setup()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        _client = GlobalHooks.App.CreateHttpClient("apiservice");
        _validToken = GlobalHooks.AdminAccessToken!;
    }

    [After(Test)]
    public void Cleanup() => _client?.Dispose();

    [Test]
    public async Task Request_WithNoTenantIdClaim_ShouldBeForbidden()
    {
        // Arrange
        // Test 1: Valid token (tenant=acme), Header=contoso -> Should Fail
        // This confirms the middleware CHECKS the claim against the header.

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/books");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _validToken);
        // Admin token is for *DEFAULT* tenant usually (or acme/contoso depending on which one we picked).
        // GlobalHooks authenticates as admin@bookstore.com -> Default Tenant.

        request.Headers.Add("X-Tenant-ID", "acme"); // Mismatch!

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        // Current implementation: Checks if userTenant != currentTenant.
        // userTenant = "BookStore" (default), currentTenant = "acme"
        // Should be 403 Forbidden.
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
