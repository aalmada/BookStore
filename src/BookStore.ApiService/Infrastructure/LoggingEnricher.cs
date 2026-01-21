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

        // Get headers once
        var headers = context.Request.Headers;
        var items = context.Items;

        // Get correlation ID (prefer cached value from MartenMetadataMiddleware)
        // StringValues implicitly converts to string
        var correlationId = items["CorrelationId"] as string
            ?? (string?)headers["X-Correlation-ID"]
            ?? activity?.RootId
            ?? Guid.CreateVersion7().ToString();

        // Get causation ID
        var causationId = items["CausationId"] as string
            ?? (string?)headers["X-Causation-ID"]
            ?? activity?.ParentId;

        // Get trace and span IDs (deferred string conversion)
        object? traceId = activity?.TraceId;
        object? spanId = activity?.SpanId;

        // Get user information
        var userId = context.User.Identity?.Name ?? "anonymous";

        // Get request information
        var request = context.Request;
        var requestPath = request.Path.Value;
        var requestMethod = request.Method;
        string? userAgent = headers["User-Agent"];
        var remoteIp = context.Connection.RemoteIpAddress;

        // Create a log scope with all metadata - pre-size dictionary (10 items)
        using (_logger.BeginScope(new Dictionary<string, object?>(10)
        {
            ["CorrelationId"] = correlationId,
            ["CausationId"] = causationId,
            ["TraceId"] = traceId?.ToString(),
            ["SpanId"] = spanId?.ToString(),
            ["UserId"] = userId,
            ["TenantId"] = items["TenantId"] as string ?? "unknown",
            ["RequestPath"] = requestPath,
            ["RequestMethod"] = requestMethod,
            ["RemoteIp"] = remoteIp?.ToString(),
            ["UserAgent"] = userAgent
        }))
        {
            // Log the request start with enriched metadata
            Log.Infrastructure.RequestStarted(
                _logger,
                requestMethod,
                requestPath ?? "/",
                remoteIp?.ToString() ?? "unknown");

            await _next(context);
        }
    }
}

public static class LoggingEnricherMiddlewareExtensions
{
    public static IApplicationBuilder UseLoggingEnricher(this IApplicationBuilder builder) => builder.UseMiddleware<LoggingEnricherMiddleware>();
}
