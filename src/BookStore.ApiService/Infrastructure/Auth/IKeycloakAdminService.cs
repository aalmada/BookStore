using BookStore.Shared.Models;

namespace BookStore.ApiService.Infrastructure.Auth;

public interface IKeycloakAdminService
{
    Task<Result<string>> CreateUserAsync(
        string tenantId,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(
        string keycloakUserId,
        CancellationToken cancellationToken = default);
}
