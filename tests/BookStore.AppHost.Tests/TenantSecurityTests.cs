using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using JasperFx;
using Microsoft.IdentityModel.Tokens;
using Refit;

namespace BookStore.AppHost.Tests;

public class TenantSecurityTests
{
    [Test]
    public async Task Request_WithNoTenantIdClaim_ShouldBeForbidden()
    {
        // Arrange: Register a real user so their sub resolves in the OnTokenValidated DB check.
        // Then re-sign a new token using all the real claims EXCEPT tenant_id.
        // This ensures JWT Bearer authentication succeeds and TenantSecurityMiddleware sees an
        // authenticated user whose tenant_id claim is absent — triggering 403 Forbidden.
        var (_, _, loginResponse, tenantId) = await AuthenticationHelpers.RegisterAndLoginUserAsync();

        var signingKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("your-secret-key-must-be-at-least-32-characters-long-for-hs256"));

        var handler = new JwtSecurityTokenHandler();
        var realToken = handler.ReadJwtToken(loginResponse.AccessToken);
        var claimsWithoutTenantId = realToken.Claims
            .Where(c => c.Type != "tenant_id")
            .ToList();

        var forgedToken = new JwtSecurityToken(
            issuer: "BookStore.ApiService",
            audience: "BookStore.Web",
            claims: claimsWithoutTenantId,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var tokenWithoutTenantClaim = handler.WriteToken(forgedToken);

        var client = RestService.For<IShoppingCartClient>(
            HttpClientHelpers.GetAuthenticatedClient(tokenWithoutTenantClaim, tenantId));

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetShoppingCartAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Request_Anonymous_WithTenantHeader_ShouldReturnProblemDetailsFormat()
    {
        // The TenantSecurityMiddleware must return RFC 7807 ProblemDetails (application/problem+json),
        // not a plain JSON error object, so clients get a consistent error contract.
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        var otherTenant = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(otherTenant);

        using var httpClient = GlobalHooks.App.CreateHttpClient("apiservice");
        httpClient.DefaultRequestHeaders.Add("X-Tenant-ID", otherTenant);

        // /api/cart does not have AllowAnonymousTenantAttribute, so the middleware blocks anonymous access
        var response = await httpClient.GetAsync("/api/cart");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        _ = await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");
    }
}
