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
        // Get correlation ID from header (StringValues implicitly converts to string, avoiding LINQ)
        string? correlationId = context.Request.Headers["X-Correlation-ID"];
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Activity.Current?.RootId ?? Guid.CreateVersion7().ToString();
        }

        // Get causation ID from header
        string? causationId = context.Request.Headers["X-Causation-ID"];
        if (string.IsNullOrEmpty(causationId))
        {
            causationId = Activity.Current?.ParentId ?? correlationId;
        }

        // Set on Marten session
        session.CorrelationId = correlationId;
        session.CausationId = causationId;

        // Store in HttpContext.Items
        context.Items["CorrelationId"] = correlationId;
        context.Items["CausationId"] = causationId;

        // Ensure Activity carries the correlation ID
        // Only access Activity.Current once
        var activity = Activity.Current;
        if (activity != null)
        {
            _ = activity.SetTag("correlation_id", correlationId);
            _ = activity.SetTag("causation_id", causationId);
        }

        // PII-safe metadata
        var user = context.User;
        // Avoid ToString() allocations if not needed for logging immediately
        // ClaimsPrincipalExtensions.GetUserId returns Guid.Empty if missing/invalid
        var userId = user?.GetUserId();
        if (userId == Guid.Empty)
        {
            userId = null;
        }

        var remoteIp = context.Connection.RemoteIpAddress;

        string? userAgent = context.Request.Headers["User-Agent"];

        // TenantId is usually a string in our system
        var tenantId = context.Items["TenantId"] as string;

        // Set headers on Marten session - Marten handles object values efficiently
        if (!string.IsNullOrEmpty(tenantId))
        {
            session.SetHeader("tenant-id", tenantId);
        }

        if (userId.HasValue)
        {
            session.SetHeader("user-id", userId.Value.ToString());
        }

        if (remoteIp != null)
        {
            session.SetHeader("remote-ip", remoteIp.ToString());
        }

        if (!string.IsNullOrEmpty(userAgent))
        {
            session.SetHeader("user-agent", userAgent);
        }

        // Use static lambda with state to avoid closure allocation
        context.Response.OnStarting(static state =>
        {
            var (ctx, cId) = ((HttpContext, string))state;
            ctx.Response.Headers["X-Correlation-ID"] = cId;
            return Task.CompletedTask;
        }, (context, correlationId));

        // Log using the optimized signature
        Log.Infrastructure.MartenMetadataApplied(_logger, context.Request.Method, context.Request.Path, correlationId!, causationId!, userId?.ToString() ?? "anonymous", remoteIp?.ToString());

        await _next(context);
    }
}

public static class MartenMetadataMiddlewareExtensions
{
    public static IApplicationBuilder UseMartenMetadata(this IApplicationBuilder builder) => builder.UseMiddleware<MartenMetadataMiddleware>();
}
