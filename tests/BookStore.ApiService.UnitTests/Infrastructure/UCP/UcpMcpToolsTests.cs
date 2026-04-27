using System.Net;
using System.Text;
using BookStore.ApiService.Infrastructure.UCP;

namespace BookStore.ApiService.UnitTests.Infrastructure.UCP;

public class UcpMcpToolsTests
{
    [Test]
    [Category("Unit")]
    public async Task SearchCatalog_ShouldCallCatalogItemsEndpointWithQueryParameters()
    {
        var handler = new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"total\":0,\"has_more\":false}", Encoding.UTF8, "application/json")
            });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://merchant.example")
        };

        var clientFactory = Substitute.For<IHttpClientFactory>();
        _ = clientFactory.CreateClient("ucp-mcp").Returns(client);
        var tools = new UcpMcpTools();

        var result = await tools.search_catalog("book", "GBP", 10, 5, clientFactory, CancellationToken.None);

        _ = await Assert.That(handler.LastRequest).IsNotNull();
        _ = await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Get);
        _ = await Assert.That(handler.LastRequest.RequestUri!.PathAndQuery)
            .IsEqualTo("/api/ucp/catalog/items?q=book&currency=GBP&limit=10&offset=5");
        _ = await Assert.That(result.TryGetProperty("items", out _)).IsTrue();
    }

    [Test]
    [Category("Unit")]
    public async Task CancelCheckout_ShouldCallCancelEndpoint()
    {
        var handler = new CapturingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"cancelled\"}", Encoding.UTF8, "application/json")
            });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://merchant.example")
        };

        var clientFactory = Substitute.For<IHttpClientFactory>();
        _ = clientFactory.CreateClient("ucp-mcp").Returns(client);
        var tools = new UcpMcpTools();

        var result = await tools.cancel_checkout("abc-123", clientFactory, CancellationToken.None);

        _ = await Assert.That(handler.LastRequest).IsNotNull();
        _ = await Assert.That(handler.LastRequest!.Method).IsEqualTo(HttpMethod.Post);
        _ = await Assert.That(handler.LastRequest.RequestUri!.PathAndQuery)
            .IsEqualTo("/api/ucp/checkout-sessions/abc-123/cancel");
        _ = await Assert.That(result.GetProperty("status").GetString()).IsEqualTo("cancelled");
    }

    sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
