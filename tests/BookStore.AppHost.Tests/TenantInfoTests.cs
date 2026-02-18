using System.Net;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class TenantInfoTests
{
    [Test]
    public async Task GetTenantInfo_ReturnsCorrectInfo()
    {
        // Arrange: create a fresh tenant so we control both Id and Name
        var tenantId = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(tenantId);

        var client = RestService.For<ITenantsClient>(HttpClientHelpers.GetUnauthenticatedClient());

        // Act
        var info = await client.GetTenantAsync(tenantId);

        // Assert: the API echoes back the same ID and a non-empty name
        _ = await Assert.That(info).IsNotNull();
        _ = await Assert.That(info.Id).IsEqualTo(tenantId);
        _ = await Assert.That(info.Name).IsNotNullOrEmpty();
    }

    [Test]
    public async Task GetTenantInfo_InvalidId_ReturnsNotFound()
    {
        var client = RestService.For<ITenantsClient>(HttpClientHelpers.GetUnauthenticatedClient());

        var exception = await Assert.That(async () => await client.GetTenantAsync("invalid-tenant-id"))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
