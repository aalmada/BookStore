using System.Diagnostics;

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

        // Create a log scope with all metadata (excluding PII like email)
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId ?? "none",
            ["SpanId"] = spanId ?? "none",
            ["UserId"] = userId,
            ["RequestPath"] = requestPath ?? "/",
            ["RequestMethod"] = requestMethod,
            ["UserAgent"] = userAgent ?? "unknown",
            ["RemoteIp"] = remoteIp ?? "unknown",
            ["MachineName"] = Environment.MachineName,
            ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        }))
        {
            await _next(context);
        }
    }
}

public static class LoggingEnricherMiddlewareExtensions
{
    public static IApplicationBuilder UseLoggingEnricher(this IApplicationBuilder builder) => builder.UseMiddleware<LoggingEnricherMiddleware>();
}
