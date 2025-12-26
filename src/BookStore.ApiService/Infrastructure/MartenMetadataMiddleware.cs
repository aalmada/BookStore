using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Middleware to set Marten correlation and causation IDs from HTTP headers
/// </summary>
public class MartenMetadataMiddleware
{
    readonly RequestDelegate _next;
    readonly ILogger<MartenMetadataMiddleware> _logger;

    public MartenMetadataMiddleware(RequestDelegate next, ILogger<MartenMetadataMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, Marten.IDocumentSession session)
    {
        // Get correlation ID from header or generate new one
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Activity.Current?.RootId
            ?? Guid.CreateVersion7().ToString();

        // Get causation ID from header (usually the previous event/command ID)
        var causationId = context.Request.Headers["X-Causation-ID"].FirstOrDefault()
            ?? Activity.Current?.ParentId
            ?? correlationId;

        // Set on Marten session - these will be automatically stored with events
        session.CorrelationId = correlationId;
        session.CausationId = causationId;

        // Optionally set custom headers
        var userId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
        {
            session.SetHeader("user-id", userId);
        }

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            return Task.CompletedTask;
        });

        // Create log scope with correlation and causation IDs for all logs in this request
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["CausationId"] = causationId
        }))
        {
            await _next(context);
        }
    }
}

public static class MartenMetadataMiddlewareExtensions
{
    public static IApplicationBuilder UseMartenMetadata(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MartenMetadataMiddleware>();
    }
}
