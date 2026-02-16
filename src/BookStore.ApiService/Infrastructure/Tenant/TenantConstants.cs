using BookStore.Shared;

namespace BookStore.ApiService.Infrastructure.Tenant;

/// <summary>
/// Central constants for multi-tenancy configuration.
/// For the default tenant ID, use MultiTenancyConstants.DefaultTenantId from BookStore.Shared.
/// </summary>
public static class TenantConstants
{
    /// <summary>
    /// All known tenant IDs for seeding and configuration.
    /// </summary>
    public static readonly string[] KnownTenants = [MultiTenancyConstants.DefaultTenantId, "acme", "contoso"];
}
