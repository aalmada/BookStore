using System.Diagnostics;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;

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

        // Store in HttpContext.Items for other middlewares (Logging, Wolverine)
        context.Items["CorrelationId"] = correlationId;
        context.Items["CausationId"] = causationId;

        // Ensure Activity (if present) carries the correlation ID as a tag
        if (Activity.Current != null)
        {
            _ = Activity.Current.SetTag("correlation_id", correlationId);
            _ = Activity.Current.SetTag("causation_id", causationId);
        }

        // Capture technical metadata (using GUID ID to avoid PII)
        var userId = context.User?.GetUserId().ToString();
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();

        // Set technical headers on Marten session
        if (!string.IsNullOrEmpty(userId))
        {
            session.SetHeader("user-id", userId);
        }

        if (!string.IsNullOrEmpty(remoteIp))
        {
            session.SetHeader("remote-ip", remoteIp);
        }

        if (!string.IsNullOrEmpty(userAgent))
        {
            session.SetHeader("user-agent", userAgent);
        }

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            return Task.CompletedTask;
        });

        // Log the Marten metadata setup
        var hasHeader = context.Request.Headers.ContainsKey("X-Correlation-ID");
        _logger.LogInformation("[MARTEN-METADATA] Request: {Method} {Path}, X-Correlation-ID: {HasHeader}, CorelationId: {CorrelationId}, RemoteIp: {RemoteIp}",
            context.Request.Method, context.Request.Path, hasHeader, correlationId, remoteIp);

        Log.Infrastructure.MartenMetadataSet(_logger, correlationId, causationId, userId ?? "anonymous");

        await _next(context);
    }
}

public static class MartenMetadataMiddlewareExtensions
{
    public static IApplicationBuilder UseMartenMetadata(this IApplicationBuilder builder) => builder.UseMiddleware<MartenMetadataMiddleware>();
}
