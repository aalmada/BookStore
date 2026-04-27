using System.Text;
using System.Text.Json;
using BookStore.ApiService.Infrastructure.UCP;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.UnitTests.Infrastructure.UCP;

public class UcpSignatureMiddlewareTests
{
    [Test]
    [Category("Unit")]
    public async Task InvokeAsync_WhenSignaturesRequiredAndSigningFails_ShouldReturnProblemDetails()
    {
        var middleware = new UcpSignatureMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"status\":\"completed\"}");
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/ucp/checkout-sessions/00000000-0000-0000-0000-000000000001/complete";
        await using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var options = Options.Create(new UcpKeyOptions
        {
            RequireSignatures = true,
            SigningKeyId = "merchant-key-2026",
            SigningPrivateKeyBase64 = "not-base64"
        });
        var signer = new UcpResponseSigner(options);

        await middleware.InvokeAsync(context, signer, options);

        responseStream.Position = 0;
        var body = await new StreamReader(responseStream, Encoding.UTF8).ReadToEndAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        _ = await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status500InternalServerError);
        _ = await Assert.That(context.Response.ContentType).IsEqualTo("application/problem+json");
        _ = await Assert.That(json.GetProperty("code").GetString()).IsEqualTo("ucp.signature.signing_failed");
    }

    [Test]
    [Category("Unit")]
    public async Task InvokeAsync_WhenRequestIsNotCompleteCheckout_ShouldBypassSignatureHandling()
    {
        var middleware = new UcpSignatureMiddleware(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"status\":\"incomplete\"}");
        });

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/ucp/checkout-sessions";
        await using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var options = Options.Create(new UcpKeyOptions
        {
            RequireSignatures = true,
            SigningPrivateKeyBase64 = "not-base64"
        });
        var signer = new UcpResponseSigner(options);

        await middleware.InvokeAsync(context, signer, options);

        responseStream.Position = 0;
        var body = await new StreamReader(responseStream, Encoding.UTF8).ReadToEndAsync();

        _ = await Assert.That(context.Response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        _ = await Assert.That(body).IsEqualTo("{\"status\":\"incomplete\"}");
        _ = await Assert.That(context.Response.Headers.ContainsKey("Signature")).IsFalse();
    }
}
