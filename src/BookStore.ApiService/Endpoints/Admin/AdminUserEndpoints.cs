using BookStore.Shared.Models;
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

    static Task<IResult> GetUsers(CancellationToken ct) => Task.FromResult(NotImplemented());

    static Task<IResult> PromoteToAdmin(Guid userId, CancellationToken ct) => Task.FromResult(NotImplemented());

    static Task<IResult> DemoteFromAdmin(Guid userId, CancellationToken ct) => Task.FromResult(NotImplemented());

    static IResult NotImplemented() => Results.Problem(
        title: "Not Implemented",
        detail: "User management is performed via Keycloak Admin Console.",
        statusCode: StatusCodes.Status501NotImplemented,
        extensions: new Dictionary<string, object?>
        {
            ["error"] = ErrorCodes.Admin.UserManagementNotImplemented
        });
}
