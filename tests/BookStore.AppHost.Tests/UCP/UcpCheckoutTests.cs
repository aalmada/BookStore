using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using JasperFx;

namespace BookStore.AppHost.Tests;

public class UcpCheckoutTests
{
    const string UcpAgentHeaderValue = "profile=\"https://test-agent.example.com/.well-known/ucp\"";
    const string UcpAgent = "UCP-Agent";
    const string CheckoutBase = "/api/ucp/checkout-sessions";

    static HttpClient CreateCheckoutClient()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        return client;
    }

    static async Task<Guid> CreateBookAndGetIdAsync()
    {
        var adminBooksClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var book = await BookHelpers.CreateBookAsync(adminBooksClient, FakeDataGenerators.GenerateFakeBookRequest());
        return book.Id;
    }

    // ------------------------------------------------------------------
    // Missing UCP-Agent header validation
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostCheckout_WithoutUcpAgentHeader_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();

        var body = new { currency = "GBP", line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } } };
        using var response = await client.PostAsJsonAsync(CheckoutBase, body);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task GetCheckout_WithoutUcpAgentHeader_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        using var client = CreateCheckoutClient();
        using var response = await client.GetAsync($"{CheckoutBase}/{Guid.CreateVersion7()}");

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // Create — validation
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostCheckout_WithEmptyLineItems_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        var body = new { currency = "GBP", line_items = Array.Empty<object>() };
        using var response = await client.PostAsJsonAsync(CheckoutBase, body);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostCheckout_WithInvalidBookId_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        var nonExistentId = Guid.CreateVersion7().ToString();
        var body = new { currency = "GBP", line_items = new[] { new { id = "li_1", item = new { id = nonExistentId }, quantity = 1 } } };
        using var response = await client.PostAsJsonAsync(CheckoutBase, body);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // Create — happy path
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostCheckout_WithValidBook_ShouldCreateSessionAndReturn201()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        var body = BuildCreateBody(bookId);
        using var response = await client.PostAsJsonAsync(CheckoutBase, body);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("incomplete");
        _ = await Assert.That(json.GetProperty("id").GetString()).IsNotNull();
        _ = await Assert.That(json.GetProperty("currency").GetString()).IsEqualTo("GBP");
        _ = await Assert.That(json.GetProperty("line_items").GetArrayLength()).IsGreaterThan(0);
    }

    // ------------------------------------------------------------------
    // GET
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task GetCheckout_AfterCreate_ShouldReturnSession()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create
        var body = BuildCreateBody(bookId);
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, body);
        _ = await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        // Get
        using var getResponse = await client.GetAsync($"{CheckoutBase}/{sessionId}");
        _ = await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var session = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(session.GetProperty("id").GetString()).IsEqualTo(sessionId);
        _ = await Assert.That(session.GetProperty("status").GetString()).IsEqualTo("incomplete");
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task GetCheckout_NonExistentSession_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        using var response = await client.GetAsync($"{CheckoutBase}/{Guid.CreateVersion7()}");
        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // Update (PUT)
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PutCheckout_WithBuyerEmail_ShouldSetStatusReadyForComplete()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, BuildCreateBody(bookId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        // Update with buyer
        var updateBody = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            buyer = new { email = "buyer@example.com", first_name = "Alice", last_name = "Smith" }
        };

        using var putResponse = await client.PutAsJsonAsync($"{CheckoutBase}/{sessionId}", updateBody);
        _ = await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(updated.GetProperty("status").GetString()).IsEqualTo("ready_for_complete");
    }

    // ------------------------------------------------------------------
    // Complete
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostComplete_AfterBuyerUpdate_ShouldCompleteSessionAndReturnOrderId()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, BuildCreateBody(bookId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        // Update with buyer
        var updateBody = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            buyer = new { email = "buyer@example.com", first_name = "Alice", last_name = "Smith" }
        };
        using var putResponse = await client.PutAsJsonAsync($"{CheckoutBase}/{sessionId}", updateBody);
        _ = await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Complete
        using var completeResponse = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/complete", new { });
        _ = await Assert.That(completeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var completed = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(completed.GetProperty("status").GetString()).IsEqualTo("completed");
        _ = await Assert.That(completed.TryGetProperty("order", out var order)).IsTrue();
        _ = await Assert.That(order.GetProperty("id").GetString()).IsNotNull();
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostComplete_WithoutBuyerInfo_ShouldReturn200WithErrorMessage()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create (no buyer yet)
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, BuildCreateBody(bookId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        // Try to complete — session status is "incomplete", not "ready_for_complete"
        using var completeResponse = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/complete", new { });
        _ = await Assert.That(completeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var result = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        // UCP spec returns 200 with error messages in the messages array
        _ = await Assert.That(result.GetProperty("messages").GetArrayLength()).IsGreaterThan(0);
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostComplete_OnAlreadyCompletedSession_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create → update with buyer → complete
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, BuildCreateBody(bookId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        var updateBody = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            buyer = new { email = "buyer@example.com", first_name = "Alice", last_name = "Smith" }
        };
        using var putResponse = await client.PutAsJsonAsync($"{CheckoutBase}/{sessionId}", updateBody);
        _ = await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var firstComplete = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/complete", new { });
        _ = await Assert.That(firstComplete.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Try to complete again
        using var secondComplete = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/complete", new { });
        _ = await Assert.That(secondComplete.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // Cancel
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostCancel_OnIncompleteSession_ShouldReturn200WithCancelledStatus()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, BuildCreateBody(bookId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        // Cancel
        using var cancelResponse = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/cancel", new { });
        _ = await Assert.That(cancelResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(cancelled.GetProperty("status").GetString()).IsEqualTo("cancelled");
    }

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    public async Task PostCancel_OnAlreadyCancelledSession_ShouldReturn400()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateCheckoutClient();
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);

        // Create
        using var createResponse = await client.PostAsJsonAsync(CheckoutBase, BuildCreateBody(bookId));
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = created.GetProperty("id").GetString()!;

        // Cancel once
        using var firstCancel = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/cancel", new { });
        _ = await Assert.That(firstCancel.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // Cancel again
        using var secondCancel = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/cancel", new { });
        _ = await Assert.That(secondCancel.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static object BuildCreateBody(Guid bookId, string currency = "GBP", int quantity = 1) => new
    {
        line_items = new[]
        {
            new { id = "li_1", item = new { id = bookId.ToString() }, quantity }
        },
        context = new { currency }
    };
}
