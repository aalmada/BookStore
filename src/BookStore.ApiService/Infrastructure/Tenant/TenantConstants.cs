using JasperFx;

namespace BookStore.ApiService.Infrastructure.Tenant;

/// <summary>
/// Central constants for multi-tenancy configuration.
/// For the default tenant ID, use JasperFx.StorageConstants.DefaultTenantId directly.
/// </summary>
public static class TenantConstants
{
    /// <summary>
    /// All known tenant IDs for seeding and configuration.
    /// </summary>
    public static readonly string[] KnownTenants = [StorageConstants.DefaultTenantId, "acme", "contoso"];
}
