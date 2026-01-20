using JasperFx;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class TenantContext : ITenantContext
{
    public string TenantId { get; private set; } = StorageConstants.DefaultTenantId;

    public void Initialize(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId cannot be null or whitespace", nameof(tenantId));
        }

        if (TenantId != StorageConstants.DefaultTenantId && !string.Equals(TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"TenantContext is already initialized to '{TenantId}'. Cannot change to '{tenantId}'.");
        }

        TenantId = tenantId;
    }
}
