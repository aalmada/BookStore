using System.Diagnostics;
using BookStore.ApiService.Infrastructure.Logging;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Middleware to enrich logs with correlation ID, trace information, and request metadata
/// </summary>
public class LoggingEnricherMiddleware
{
    readonly RequestDelegate _next;
    readonly ILogger<LoggingEnricherMiddleware> _logger;

    public LoggingEnricherMiddleware(RequestDelegate next, ILogger<LoggingEnricherMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Cache Activity reference to avoid multiple property accesses
        var activity = Activity.Current;

        // Get headers once to avoid multiple lookups
        var headers = context.Request.Headers;

        // Get correlation ID (already set by MartenMetadataMiddleware)
        var correlationId = context.Items["CorrelationId"] as string
            ?? (headers.TryGetValue("X-Correlation-ID", out var correlationHeader) ? correlationHeader.ToString() : null)
            ?? activity?.RootId
            ?? Guid.CreateVersion7().ToString();

        // Get causation ID
        var causationId = context.Items["CausationId"] as string
            ?? (headers.TryGetValue("X-Causation-ID", out var causationHeader) ? causationHeader.ToString() : null)
            ?? activity?.ParentId;

        // Get trace and span IDs from OpenTelemetry Activity (avoid ToString() if null)
        var traceId = activity?.TraceId.ToString();
        var spanId = activity?.SpanId.ToString();

        // Get user information (excluding email for privacy)
        var userId = context.User.Identity?.Name ?? "anonymous";

        // Get request information (cache frequently accessed properties)
        var request = context.Request;
        var requestPath = request.Path.Value;
        var requestMethod = request.Method;
        var userAgent = headers.TryGetValue("User-Agent", out var userAgentHeader)
            ? userAgentHeader.ToString()
            : null;
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        // Create a log scope with all metadata - this makes these properties available
        // in all structured logs within this request
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["CausationId"] = causationId,
            ["TraceId"] = traceId,
            ["SpanId"] = spanId,
            ["UserId"] = userId,
            ["RequestPath"] = requestPath,
            ["RequestMethod"] = requestMethod,
            ["RemoteIp"] = remoteIp,
            ["UserAgent"] = userAgent
        }))
        {
            // Log the request start with enriched metadata
            Log.Infrastructure.RequestStarted(
                _logger,
                requestMethod,
                requestPath ?? "/",
                remoteIp ?? "unknown");

            await _next(context);
        }
    }
}

public static class LoggingEnricherMiddlewareExtensions
{
    public static IApplicationBuilder UseLoggingEnricher(this IApplicationBuilder builder) => builder.UseMiddleware<LoggingEnricherMiddleware>();
}
