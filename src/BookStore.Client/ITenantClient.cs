using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface ITenantClient
{
    [Get("/api/tenants/{id}")]
    Task<TenantInfoDto> GetTenantAsync(string id);

    [Get("/api/tenants")]
    Task<List<TenantInfoDto>> GetTenantsAsync();
}
