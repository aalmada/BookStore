using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IPublishersClient
{
    [Headers("Accept: application/json")]
    [Get("/api/publishers")]
    Task<PagedListDto<PublisherDto>> GetPublishersAsync([Query, AliasAs("Page")] int? page, [Query, AliasAs("PageSize")] int? pageSize, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Get("/api/publishers/{id}")]
    Task<PublisherDto> GetPublisherAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Get("/api/publishers/{id}")]
    Task<IApiResponse<PublisherDto>> GetPublisherWithResponseAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, CancellationToken cancellationToken = default);

    [Get("/api/admin/publishers")]
    Task<PagedListDto<PublisherDto>> GetAllPublishersAsync(
        [Query] PublisherSearchRequest request,
        CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/publishers")]
    Task CreatePublisherAsync([Body] CreatePublisherRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/publishers")]
    Task<ApiResponse<PublisherDto>> CreatePublisherWithResponseAsync([Body] CreatePublisherRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/publishers/{id}")]
    Task UpdatePublisherAsync(Guid id, [Body] UpdatePublisherRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/publishers/{id}")]
    Task<IApiResponse> UpdatePublisherWithResponseAsync(Guid id, [Body] UpdatePublisherRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/publishers/{id}")]
    Task SoftDeletePublisherAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/publishers/{id}")]
    Task<IApiResponse> SoftDeletePublisherWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/admin/publishers/{id}/restore")]
    Task RestorePublisherAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/admin/publishers/{id}/restore")]
    Task<IApiResponse> RestorePublisherWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}
