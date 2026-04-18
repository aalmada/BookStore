using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class TenantSecurityMiddleware(RequestDelegate next, ILogger<TenantSecurityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var currentTenant = tenantContext.TenantId;

        // Security: specific tenant access requires authentication
        // Only the default tenant allows anonymous access (public data)
        // Exceptions: Endpoints marked with [AllowAnonymousTenant]

        var endpoint = context.GetEndpoint();
        var hasAnonymousTenantAttribute = endpoint?.Metadata.GetMetadata<AllowAnonymousTenantAttribute>() != null;

        if (hasAnonymousTenantAttribute)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userTenant = context.User.FindFirst("tenant_id")?.Value;

            // Security: Ensure tenant_id claim is present
            if (string.IsNullOrEmpty(userTenant))
            {
                Log.Tenants.CrossTenantAccessAttempted(logger, "MISSING_CLAIM", currentTenant ?? "UNKNOWN");
                await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden,
                    "Forbidden", "Security violation: Missing tenant claim");
                return;
            }

            // Check for tenant mismatch
            if (!string.IsNullOrEmpty(currentTenant) &&
                !string.Equals(userTenant, currentTenant, StringComparison.OrdinalIgnoreCase))
            {
                Log.Tenants.CrossTenantAccessAttempted(logger, userTenant, currentTenant);
                await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden,
                    "Forbidden", "Cross-tenant access denied");
                return;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(currentTenant) &&
                !string.Equals(currentTenant, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
            {
                Log.Tenants.CrossTenantAccessAttempted(logger, "ANONYMOUS", currentTenant);
                await WriteProblemDetailsAsync(context, StatusCodes.Status403Forbidden,
                    "Forbidden", "Anonymous access to tenant specific data is denied");
                return;
            }
        }

        await next(context);
    }

    static async Task WriteProblemDetailsAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://tools.ietf.org/html/rfc9110#section-{GetRfcSection(statusCode)}"
        }, options: null, contentType: "application/problem+json");
    }

    static string GetRfcSection(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => "15.5.2",
        StatusCodes.Status403Forbidden => "15.5.4",
        _ => "15.6"
    };
}

public static class TenantSecurityMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantSecurity(this IApplicationBuilder builder) => builder.UseMiddleware<TenantSecurityMiddleware>();
}
