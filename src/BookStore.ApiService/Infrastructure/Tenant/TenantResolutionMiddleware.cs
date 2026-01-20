using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class TenantResolutionMiddleware
{
    readonly RequestDelegate _next;
    readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ITenantStore tenantStore)
    {
        // Check for tenant header
        if (context.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantIdValues))
        {
            var tenantId = tenantIdValues.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                // Validate tenant
                if (await tenantStore.IsValidTenantAsync(tenantId))
                {
                    // Cast to concrete type to set the property
                    // In a real DI scenario with Scoped lifetime, this works because we resolve the same instance
                    if (tenantContext is TenantContext concreteContext)
                    {
                        concreteContext.TenantId = tenantId;
                    }
                }
                else
                {
                    // Invalid tenant - return 400 Bad Request
                    Log.Tenants.InvalidTenantRequested(_logger, tenantId);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or unknown tenant" });
                    return;
                }
            }
        }

        // Use "*DEFAULT*" if no header provided (TenantContext defaults to StorageConstants.DefaultTenantId)
        // No else block needed as TenantContext initializes to "*DEFAULT*"

        // Make tenant ID available in Items for other middleware/logs
        context.Items["TenantId"] = tenantContext.TenantId;

        // Audit log: Track tenant access for security monitoring
        Log.Tenants.TenantAccess(
            _logger,
            tenantContext.TenantId,
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress?.ToString());

        await _next(context);
    }
}

public static class TenantMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder) => builder.UseMiddleware<TenantResolutionMiddleware>();
}
