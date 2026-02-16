using BookStore.ApiService.Infrastructure.Logging;
using BookStore.Shared.Models;
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
                    // Set the tenant ID on the context
                    tenantContext.Initialize(tenantId);
                }
                else
                {
                    // Invalid tenant - return 400 Bad Request with ProblemDetails
                    Log.Tenants.InvalidTenantRequested(_logger, tenantId);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/problem+json";

                    var problemDetails = new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                        title = "Bad Request",
                        status = 400,
                        detail = "The specified tenant is invalid or does not exist.",
                        error = ErrorCodes.Tenancy.TenantNotFound
                    };

                    await context.Response.WriteAsJsonAsync(problemDetails);
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
