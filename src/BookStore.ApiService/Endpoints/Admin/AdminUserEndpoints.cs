using System.Security.Claims;
using BookStore.ApiService.Infrastructure;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Models;
using BookStore.Shared.Infrastructure;
using BookStore.Shared.Models;
using Marten;
using Marten.Linq;
using Marten.Pagination;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BookStore.ApiService.Endpoints.Admin;

public static class AdminUserEndpoints
{
    public static RouteGroupBuilder MapAdminUserEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetUsers)
            .WithName("GetUsers")
            .WithSummary("Get all users in the current tenant");

        _ = group.MapPost("/{userId:guid}/promote", PromoteToAdmin)
            .WithName("PromoteToAdmin")
            .WithSummary("Promote a user to the Admin role");

        _ = group.MapPost("/{userId:guid}/demote", DemoteFromAdmin)
            .WithName("DemoteFromAdmin")
            .WithSummary("Demote a user from the Admin role");

        return group.RequireAuthorization("Admin");
    }

    static async Task<IResult> GetUsers(
        [AsParameters] UserSearchRequest request,
        IDocumentSession session,
        IOptions<PaginationOptions> paginationOptions,
        CancellationToken ct)
    {
        var paging = request.Normalize(paginationOptions.Value);

        IQueryable<ApplicationUser> query = session.Query<ApplicationUser>();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(u => u.Email!.Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        }

        if (request.IsAdmin.HasValue)
        {
            if (request.IsAdmin.Value)
            {
                query = query.Where(u => u.Roles.Any(r => r == "Admin"));
            }
            else
            {
                query = query.Where(u => !u.Roles.Any(r => r == "Admin"));
            }
        }

        if (request.EmailConfirmed.HasValue)
        {
            query = query.Where(u => u.EmailConfirmed == request.EmailConfirmed.Value);
        }

        if (request.HasPassword.HasValue)
        {
            if (request.HasPassword.Value)
            {
                query = query.Where(u => u.PasswordHash != null && u.PasswordHash != "");
            }
            else
            {
                query = query.Where(u => u.PasswordHash == null || u.PasswordHash == "");
            }
        }

        if (request.HasPasskey.HasValue)
        {
            if (request.HasPasskey.Value)
            {
                query = query.Where(u => u.Passkeys.Count > 0);
            }
            else
            {
                query = query.Where(u => u.Passkeys.Count == 0);
            }
        }

        var normalizedSortOrder = request.SortOrder?.ToLowerInvariant() == "desc" ? "desc" : "asc";
        var normalizedSortBy = request.SortBy?.ToLowerInvariant();

        query = (normalizedSortBy, normalizedSortOrder) switch
        {
            ("email", "desc") => query.OrderByDescending(u => u.Email),
            _ => query.OrderBy(u => u.Email)
        };

        var pagedList = await ((IMartenQueryable<ApplicationUser>)query).ToPagedListAsync(paging.Page!.Value, paging.PageSize!.Value, ct);

        var dtos = pagedList.Select(u => new UserAdminDto(
            u.Id,
            u.Email ?? "",
            u.EmailConfirmed,
            [.. u.Roles.Select(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : r)],
            !string.IsNullOrEmpty(u.PasswordHash),
            u.Passkeys.Count > 0
        )).ToList();

        return Results.Ok(new PagedListDto<UserAdminDto>(
            dtos,
            pagedList.PageNumber,
            pagedList.PageSize,
            pagedList.TotalItemCount));
    }

    static async Task<IResult> PromoteToAdmin(
        Guid userId,
        ClaimsPrincipal currentUser,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct)
    {
        var currentUserId = currentUser.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == userId.ToString())
        {
            return Result.Failure(Error.Validation(ErrorCodes.Admin.CannotPromoteSelf, "You cannot promote yourself.")).ToProblemDetails();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Admin.UserNotFound, "User not found.")).ToProblemDetails();
        }

        if (user.Roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Admin.AlreadyAdmin, "User is already an Admin.")).ToProblemDetails();
        }

        var result = await userManager.AddToRoleAsync(user, "Admin");
        if (result.Succeeded)
        {
            return Results.Ok();
        }

        return Result.Failure(Error.Validation(ErrorCodes.Admin.AlreadyAdmin, string.Join(", ", result.Errors.Select(e => e.Description)))).ToProblemDetails();
    }

    static async Task<IResult> DemoteFromAdmin(
        Guid userId,
        ClaimsPrincipal currentUser,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct)
    {
        var currentUserId = currentUser.FindFirstValue(ClaimTypes.NameIdentifier);
        if (currentUserId == userId.ToString())
        {
            return Result.Failure(Error.Validation(ErrorCodes.Admin.CannotDemoteSelf, "You cannot demote yourself.")).ToProblemDetails();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.Failure(Error.NotFound(ErrorCodes.Admin.UserNotFound, "User not found.")).ToProblemDetails();
        }

        if (!user.Roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(Error.Validation(ErrorCodes.Admin.NotAdmin, "User is not an Admin.")).ToProblemDetails();
        }

        var result = await userManager.RemoveFromRoleAsync(user, "Admin");
        if (result.Succeeded)
        {
            return Results.Ok();
        }

        return Result.Failure(Error.Validation(ErrorCodes.Admin.NotAdmin, string.Join(", ", result.Errors.Select(e => e.Description)))).ToProblemDetails();
    }
}
