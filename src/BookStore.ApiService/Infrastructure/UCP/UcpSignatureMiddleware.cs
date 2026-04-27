using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Infrastructure.UCP;

public sealed class UcpSignatureMiddleware(RequestDelegate next)
{
    const string CompleteSuffix = "/complete";

    public async Task InvokeAsync(HttpContext context, UcpResponseSigner signer, IOptions<UcpKeyOptions> options)
    {
        var keyOptions = options.Value;
        if (!keyOptions.RequireSignatures || !IsCompleteCheckoutRequest(context.Request))
        {
            await next(context);
            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await next(context);

        buffer.Position = 0;
        var bodyBytes = buffer.ToArray();

        if (context.Response.StatusCode == StatusCodes.Status200OK && bodyBytes.Length > 0)
        {
            var contentType = context.Response.ContentType ?? "application/json";
            var signed = signer.TrySignCompleteCheckoutResponse(context.Response.Headers, context.Response.StatusCode, contentType, bodyBytes);
            if (!signed)
            {
                _ = context.Response.Headers.Remove("Content-Digest");
                _ = context.Response.Headers.Remove("Signature-Input");
                _ = context.Response.Headers.Remove("Signature");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "UCP response signing failed",
                    Detail = "The checkout completion response could not be signed."
                };
                problem.Extensions["code"] = "ucp.signature.signing_failed";

                bodyBytes = JsonSerializer.SerializeToUtf8Bytes(problem);
            }
        }

        context.Response.ContentLength = bodyBytes.Length;
        context.Response.Body = originalBody;
        await context.Response.Body.WriteAsync(bodyBytes);
    }

    static bool IsCompleteCheckoutRequest(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        var path = request.Path.Value;
        return path is not null
            && path.StartsWith("/api/ucp/checkout-sessions/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith(CompleteSuffix, StringComparison.OrdinalIgnoreCase);
    }
}
