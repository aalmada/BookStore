using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IAdminUserClient
{
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
        string? sortOrder = null);

    [Post("/api/admin/users/{userId}/promote")]
    Task PromoteToAdminAsync(Guid userId);

    [Post("/api/admin/users/{userId}/demote")]
    Task DemoteFromAdminAsync(Guid userId);
}
