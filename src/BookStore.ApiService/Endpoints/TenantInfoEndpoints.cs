using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using Marten;

namespace BookStore.ApiService.Endpoints;

public static class TenantInfoEndpoints
{
    public static RouteGroupBuilder MapTenantInfoEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetTenants);
        _ = group.MapGet("/{id}", GetTenantInfo);
        return group;
    }

    // GET /api/tenants
    public static async Task<IResult> GetTenants(
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.LightweightSession();
        var tenants = await session.Query<Tenant>()
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.Id)
            .Select(t => new TenantInfoDto(t.Id, t.Name, t.Tagline, t.ThemePrimaryColor))
            .ToListAsync(ct);

        return Results.Ok(tenants);
    }

    // GET /api/tenants/{id}
    public static async Task<IResult> GetTenantInfo(
        string id,
        IDocumentStore store,
        CancellationToken ct)
    {
        await using var session = store.LightweightSession();

        var tenant = await session.LoadAsync<Tenant>(id, ct);

        if (tenant == null || !tenant.IsEnabled)
        {
            return Results.NotFound();
        }

        return Results.Ok(new TenantInfoDto(tenant.Id, tenant.Name, tenant.Tagline, tenant.ThemePrimaryColor));
    }
}
