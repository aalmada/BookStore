using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing users (Admin only).
/// </summary>
public interface IUsersClient
{
    /// <summary>
    /// Gets a paged list of users with optional filtering.
    /// </summary>

    [Get("/api/admin/users")]
    Task<PagedListDto<UserAdminDto>> GetUsersAsync(
        string? search = null,
        bool? isAdmin = null,
        bool? emailConfirmed = null,
        bool? hasPassword = null,
        bool? hasPasskey = null,
        int? page = null,
        int? pageSize = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes a user to administrator role.
    /// </summary>
    [Post("/api/admin/users/{userId}/promote")]
    Task PromoteToAdminAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Demotes a user from administrator role.
    /// </summary>
    [Post("/api/admin/users/{userId}/demote")]
    Task DemoteFromAdminAsync(Guid userId, CancellationToken cancellationToken = default);
}

