using BookStore.ApiService.Models;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Marten;

namespace BookStore.ApiService.Endpoints;

public static class TenantInfoEndpoints
{
    public static RouteGroupBuilder MapTenantInfoEndpoints(this RouteGroupBuilder group)
    {
        // Public endpoints for listing tenants
        _ = group.WithMetadata(new AllowAnonymousTenantAttribute());

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
            .Select(t => new TenantInfoDto(t.Id, t.Name, t.Tagline, t.ThemePrimaryColor, true, null,
                t.ThemeSecondaryColor, t.LogoUrl, t.FontFamily, t.BorderRadiusStyle,
                t.HeroBannerUrl, t.SuccessColor, t.ErrorColor))
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

        return Results.Ok(new TenantInfoDto(tenant.Id, tenant.Name, tenant.Tagline, tenant.ThemePrimaryColor, tenant.IsEnabled, null,
            tenant.ThemeSecondaryColor, tenant.LogoUrl, tenant.FontFamily, tenant.BorderRadiusStyle,
            tenant.HeroBannerUrl, tenant.SuccessColor, tenant.ErrorColor));
    }
}
