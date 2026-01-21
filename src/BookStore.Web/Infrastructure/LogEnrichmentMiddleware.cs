using BookStore.Client.Services;
using BookStore.Web.Services;

namespace BookStore.Web.Infrastructure;

public class LogEnrichmentMiddleware
{
    readonly RequestDelegate _next;
    readonly ILogger<LogEnrichmentMiddleware> _logger;

    public LogEnrichmentMiddleware(RequestDelegate next, ILogger<LogEnrichmentMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientContext = context.RequestServices.GetService<ClientContextService>();
        var tenantService = context.RequestServices.GetService<TenantService>();

        if (clientContext == null || tenantService == null)
        {
            await _next(context);
            return;
        }

        // Initialize tenant from query string or cookie
        // Note: SetTenantAsync validates tenant existence via API call and falls back gracefully
        if (context.Request.Query.TryGetValue("tenant", out var tenantId) && !string.IsNullOrWhiteSpace(tenantId))
        {
            await tenantService.SetTenantAsync(tenantId.ToString());
        }
        else if (context.Request.Cookies.TryGetValue("tenant", out var cookieTenantId) &&
                 !string.IsNullOrWhiteSpace(cookieTenantId))
        {
            // URL-decode the cookie value (set with encodeURIComponent in JS)
            var decodedTenantId = Uri.UnescapeDataString(cookieTenantId);
            await tenantService.SetTenantAsync(decodedTenantId);
        }

        var scopeState = new Dictionary<string, object>
        {
            ["CorrelationId"] = clientContext.CorrelationId,
            ["CausationId"] = clientContext.CausationId,
            ["TenantId"] = tenantService.CurrentTenantId
        };

        // Enrich with basic browser info from headers
        if (context.Request.Headers.TryGetValue("User-Agent", out var userAgent))
        {
            scopeState["Browser.UserAgent"] = userAgent.ToString();
        }

        using (_logger.BeginScope(scopeState))
        {
            await _next(context);
        }
    }
}
