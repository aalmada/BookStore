using JasperFx;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class TenantContext : ITenantContext
{
    public string TenantId
    {
        get;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("TenantId cannot be null or whitespace", nameof(value));
            }

            field = value;
        }
    } = StorageConstants.DefaultTenantId;
}
