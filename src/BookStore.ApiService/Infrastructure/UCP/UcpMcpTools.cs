using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using BookStore.ApiService.Models.Ucp;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace BookStore.ApiService.Infrastructure.UCP;

[McpServerToolType]
public sealed class UcpMcpTools
{
    [McpServerTool, Description("Search catalog items.")]
    public async Task<JsonElement> search_catalog(
        [Description("Optional free-text search.")] string? q,
        [Description("Optional ISO currency code.")] string? currency,
        [Description("Max number of items to return.")] int? limit,
        [Description("Result offset.")] int? offset,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query["q"] = q;
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            query["currency"] = currency;
        }

        if (limit.HasValue)
        {
            query["limit"] = limit.Value.ToString();
        }

        if (offset.HasValue)
        {
            query["offset"] = offset.Value.ToString();
        }

        var path = QueryHelpers.AddQueryString("/api/ucp/catalog/items", query);
        return await SendGetAsync(httpClientFactory, path, cancellationToken);
    }

    [McpServerTool, Description("Create a checkout session.")]
    public async Task<JsonElement> create_checkout(
        UcpCreateCheckoutRequest request,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
        => await SendJsonAsync(httpClientFactory, HttpMethod.Post, "/api/ucp/checkout-sessions", request, cancellationToken);

    [McpServerTool, Description("Get checkout session details.")]
    public async Task<JsonElement> get_checkout(
        [Description("Checkout session id.")] string id,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
        => await SendGetAsync(httpClientFactory, $"/api/ucp/checkout-sessions/{id}", cancellationToken);

    [McpServerTool, Description("Update a checkout session using full replacement semantics.")]
    public async Task<JsonElement> update_checkout(
        [Description("Checkout session id.")] string id,
        UcpUpdateCheckoutRequest request,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
        => await SendJsonAsync(httpClientFactory, HttpMethod.Put, $"/api/ucp/checkout-sessions/{id}", request, cancellationToken);

    [McpServerTool, Description("Complete a checkout session.")]
    public async Task<JsonElement> complete_checkout(
        [Description("Checkout session id.")] string id,
        UcpCompleteCheckoutRequest? request,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
        => await SendJsonAsync(httpClientFactory, HttpMethod.Post, $"/api/ucp/checkout-sessions/{id}/complete", request ?? new UcpCompleteCheckoutRequest(null), cancellationToken);

    [McpServerTool, Description("Cancel a checkout session.")]
    public async Task<JsonElement> cancel_checkout(
        [Description("Checkout session id.")] string id,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
        => await SendJsonAsync(httpClientFactory, HttpMethod.Post, $"/api/ucp/checkout-sessions/{id}/cancel", new { }, cancellationToken);

    static async Task<JsonElement> SendGetAsync(IHttpClientFactory httpClientFactory, string path, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("ucp-mcp");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await client.SendAsync(request, cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    static async Task<JsonElement> SendJsonAsync(
        IHttpClientFactory httpClientFactory,
        HttpMethod method,
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("ucp-mcp");
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(payload)
        };

        using var response = await client.SendAsync(request, cancellationToken);
        return await ReadResponseAsync(response, cancellationToken);
    }

    static async Task<JsonElement> ReadResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (bytes.Length == 0)
        {
            return JsonSerializer.SerializeToElement(new { status = (int)response.StatusCode });
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(bytes);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new
            {
                status = (int)response.StatusCode,
                content = await response.Content.ReadAsStringAsync(cancellationToken)
            });
        }
    }
}
