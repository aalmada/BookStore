using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Auth;
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
            .ToListAsync(ct);

        return Results.Ok(tenants.Select(t => new TenantInfoDto(t.Id, t.Name, t.Tagline, t.ThemePrimaryColor, t.IsEnabled, t.Version.ToString())));
    }

    // POST /api/admin/tenants
    public static async Task<IResult> CreateTenant(
        [FromBody] CreateTenantCommand request,
        IDocumentStore store,
        ITenantContext tenantContext,
        ITenantStore tenantStore,
        IKeycloakAdminService keycloakAdminService,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("TenantEndpoints");

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

        string? keycloakUserId = null;

        // Provision tenant admin in Keycloak before committing tenant changes locally.
        if (!string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            var keycloakResult = await keycloakAdminService.CreateUserAsync(
                tenant.Id,
                request.AdminEmail,
                request.AdminPassword ?? string.Empty,
                ct);

            if (keycloakResult.IsFailure)
            {
                return keycloakResult.ToProblemDetails();
            }

            keycloakUserId = keycloakResult.Value;
        }

        try
        {
            await session.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(keycloakUserId))
            {
                TenantEndpointsLog.TenantSaveFailedCompensating(logger, tenant.Id, keycloakUserId, ex);

                var deleteUserResult = await keycloakAdminService.DeleteUserAsync(keycloakUserId, ct);
                if (deleteUserResult.IsFailure)
                {
                    TenantEndpointsLog.CompensationDeleteFailed(logger, tenant.Id, keycloakUserId, deleteUserResult.Error.Code);

                    return Result.Failure(Error.Failure(
                        ErrorCodes.Tenancy.TenantPersistenceCompensationFailed,
                        "Tenant creation failed after provisioning the tenant admin in Keycloak; compensation was attempted."))
                        .ToProblemDetails();
                }

                TenantEndpointsLog.CompensationDeleteSucceeded(logger, tenant.Id, keycloakUserId);
                return Result.Failure(Error.Failure(
                    ErrorCodes.Tenancy.TenantPersistenceFailed,
                    "Tenant creation failed while saving tenant data."))
                    .ToProblemDetails();
            }

            TenantEndpointsLog.TenantSaveFailed(logger, tenant.Id, ex);
            return Result.Failure(Error.Failure(
                ErrorCodes.Tenancy.TenantPersistenceFailed,
                "Tenant creation failed while saving tenant data."))
                .ToProblemDetails();
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
        HttpContext httpContext,
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

        var ifMatch = httpContext.Request.Headers["If-Match"].FirstOrDefault();
        if (!string.IsNullOrEmpty(ifMatch) && Guid.TryParse(ifMatch.Trim('"'), out var expectedVersion) && tenant.Version != expectedVersion)
        {
            return Result.Failure(Error.Conflict(ErrorCodes.Tenancy.ConcurrencyConflict, "Tenant has been modified by another request. Please reload and try again.")).ToProblemDetails();
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
static partial class TenantEndpointsLog
{
    [LoggerMessage(EventId = 9300, Level = LogLevel.Error, Message = "Failed to save tenant {TenantId} after provisioning Keycloak user {KeycloakUserId}; starting compensation")]
    public static partial void TenantSaveFailedCompensating(ILogger logger, string tenantId, string keycloakUserId, Exception exception);

    [LoggerMessage(EventId = 9301, Level = LogLevel.Error, Message = "Compensation failed deleting Keycloak user {KeycloakUserId} for tenant {TenantId}. Error code: {ErrorCode}")]
    public static partial void CompensationDeleteFailed(ILogger logger, string tenantId, string keycloakUserId, string errorCode);

    [LoggerMessage(EventId = 9302, Level = LogLevel.Information, Message = "Compensation succeeded deleting Keycloak user {KeycloakUserId} for tenant {TenantId}")]
    public static partial void CompensationDeleteSucceeded(ILogger logger, string tenantId, string keycloakUserId);

    [LoggerMessage(EventId = 9303, Level = LogLevel.Error, Message = "Failed to save tenant {TenantId}")]
    public static partial void TenantSaveFailed(ILogger logger, string tenantId, Exception exception);
}
