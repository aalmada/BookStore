using System.Net;
using BookStore.Client;
using BookStore.Shared.Models;
using Refit;

namespace BookStore.AppHost.Tests;

public class TenantInfoTests
{
    [Test]
    public async Task GetTenantInfo_ReturnsCorrectName()
    {
        var client = RestService.For<ITenantsClient>(TestHelpers.GetUnauthenticatedClient());

        // 1. Get info for "acme"
        var acmeInfo = await client.GetTenantAsync("acme");
        _ = await Assert.That(acmeInfo).IsNotNull();
        _ = await Assert.That(acmeInfo.Id).IsEqualTo("acme");
        _ = await Assert.That(acmeInfo.Name).IsEqualTo("Acme Corp");

        // 2. Get info for "contoso"
        var contosoInfo = await client.GetTenantAsync("contoso");
        _ = await Assert.That(contosoInfo).IsNotNull();
        _ = await Assert.That(contosoInfo.Id).IsEqualTo("contoso");
        _ = await Assert.That(contosoInfo.Name).IsEqualTo("Contoso Ltd");
    }

    [Test]
    public async Task GetTenantInfo_InvalidId_ReturnsNotFound()
    {
        var client = RestService.For<ITenantsClient>(TestHelpers.GetUnauthenticatedClient());

        var exception = await Assert.That(async () => await client.GetTenantAsync("invalid-tenant-id"))
            .Throws<ApiException>();
        _ = await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
