using BookStore.ApiService.Infrastructure.Logging;
using Microsoft.AspNetCore.Http;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class TenantSecurityMiddleware(RequestDelegate next, ILogger<TenantSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var currentTenant = tenantContext.TenantId;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userTenant = context.User.FindFirst("tenant_id")?.Value;

            // Security: Ensure tenant_id claim is present
            if (string.IsNullOrEmpty(userTenant))
            {
                Log.Tenants.CrossTenantAccessAttempted(logger, "MISSING_CLAIM", currentTenant ?? "UNKNOWN");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Security violation: Missing tenant claim" });
                return;
            }

            // Check for tenant mismatch
            if (!string.IsNullOrEmpty(currentTenant) &&
                !string.Equals(userTenant, currentTenant, StringComparison.OrdinalIgnoreCase))
            {
                Log.Tenants.CrossTenantAccessAttempted(logger, userTenant, currentTenant);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Cross-tenant access denied" });
                return;
            }
        }
        else
        {
            // Security: specific tenant access requires authentication
            // Only the default tenant allows anonymous access (public data)
            // Exceptions: Authentication endpoints, Tenant Info, Health checks, Docs
            if (IsAllowedAnonymousPath(context.Request.Path))
            {
                await next(context);
                return;
            }

            if (!string.IsNullOrEmpty(currentTenant) &&
                !string.Equals(currentTenant, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
            {
                Log.Tenants.CrossTenantAccessAttempted(logger, "ANONYMOUS", currentTenant);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Anonymous access to tenant specific data is denied" });
                return;
            }
        }

        await next(context);
    }

    static bool IsAllowedAnonymousPath(PathString path) => path.StartsWithSegments("/account") ||
               path.StartsWithSegments("/api/tenants") ||
               path.StartsWithSegments("/health") ||
               path.StartsWithSegments("/metrics") ||
               path.StartsWithSegments("/api-reference") ||
               path.StartsWithSegments("/scalar") ||
               path.StartsWithSegments("/openapi");
}

public static class TenantSecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantSecurity(this IApplicationBuilder builder) => builder.UseMiddleware<TenantSecurityMiddleware>();
}
