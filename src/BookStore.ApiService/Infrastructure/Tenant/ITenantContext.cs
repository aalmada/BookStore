namespace BookStore.ApiService.Infrastructure.Tenant;

public interface ITenantContext
{
    string TenantId { get; }
    void Initialize(string tenantId);
}
