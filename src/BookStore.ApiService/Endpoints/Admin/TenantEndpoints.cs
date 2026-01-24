using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Infrastructure.Tenant;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
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
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        // Security: Only the Default (System) Tenant can see all tenants
        if (!string.Equals(tenantContext.TenantId, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Forbidden(ErrorCodes.Tenancy.AccessDenied, "Access denied.")).ToProblemDetails();
        }

        // Use a lightweight session on the native default tenant (global scope for tenants)
        await using var session = store.LightweightSession();

        var tenants = await session.Query<Tenant>()
            .OrderBy(t => t.Id)
            .Select(t => new TenantInfoDto(t.Id, t.Name, t.Tagline, t.ThemePrimaryColor, t.IsEnabled))
            .ToListAsync(ct);

        return Results.Ok(tenants);
    }

    // POST /api/admin/tenants
    public static async Task<IResult> CreateTenant(
        [FromBody] CreateTenantCommand request,
        IDocumentStore store,
        ITenantContext tenantContext,
        ITenantStore tenantStore,
        [FromServices] Wolverine.IMessageBus bus,
        [FromServices] Microsoft.Extensions.Options.IOptions<BookStore.ApiService.Infrastructure.Email.EmailOptions> emailOptions,
        CancellationToken ct)
    {
        // Security check
        if (!string.Equals(tenantContext.TenantId, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Forbidden(ErrorCodes.Tenancy.AccessDenied, "Access denied.")).ToProblemDetails();
        }

        if (string.IsNullOrWhiteSpace(request.Id))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Tenancy.TenantIdRequired, "Tenant ID is required.")).ToProblemDetails();
        }

        var (isValid, errors) = BookStore.Shared.Validation.TenantIdValidator.Validate(request.Id);
        if (!isValid)
        {
            return Result.Failure(Error.Validation(ErrorCodes.Tenancy.InvalidTenantId, errors.FirstOrDefault() ?? "Invalid Tenant ID.")).ToProblemDetails();
        }

        if (!string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            var (isPasswordValid, passwordErrors) = BookStore.Shared.Validation.PasswordValidator.Validate(request.AdminPassword);
            if (!isPasswordValid)
            {
                return Result.Failure(Error.Validation(ErrorCodes.Tenancy.InvalidAdminPassword, passwordErrors.FirstOrDefault() ?? "Invalid Password.")).ToProblemDetails();
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            if (!BookStore.Shared.Validation.EmailValidator.IsValid(request.AdminEmail))
            {
                return Result.Failure(Error.Validation(ErrorCodes.Tenancy.InvalidAdminEmail, "Invalid Admin Email.")).ToProblemDetails();
            }
        }

        // Use a lightweight session on the native default tenant
        await using var session = store.LightweightSession();

        var existing = await session.LoadAsync<Tenant>(request.Id, ct);
        if (existing != null)
        {
            return Result.Failure(Error.Conflict(ErrorCodes.Tenancy.TenantAlreadyExists, $"Tenant '{request.Id}' already exists.")).ToProblemDetails();
        }

        var tenant = new Tenant
        {
            Id = request.Id,
            Name = request.Name,
            Tagline = request.Tagline,
            ThemePrimaryColor = request.ThemePrimaryColor,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow
        };

        session.Store(tenant);
        await session.SaveChangesAsync(ct);

        // Seed initial admin user if provided
        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            var verificationRequired = emailOptions.Value.DeliveryMethod != "None";

            // Invoke the seeding command in the context of the NEW tenant
            var seedCommand = new Messages.Commands.SeedTenantAdmin(
                tenant.Id,
                request.AdminEmail,
                request.AdminPassword,
                verificationRequired);

            await bus.InvokeAsync(seedCommand, new Wolverine.DeliveryOptions { TenantId = tenant.Id }, ct);
        }

        // Invalidate cache
        await tenantStore.InvalidateCacheAsync(tenant.Id);

        return Results.Created($"/api/admin/tenants/{tenant.Id}", tenant);
    }

    // PUT /api/admin/tenants/{id}
    public static async Task<IResult> UpdateTenant(
        string id,
        [FromBody] UpdateTenantCommand request,
        IDocumentStore store,
        ITenantContext tenantContext,
        ITenantStore tenantStore,
        CancellationToken ct)
    {
        // Security check: Only System Admin can update tenant definitions
        if (!string.Equals(tenantContext.TenantId, JasperFx.StorageConstants.DefaultTenantId, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Forbidden(ErrorCodes.Tenancy.AccessDenied, "Access denied.")).ToProblemDetails();
        }

        await using var session = store.LightweightSession();

        var tenant = await session.LoadAsync<Tenant>(id, ct);
        if (tenant == null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Tenancy.TenantNotFound, "Tenant not found.")).ToProblemDetails();
        }

        tenant.Name = request.Name;
        tenant.Tagline = request.Tagline;
        tenant.ThemePrimaryColor = request.ThemePrimaryColor;
        tenant.IsEnabled = request.IsEnabled;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        session.Store(tenant);
        await session.SaveChangesAsync(ct);

        // Invalidate cache
        await tenantStore.InvalidateCacheAsync(id);

        return Results.Ok(tenant);
    }
}
