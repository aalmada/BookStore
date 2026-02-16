using BookStore.ApiService.Models;
using BookStore.Shared;
using Marten;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class MartenTenantStore(IDocumentStore store) : ITenantStore
{
    public async Task<bool> IsValidTenantAsync(string tenantId)
    {
        // Special case for "*DEFAULT*" which might not always be in the DB depending on initialization
        // Also accept "default" (case-insensitive) as an alias for the default tenant
        if (MultiTenancyConstants.DefaultTenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) ||
            MultiTenancyConstants.DefaultTenantAlias.Equals(tenantId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Use a lightweight session to query for the tenant
        // Uses Marten's native default tenant bucket
        await using var session = store.LightweightSession();

        var tenant = await session.LoadAsync<BookStore.ApiService.Models.Tenant>(tenantId);
        return tenant is { IsEnabled: true };
    }

    public async Task<IEnumerable<string>> GetAllTenantsAsync()
    {
        // Uses Marten's native default tenant bucket
        await using var session = store.LightweightSession();
        return await session.Query<BookStore.ApiService.Models.Tenant>()
            .Select(t => t.Id)
            .ToListAsync();
    }

    public Task InvalidateCacheAsync(string tenantId) => Task.CompletedTask;
}
