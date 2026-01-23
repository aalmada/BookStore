namespace BookStore.ApiService.Infrastructure.Tenant;

public interface ITenantStore
{
    Task<bool> IsValidTenantAsync(string tenantId);
    Task<IEnumerable<string>> GetAllTenantsAsync();
    Task InvalidateCacheAsync(string tenantId);
}
