using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using Refit;

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

        // Admin JWT carries the default tenant claim; send it with a different tenant header -> Forbidden
        var otherTenant = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(otherTenant);

        var validToken = GlobalHooks.AdminAccessToken!;
        var client = RestService.For<IShoppingCartClient>(HttpClientHelpers.GetAuthenticatedClient(validToken, otherTenant));

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
