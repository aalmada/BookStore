using BookStore.Shared;
using Microsoft.AspNetCore.Authorization;

namespace BookStore.Web.Infrastructure;

public static class AuthorizationPolicyExtensions
{
    public const string SystemAdminPolicyName = "SystemAdmin";

    public static void AddSystemAdminPolicy(this AuthorizationOptions options)
        => options.AddPolicy(SystemAdminPolicyName,
            policy => policy.RequireRole("Admin")
                .RequireClaim("tenant_id", MultiTenancyConstants.DefaultTenantId));
}
