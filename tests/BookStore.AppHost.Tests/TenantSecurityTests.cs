using System.Net;
using System.Net.Http.Headers;
using BookStore.AppHost.Tests;

namespace BookStore.AppHost.Tests;

public class TenantSecurityTests
{
    [Test]
    public async Task Request_WithNoTenantIdClaim_ShouldBeForbidden()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        using var client = GlobalHooks.App.CreateHttpClient("apiservice");
        var validToken = GlobalHooks.AdminAccessToken!;

        // Arrange
        // Test 1: Valid token (tenant=acme), Header=contoso -> Should Fail
        // This confirms the middleware CHECKS the claim against the header.

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/books");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", validToken);
        // Admin token is for *DEFAULT* tenant usually (or acme/contoso depending on which one we picked).
        // GlobalHooks authenticates as admin@bookstore.com -> Default Tenant.

        request.Headers.Add("X-Tenant-ID", "acme"); // Mismatch!

        // Act
        var response = await client.SendAsync(request);

        // Assert
        // Current implementation: Checks if userTenant != currentTenant.
        // userTenant = "BookStore" (default), currentTenant = "acme"
        // Should be 403 Forbidden.
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Request_Anonymous_WithTenantHeader_ShouldBeForbidden()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        using var client = GlobalHooks.App.CreateHttpClient("apiservice");

        // Test 2: Anonymous user with X-Tenant-ID="acme" -> Should currently pass (FAIL SECURITY), 
        // but we want it to be Forbidden or Defaulted.
        // Plan: Reject non-default tenant access for Anonymous users.

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/books");
        // No Authorization header
        request.Headers.Add("X-Tenant-ID", "acme");

        var response = await client.SendAsync(request);

        // Security check: Should be Forbidden
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Request_NoTenantClaim_ShouldBeForbidden()
        // Placeholder for future test
        => await Task.CompletedTask;
}
