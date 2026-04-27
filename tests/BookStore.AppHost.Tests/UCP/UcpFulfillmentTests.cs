using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.AppHost.Tests.Helpers;
using BookStore.Client;
using JasperFx;

namespace BookStore.AppHost.Tests;

public class UcpFulfillmentTests
{
    const string UcpAgent = "UCP-Agent";
    const string UcpAgentHeaderValue = "profile=\"https://test-agent.example.com/.well-known/ucp\"";
    const string CheckoutBase = "/api/ucp/checkout-sessions";

    static HttpClient CreateClient()
    {
        var app = GlobalHooks.App!;
        var client = app.CreateHttpClient("apiservice");
        client.DefaultRequestHeaders.Add("X-Tenant-ID", StorageConstants.DefaultTenantId);
        client.DefaultRequestHeaders.Add(UcpAgent, UcpAgentHeaderValue);
        return client;
    }

    static async Task<Guid> CreateBookAndGetIdAsync()
    {
        var adminBooksClient = await HttpClientHelpers.GetAuthenticatedClientAsync<IBooksClient>();
        var book = await BookHelpers.CreateBookAsync(adminBooksClient, FakeDataGenerators.GenerateFakeBookRequest());
        return book.Id;
    }

    static async Task<string> CreateCheckoutSessionAsync(HttpClient client, Guid bookId)
    {
        var body = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            context = new { currency = "GBP" }
        };
        using var response = await client.PostAsJsonAsync(CheckoutBase, body);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    // ------------------------------------------------------------------
    // Fulfillment: providing address returns available shipping options
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    [Category("Fulfillment")]
    public async Task PutCheckout_WithShippingAddress_ShouldReturnFulfillmentOptions()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateClient();
        var sessionId = await CreateCheckoutSessionAsync(client, bookId);

        var updateBody = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            buyer = new { email = "buyer@example.com", first_name = "Alice", last_name = "Smith" },
            fulfillment = new
            {
                methods = new[]
                {
                    new
                    {
                        type = "shipping",
                        destinations = new[]
                        {
                            new
                            {
                                street_address = "123 Main St",
                                address_locality = "London",
                                address_region = "England",
                                postal_code = "SW1A 1AA",
                                address_country = "GB"
                            }
                        }
                    }
                }
            }
        };

        using var response = await client.PutAsJsonAsync($"{CheckoutBase}/{sessionId}", updateBody);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Status stays incomplete until a fulfillment option is selected
        _ = await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("incomplete");

        // Fulfillment options should be returned
        _ = await Assert.That(json.TryGetProperty("fulfillment", out var fulfillment)).IsTrue();
        _ = await Assert.That(fulfillment.TryGetProperty("methods", out var methods)).IsTrue();
        _ = await Assert.That(methods.GetArrayLength()).IsGreaterThan(0);

        var groups = methods[0].GetProperty("groups");
        _ = await Assert.That(groups.GetArrayLength()).IsGreaterThan(0);
        var options = groups[0].GetProperty("options");
        _ = await Assert.That(options.GetArrayLength()).IsEqualTo(3); // standard, express, overnight
    }

    // ------------------------------------------------------------------
    // Fulfillment: selecting an option sets status to ready_for_complete
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    [Category("Fulfillment")]
    public async Task PutCheckout_WithSelectedFulfillmentOption_ShouldSetReadyForComplete()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateClient();
        var sessionId = await CreateCheckoutSessionAsync(client, bookId);

        var updateBody = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            buyer = new { email = "buyer@example.com", first_name = "Alice", last_name = "Smith" },
            fulfillment = new
            {
                methods = new[]
                {
                    new
                    {
                        id = "ship_1",
                        type = "shipping",
                        destinations = new[]
                        {
                            new
                            {
                                id = "dest_1",
                                street_address = "123 Main St",
                                address_locality = "London",
                                address_region = "England",
                                postal_code = "SW1A 1AA",
                                address_country = "GB"
                            }
                        },
                        selected_destination_id = "dest_1",
                        groups = new[] { new { id = "pkg_1", selected_option_id = "standard" } }
                    }
                }
            }
        };

        using var response = await client.PutAsJsonAsync($"{CheckoutBase}/{sessionId}", updateBody);

        _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("ready_for_complete");

        // Shipping should appear in totals
        var totals = json.GetProperty("totals");
        var totalTypes = Enumerable.Range(0, totals.GetArrayLength())
            .Select(i => totals[i].GetProperty("type").GetString())
            .ToList();
        _ = await Assert.That(totalTypes).Contains("fulfillment");
        _ = await Assert.That(totalTypes).Contains("total");
    }

    // ------------------------------------------------------------------
    // Fulfillment: complete with shipping cost included in total
    // ------------------------------------------------------------------

    [Test]
    [Category("Integration")]
    [Category("UCP")]
    [Category("Fulfillment")]
    public async Task PostComplete_WithFulfillmentSelected_ShouldIncludeShippingInTotal()
    {
        var notificationService = GlobalHooks.NotificationService!;
        _ = await notificationService.WaitForResourceHealthyAsync("apiservice", CancellationToken.None)
            .WaitAsync(TestConstants.DefaultTimeout);

        var bookId = await CreateBookAndGetIdAsync();
        using var client = CreateClient();
        var sessionId = await CreateCheckoutSessionAsync(client, bookId);

        // Update with buyer + fulfillment option selected (express)
        var updateBody = new
        {
            line_items = new[] { new { id = "li_1", item = new { id = bookId.ToString() }, quantity = 1 } },
            buyer = new { email = "buyer@example.com", first_name = "Alice", last_name = "Smith" },
            fulfillment = new
            {
                methods = new[]
                {
                    new
                    {
                        id = "ship_1",
                        type = "shipping",
                        destinations = new[]
                        {
                            new
                            {
                                id = "dest_1",
                                street_address = "123 Main St",
                                address_locality = "London",
                                address_region = "England",
                                postal_code = "SW1A 1AA",
                                address_country = "GB"
                            }
                        },
                        selected_destination_id = "dest_1",
                        groups = new[] { new { id = "pkg_1", selected_option_id = "express" } }
                    }
                }
            }
        };
        using var putResponse = await client.PutAsJsonAsync($"{CheckoutBase}/{sessionId}", updateBody);
        _ = await Assert.That(putResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var updated = await putResponse.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(updated.GetProperty("status").GetString()).IsEqualTo("ready_for_complete");

        // Complete
        using var completeResponse = await client.PostAsJsonAsync($"{CheckoutBase}/{sessionId}/complete", new { });
        _ = await Assert.That(completeResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var completed = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        _ = await Assert.That(completed.GetProperty("status").GetString()).IsEqualTo("completed");

        // Verify totals include fulfillment
        var totals = completed.GetProperty("totals");
        var fulfillmentTotal = Enumerable.Range(0, totals.GetArrayLength())
            .Select(i => totals[i])
            .FirstOrDefault(t => t.GetProperty("type").GetString() == "fulfillment");
        _ = await Assert.That(fulfillmentTotal.ValueKind).IsNotEqualTo(JsonValueKind.Undefined);
        _ = await Assert.That(fulfillmentTotal.GetProperty("amount").GetInt64()).IsEqualTo(999L); // express = £9.99
    }
}
