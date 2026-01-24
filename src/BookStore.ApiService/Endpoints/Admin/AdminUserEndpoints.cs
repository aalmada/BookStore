using System.Security.Claims;
using BookStore.ApiService.Infrastructure.Extensions;
using BookStore.ApiService.Models;
using BookStore.Shared.Models;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        IDocumentSession session,
        CancellationToken ct)
    {
        var users = await session.Query<ApplicationUser>()
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var dtos = users.Select(u => new UserAdminDto(
            u.Id,
            u.Email ?? "",
            u.EmailConfirmed,
            [.. u.Roles.Select(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : r)]
        )).ToList();

        return Results.Ok(dtos);
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
