using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing publishers in the system.
/// </summary>
public interface IPublishersClient
{
    /// <summary>
    /// Gets a paged list of publishers.
    /// </summary>

    [Headers("Accept: application/json")]
    [Get("/api/publishers")]
    Task<PagedListDto<PublisherDto>> GetPublishersAsync([Query, AliasAs("Page")] int? page, [Query, AliasAs("PageSize")] int? pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific publisher by its ID.
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Get("/api/publishers/{id}")]
    Task<PublisherDto> GetPublisherAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific publisher by its ID with full API response.
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Get("/api/publishers/{id}")]
    Task<IApiResponse<PublisherDto>> GetPublisherWithResponseAsync(Guid id, [Header("If-None-Match")] string? ifNoneMatch = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all publishers for admin management.
    /// </summary>
    [Get("/api/admin/publishers")]

    Task<PagedListDto<PublisherDto>> GetAllPublishersAsync(
        [Query] PublisherSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new publisher (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Post("/api/admin/publishers")]
    Task CreatePublisherAsync([Body] CreatePublisherRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new publisher with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]
    [Post("/api/admin/publishers")]
    Task<IApiResponse<PublisherDto>> CreatePublisherWithResponseAsync([Body] CreatePublisherRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing publisher (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/publishers/{id}")]
    Task UpdatePublisherAsync(Guid id, [Body] UpdatePublisherRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing publisher with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/publishers/{id}")]
    Task<IApiResponse> UpdatePublisherWithResponseAsync(Guid id, [Body] UpdatePublisherRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a publisher (Admin only).
    /// </summary>
    [Delete("/api/admin/publishers/{id}")]

    Task SoftDeletePublisherAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a publisher with full API response (Admin only).
    /// </summary>
    [Delete("/api/admin/publishers/{id}")]

    Task<IApiResponse> SoftDeletePublisherWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted publisher (Admin only).
    /// </summary>
    [Post("/api/admin/publishers/{id}/restore")]

    Task RestorePublisherAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted publisher with full API response (Admin only).
    /// </summary>
    [Post("/api/admin/publishers/{id}/restore")]
    Task<IApiResponse> RestorePublisherWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);
}

