using System.Net;
using System.Net.Http.Json;
using BookStore.AppHost.Tests.Helpers;

namespace BookStore.AppHost.Tests;

public class UcpProfileTests
{
    [Test]
    public async Task GetUcpProfile_ShouldReturnOk()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        // No tenant header needed — endpoint is anonymous
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var response = await client.GetAsync("/.well-known/ucp");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetUcpProfile_ShouldReturnJsonContentType()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var response = await client.GetAsync("/.well-known/ucp");

        _ = await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
    }

    [Test]
    public async Task GetUcpProfile_ShouldContainUcpVersion()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/ucp");

        _ = await Assert.That(profile.GetProperty("ucp").GetProperty("version").GetString())
            .IsEqualTo("2026-04-08");
    }

    [Test]
    public async Task GetUcpProfile_ShouldContainCheckoutCapability()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/ucp");

        var capabilities = profile.GetProperty("capabilities");
        _ = await Assert.That(capabilities.TryGetProperty("dev.ucp.shopping.checkout", out _)).IsTrue();
    }

    [Test]
    public async Task GetUcpProfile_ShouldContainCatalogCapability()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/ucp");

        var capabilities = profile.GetProperty("capabilities");
        _ = await Assert.That(capabilities.TryGetProperty("dev.ucp.shopping.catalog", out _)).IsTrue();
    }

    [Test]
    public async Task GetUcpProfile_ShouldContainCheckoutRestService()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/ucp");

        var services = profile.GetProperty("services");
        _ = await Assert.That(services.TryGetProperty("dev.ucp.shopping.checkout", out var checkoutServices)).IsTrue();

        var firstService = checkoutServices.EnumerateArray().First();
        _ = await Assert.That(firstService.GetProperty("transport").GetString()).IsEqualTo("rest");
        _ = await Assert.That(firstService.GetProperty("id").GetString()).IsEqualTo("checkout_rest");
    }

    [Test]
    public async Task GetUcpProfile_ShouldContainSimulatedPaymentHandler()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/ucp");

        var paymentHandlers = profile.GetProperty("payment_handlers");
        _ = await Assert.That(paymentHandlers.TryGetProperty("dev.bookstore.payment.simulated", out _)).IsTrue();
    }

    [Test]
    public async Task GetUcpProfile_ShouldContainCheckoutMcpService()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");

        var profile = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/.well-known/ucp");

        var services = profile.GetProperty("services");
        _ = await Assert.That(services.TryGetProperty("dev.ucp.shopping.checkout", out var checkoutServices)).IsTrue();

        var hasMcp = checkoutServices.EnumerateArray().Any(s =>
            s.TryGetProperty("transport", out var transport)
            && transport.GetString() == "mcp");

        _ = await Assert.That(hasMcp).IsTrue();
    }
}
