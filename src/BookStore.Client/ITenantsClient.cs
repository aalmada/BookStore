using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface ITenantsClient
{
    [Get("/api/tenants/{id}")]
    Task<TenantInfoDto> GetTenantAsync(string id);

    [Get("/api/tenants")]
    Task<List<TenantInfoDto>> GetTenantsAsync();

    [Get("/api/admin/tenants")]
    Task<List<TenantInfoDto>> GetAllTenantsAdminAsync();

    [Post("/api/admin/tenants")]
    Task CreateTenantAsync([Body] CreateTenantCommand command);

    [Put("/api/admin/tenants/{id}")]
    Task UpdateTenantAsync(string id, [Body] UpdateTenantCommand command, [Header("If-Match")] string? etag = null);
}
