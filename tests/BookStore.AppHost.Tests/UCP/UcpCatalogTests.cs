using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using JasperFx;

namespace BookStore.AppHost.Tests;

public class UcpCatalogTests
{
    const string UcpAgent = "UCP-Agent";
    const string UcpAgentHeaderValue = "profile=\"https://test-agent.example.com/.well-known/ucp\"";
    const string CatalogBase = "/api/ucp/catalog/items";

    static HttpClient CreateCatalogClient()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        return client;
    }

    static async Task<Guid> CreateBookAndGetIdAsync(string? title = null)
    {
        var adminBooksClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var request = FakeDataGenerators.GenerateFakeBookRequest();

        if (!string.IsNullOrWhiteSpace(title))
        {
            request.Title = title;
        }

        var created = await BookHelpers.CreateBookAsync(adminBooksClient, request);
        return created.Id;
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task SearchCatalog_WithoutUcpAgentHeader_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        using var client = CreateCatalogClient();
        using var response = await client.GetAsync(CatalogBase);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task SearchCatalog_WithUcpAgentHeader_ShouldReturnItems()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var uniqueTitle = $"UCP Catalog {Guid.CreateVersion7():N}";
        _ = await CreateBookAndGetIdAsync(uniqueTitle);

        using var client = CreateCatalogClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        using var response = await client.GetAsync($"{CatalogBase}?q={Uri.EscapeDataString("UCP Catalog")}&limit=10");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(json.TryGetProperty("items", out var items)).IsTrue();
        _ = await Assert.That(items.GetArrayLength()).IsGreaterThan(0);
        _ = await Assert.That(json.GetProperty("total_count").GetInt32()).IsGreaterThan(0);
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task GetCatalogItem_WithUcpAgentHeader_ShouldReturnItem()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();

        using var client = CreateCatalogClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        using var response = await client.GetAsync($"{CatalogBase}/{bookId}");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(item.GetProperty("id").GetString()).IsEqualTo(bookId.ToString());
        _ = await Assert.That(item.GetProperty("title").GetString()).IsNotNull();
        _ = await Assert.That(item.GetProperty("price").GetProperty("amount").GetInt64()).IsGreaterThanOrEqualTo(0);
    }
}
