using BookStore.ApiService.Models;
using Marten;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.ApiService.Endpoints.Admin;

public static class TenantEndpoints
{
    public static RouteGroupBuilder MapTenantEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetTenants);
        _ = group.MapPost("/", CreateTenant);
        _ = group.MapPut("/{id}", UpdateTenant);
        return group;
    }

    // GET /api/admin/tenants
    public static async Task<IResult> GetTenants(
        IDocumentStore store,
        BookStore.ApiService.Infrastructure.Tenant.ITenantContext tenantContext,
        CancellationToken ct)
    {
        // Security: Only the Default (System) Tenant can see all tenants
        if (!string.Equals(tenantContext.TenantId, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        {
            // If strictly tenant-locked, return Forbidden or just their own tenant
            // For now, let's return Forbidden to indicate this is a System Admin feature
            return Results.Forbid();
        }

        // Use a lightweight session on the native default tenant (global scope for tenants)
        await using var session = store.LightweightSession();

        var tenants = await session.Query<Tenant>()
            .OrderBy(t => t.Id)
            .Select(t => new TenantInfoDto(t.Id, t.Name, t.Tagline, t.ThemePrimaryColor))
            .ToListAsync(ct);

        return Results.Ok(tenants);
    }

    // POST /api/admin/tenants
    public static async Task<IResult> CreateTenant(
        [FromBody] Commands.CreateTenantCommand request,
        IDocumentStore store,
        BookStore.ApiService.Infrastructure.Tenant.ITenantContext tenantContext,
        CancellationToken ct)
    {
        // Security check
        if (!string.Equals(tenantContext.TenantId, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            return Results.BadRequest("Tenant ID is required.");
        }

        // Use a lightweight session on the native default tenant
        await using var session = store.LightweightSession();

        var existing = await session.LoadAsync<Tenant>(request.Id, ct);
        if (existing != null)
        {
            return Results.Conflict($"Tenant '{request.Id}' already exists.");
        }

        var tenant = new Tenant
        {
            Id = request.Id,
            Name = request.Name,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow
        };

        session.Store(tenant);
        await session.SaveChangesAsync(ct);

        return Results.Created($"/api/admin/tenants/{tenant.Id}", tenant);
    }

    // PUT /api/admin/tenants/{id}
    public static async Task<IResult> UpdateTenant(
        string id,
        [FromBody] Commands.UpdateTenantCommand request,
        IDocumentStore store,
        BookStore.ApiService.Infrastructure.Tenant.ITenantContext tenantContext,
        CancellationToken ct)
    {
        // Security check: Only System Admin can update tenant definitions
        if (!string.Equals(tenantContext.TenantId, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        await using var session = store.LightweightSession();

        var tenant = await session.LoadAsync<Tenant>(id, ct);
        if (tenant == null)
        {
            return Results.NotFound();
        }

        tenant.Name = request.Name;
        tenant.IsEnabled = request.IsEnabled;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        session.Store(tenant);
        await session.SaveChangesAsync(ct);

        return Results.Ok(tenant);
    }
}
