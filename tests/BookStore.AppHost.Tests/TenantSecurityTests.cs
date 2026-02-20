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
        // Arrange: Forge a valid JWT that is deliberately missing the tenant_id claim.
        // The server's JWT signature validation passes (correct key, issuer, audience),
        // but TenantSecurityMiddleware rejects it because the tenant_id claim is absent.
        var signingKey = new SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes("your-secret-key-must-be-at-least-32-characters-long-for-hs256"));

        var tokenDescriptor = new JwtSecurityToken(
            issuer: "BookStore.ApiService",
            audience: "BookStore.Web",
            claims:
            [
                new Claim("sub", Guid.CreateVersion7().ToString()),
                new Claim("email", FakeDataGenerators.GenerateFakeEmail()),
                // Deliberately omit the "tenant_id" claim
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var tokenWithoutTenantClaim = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

        var client = RestService.For<IShoppingCartClient>(
            HttpClientHelpers.GetAuthenticatedClient(tokenWithoutTenantClaim, StorageConstants.DefaultTenantId));

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
