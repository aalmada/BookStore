using System.Diagnostics;
using BookStore.ApiService.Infrastructure.Extensions;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Middleware to propagate Correlation and Causation IDs from Wolverine Message Context to Marten Session
/// This is necessary because Wolverine often creates a nested session/scope that doesn't inherit 
/// the IDs set on the outer HTTP-scoped session.
/// </summary>
public class WolverineCorrelationMiddleware
{
    public static void Before(IDocumentSession session, IMessageContext context, ILogger<WolverineCorrelationMiddleware> logger, IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;

        // Propagate CorrelationId
        // 1. Try HttpContext.Items (assigned by MartenMetadataMiddleware)
        // 2. Try HTTP Header (safety fallback)
        // 3. Try Wolverine's internal CorrelationId
        // 4. Try Activity Tag

        var correlationId = httpContext?.Items["CorrelationId"] as string
            ?? httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? context.CorrelationId
            ?? Activity.Current?.GetTagItem("correlation_id") as string;

        if (!string.IsNullOrEmpty(correlationId))
        {
            session.CorrelationId = correlationId;
        }

        // Determine CausationId
        // 1. Prefer explicit header if present
        // 2. Try HttpContext.Items
        // 3. Fallback to the Message ID of the command being handled
        var envelope = context.Envelope;
        if (envelope?.Headers.TryGetValue("X-Causation-ID", out var cidObj) == true && cidObj is string cid)
        {
            session.CausationId = cid;
        }
        else
        {
            var causationId = httpContext?.Items["CausationId"] as string
                ?? Activity.Current?.GetTagItem("causation_id") as string
                ?? envelope?.Id.ToString();

            if (!string.IsNullOrEmpty(causationId))
            {
                session.CausationId = causationId;
            }
        }

        // Propagate Technical Headers
        if (httpContext != null)
        {
            var userId = httpContext.User?.GetUserId().ToString();
            var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();

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
        }

        logger.LogInformation("[WOLVERINE-CORRELATION] Session CorrelationId: {SessionId}, CausationId: {SessionCid} (HttpContext present: {HasContext})",
            session.CorrelationId, session.CausationId, httpContext != null);
    }
}
