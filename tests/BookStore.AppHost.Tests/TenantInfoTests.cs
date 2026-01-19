using System.Net;
using System.Net.Http.Json;
using BookStore.Shared.Models;

namespace BookStore.AppHost.Tests;

public class TenantInfoTests
{
    [Test]
    public async Task GetTenantInfo_ReturnsCorrectName()
    {
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");

        // 1. Get info for "acme"
        var acmeResponse = await client.GetAsync("/api/tenants/acme");
        _ = await Assert.That(acmeResponse.IsSuccessStatusCode).IsTrue();

        var acmeInfo = await acmeResponse.Content.ReadFromJsonAsync<TenantInfoDto>();
        _ = await Assert.That(acmeInfo).IsNotNull();
        _ = await Assert.That(acmeInfo!.Id).IsEqualTo("acme");
        _ = await Assert.That(acmeInfo!.Name).IsEqualTo("Acme Corp");

        // 2. Get info for "contoso"
        var contosoResponse = await client.GetAsync("/api/tenants/contoso");
        _ = await Assert.That(contosoResponse.IsSuccessStatusCode).IsTrue();

        var contosoInfo = await contosoResponse.Content.ReadFromJsonAsync<TenantInfoDto>();
        _ = await Assert.That(contosoInfo).IsNotNull();
        _ = await Assert.That(contosoInfo!.Id).IsEqualTo("contoso");
        _ = await Assert.That(contosoInfo!.Name).IsEqualTo("Contoso Ltd");
    }

    [Test]
    public async Task GetTenantInfo_InvalidId_ReturnsNotFound()
    {
        var app = GlobalHooks.App!;
        using var client = app.CreateHttpClient("apiservice");

        var response = await client.GetAsync("/api/tenants/invalid-tenant-id");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
