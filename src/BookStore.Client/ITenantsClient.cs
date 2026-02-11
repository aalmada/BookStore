using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing tenants (Admin only for most operations).
/// </summary>
public interface ITenantsClient
{
    /// <summary>
    /// Gets specific tenant information by its ID.
    /// </summary>

    [Get("/api/tenants/{id}")]
    Task<TenantInfoDto> GetTenantAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of available tenants.
    /// </summary>
    [Get("/api/tenants")]
    Task<List<TenantInfoDto>> GetTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all tenants for admin management.
    /// </summary>
    [Get("/api/admin/tenants")]
    Task<List<TenantInfoDto>> GetAllTenantsAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new tenant (Admin only).
    /// </summary>
    [Post("/api/admin/tenants")]
    Task CreateTenantAsync([Body] CreateTenantCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tenant (Admin only).
    /// </summary>
    [Put("/api/admin/tenants/{id}")]
    Task UpdateTenantAsync(string id, [Body] UpdateTenantCommand command, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}

