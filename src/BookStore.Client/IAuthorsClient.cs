using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IAuthorsClient
{
    [Headers("Accept: application/json")]
    [Get("/api/authors")]
    Task<PagedListDto<AuthorDto>> GetAuthorsAsync([Query, AliasAs("Page")] int? page, [Query, AliasAs("PageSize")] int? pageSize, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/authors/{id}")]
    Task<AuthorDto> GetAuthorAsync(Guid id, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/authors/{id}")]
    Task<IApiResponse<AuthorDto>> GetAuthorWithResponseAsync(Guid id, [Header("Accept-Language")] string? acceptLanguage = default, CancellationToken cancellationToken = default);

    [Get("/api/admin/authors")]
    Task<PagedListDto<AdminAuthorDto>> GetAllAuthorsAsync(
        [Query] AuthorSearchRequest request,
        CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/authors/{id}")]
    Task<AdminAuthorDto> GetAuthorAdminAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/authors/{id}")]
    Task<IApiResponse<AdminAuthorDto>> GetAuthorAdminWithResponseAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/authors")]
    Task CreateAuthorAsync([Body] CreateAuthorRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/authors")]
    Task<ApiResponse<AuthorDto>> CreateAuthorWithResponseAsync([Body] CreateAuthorRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/authors/{id}")]
    Task UpdateAuthorAsync(Guid id, [Body] UpdateAuthorRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/authors/{id}")]
    Task<IApiResponse> UpdateAuthorWithResponseAsync(Guid id, [Body] UpdateAuthorRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/authors/{id}")]
    Task SoftDeleteAuthorAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/authors/{id}")]
    Task<IApiResponse> SoftDeleteAuthorWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/admin/authors/{id}/restore")]
    Task RestoreAuthorAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/admin/authors/{id}/restore")]
    Task<IApiResponse> RestoreAuthorWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}
