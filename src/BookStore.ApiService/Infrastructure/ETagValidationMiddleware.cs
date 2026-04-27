using System;
using System.Linq;
using System.Threading.Tasks;
using BookStore.Shared.Commands;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Middleware to validate ETags for write operations
/// </summary>
public class ETagValidationMiddleware
{
    readonly RequestDelegate _next;

    public ETagValidationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;

        // Explicitly exclude endpoints that do not use ETag optimistic concurrency.
        // This covers RateBook (POST), Add/RemoveFavorites (POST/DELETE), cart, and UCP checkout sessions.
        if (path.Value!.EndsWith("/rating", StringComparison.OrdinalIgnoreCase) ||
            path.Value!.EndsWith("/favorites", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/cart") ||
            path.StartsWithSegments("/api/ucp/checkout-sessions"))
        {
            await _next(context);
            return;
        }

        // Only validate for write operations (PUT, DELETE, POST for specific cases)
        if (method == HttpMethods.Put || method == HttpMethods.Delete ||
           (method == HttpMethods.Post && IsUpdateOrDeleteAction(path)))
        {
            var ifMatch = context.Request.Headers["If-Match"].FirstOrDefault();

            if (string.IsNullOrEmpty(ifMatch))
            {
                context.Response.StatusCode = StatusCodes.Status428PreconditionRequired;
                var allHeaders = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}"));
                await context.Response.WriteAsJsonAsync(new
                {
                    Title = "Precondition Required",
                    Status = 428,
                    Detail = $"The If-Match header is required for {method} {path}. Headers found: {allHeaders}"
                });
                return;
            }

            // The ETag will be extracted from the header and placed into the Wolverine command if it implements IHaveETag
            // This middleware's job is mostly enforcement of PRESENCE.
            // Validation of MATCH happens in the handler/aggregate via Marten.
        }

        await _next(context);
    }

    static bool IsUpdateOrDeleteAction(PathString path)
    {
        // Exact matches or specific sub-paths to avoid over-enforcement
        if (path.StartsWithSegments("/api/admin/books", out var rest1))
        {
            var val = rest1.Value ?? string.Empty;
            return val.EndsWith("/restore", StringComparison.OrdinalIgnoreCase) ||
                   val.EndsWith("/sales", StringComparison.OrdinalIgnoreCase);
        }

        // /api/books rating/favorites handled by explicit exclusion in InvokeAsync

        if (path.StartsWithSegments("/api/admin/authors", out var rest3))
        {
            return (rest3.Value ?? string.Empty).EndsWith("/restore", StringComparison.OrdinalIgnoreCase);
        }

        if (path.StartsWithSegments("/api/admin/categories", out var rest4))
        {
            return (rest4.Value ?? string.Empty).EndsWith("/restore", StringComparison.OrdinalIgnoreCase);
        }

        if (path.StartsWithSegments("/api/admin/publishers", out var rest5))
        {
            return (rest5.Value ?? string.Empty).EndsWith("/restore", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

public static class ETagValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseETagValidation(this IApplicationBuilder builder) => builder.UseMiddleware<ETagValidationMiddleware>();
}
