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
    public async Task Request_Anonymous_WithTenantHeader_ShouldBeForbidden()
    {
        if (GlobalHooks.App == null)
        {
            throw new InvalidOperationException("App is not initialized");
        }

        // Anonymous request targeting any non-default tenant should be Forbidden
        var otherTenant = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(otherTenant);

        var client = RestService.For<IShoppingCartClient>(HttpClientHelpers.GetUnauthenticatedClient(otherTenant));

        // Act & Assert
        var exception = await Assert.That(async () => await client.GetShoppingCartAsync()).Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
