using BookStore.ApiService.Models;
using Marten;

namespace BookStore.ApiService.Infrastructure.Tenant;

public class MartenTenantStore(IDocumentStore store) : ITenantStore
{
    public async Task<bool> IsValidTenantAsync(string tenantId)
    {
        // Special case for "default" which might not always be in the DB depending on initialization
        if (tenantId == "default")
        {
            return true;
        }

        // Use a lightweight session to query for the tenant
        // We query the "default" tenant (or global scope) where tenants are stored
        // Note: We assume Tenant documents are stored in the default tenant (or are not multi-tenanted themselves)
        // If Tenant documents are multi-tenanted, we'd have a chicken-and-egg problem.
        // So we must ensure Tenant documents are treated as global or belonging to 'default'.
        await using var session = store.LightweightSession("default");

        var tenant = await session.LoadAsync<BookStore.ApiService.Models.Tenant>(tenantId);
        return tenant is { IsEnabled: true };
    }

    public async Task<IEnumerable<string>> GetAllTenantsAsync()
    {
        await using var session = store.LightweightSession("default");
        return await session.Query<BookStore.ApiService.Models.Tenant>()
            .Select(t => t.Id)
            .ToListAsync();
    }
}
