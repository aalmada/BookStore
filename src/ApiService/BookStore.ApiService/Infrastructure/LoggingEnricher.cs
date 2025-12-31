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
        // Get correlation ID (already set by MartenMetadataMiddleware)
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Activity.Current?.RootId
            ?? Guid.CreateVersion7().ToString();

        // Get trace and span IDs from OpenTelemetry Activity
        var traceId = Activity.Current?.TraceId.ToString();
        var spanId = Activity.Current?.SpanId.ToString();

        // Get user information (excluding email for privacy)
        var userId = context.User?.Identity?.Name ?? "anonymous";

        // Get request information
        var requestPath = context.Request.Path.Value;
        var requestMethod = context.Request.Method;
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        // Log the request start with enriched metadata
        Log.Infrastructure.RequestStarted(
            _logger,
            requestMethod,
            requestPath ?? "/",
            remoteIp ?? "unknown");

        await _next(context);
    }
}

public static class LoggingEnricherMiddlewareExtensions
{
    public static IApplicationBuilder UseLoggingEnricher(this IApplicationBuilder builder) => builder.UseMiddleware<LoggingEnricherMiddleware>();
}
