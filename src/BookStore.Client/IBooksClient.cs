using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

public interface IBooksClient
{
    [Headers("Accept: application/json")]
    [Get("/api/books")]
    Task<PagedListDto<BookDto>> GetBooksAsync(
        [Query] BookSearchRequest request,
        CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/books/favorites")]
    Task<PagedListDto<BookDto>> GetFavoriteBooksAsync(
        [Query] OrderedPagedRequest request,
        CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/books/{id}")]
    Task<BookDto> GetBookAsync(Guid id, CancellationToken cancellationToken = default);

    [Get("/api/books/{id}")]
    Task<IApiResponse<BookDto>> GetBookWithResponseAsync(Guid id, CancellationToken cancellationToken = default);

    [Get("/api/admin/books")]
    Task<ICollection<AdminBookDto>> GetAllBooksAdminAsync(CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/books")]
    Task<BookDto> CreateBookAsync([Body] CreateBookRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Post("/api/admin/books")]
    Task<ApiResponse<BookDto>> CreateBookWithResponseAsync([Body] CreateBookRequest body, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/books/{id}")]
    Task UpdateBookAsync(Guid id, [Body] UpdateBookRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Content-Type: application/json")]
    [Put("/api/admin/books/{id}")]
    Task<IApiResponse> UpdateBookWithResponseAsync(Guid id, [Body] UpdateBookRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/books/{id}")]
    Task SoftDeleteBookAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Delete("/api/admin/books/{id}")]
    Task<IApiResponse> SoftDeleteBookWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Multipart]
    [Post("/api/admin/books/{id}/cover")]
    Task UploadBookCoverAsync(Guid id, StreamPart file, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Post("/api/books/{id}/favorites")]
    Task AddBookToFavoritesAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Delete("/api/books/{id}/favorites")]
    Task RemoveBookFromFavoritesAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Post("/api/books/{id}/rating")]
    Task RateBookAsync(Guid id, [Body] RateBookRequest request, [Header("If-Match")] string? etag = null);

    [Delete("/api/books/{id}/rating")]
    Task RemoveBookRatingAsync(Guid id, [Header("If-Match")] string? etag = null);

    [Post("/api/books/{id}/sales")]
    Task ScheduleBookSaleAsync(Guid id, [Body] ScheduleSaleRequest request, [Header("If-Match")] string? etag = null);

    [Post("/api/books/{id}/sales")]
    Task<IApiResponse> ScheduleBookSaleWithResponseAsync(Guid id, [Body] ScheduleSaleRequest request, [Header("If-Match")] string? etag = null);

    [Delete("/api/books/{id}/sales")]
    Task CancelBookSaleAsync(Guid id, [Query] DateTimeOffset saleStart, [Header("If-Match")] string? etag = null);

    [Delete("/api/books/{id}/sales")]
    Task<IApiResponse> CancelBookSaleWithResponseAsync(Guid id, [Query] DateTimeOffset saleStart, [Header("If-Match")] string? etag = null);

    [Post("/api/admin/books/{id}/restore")]
    Task RestoreBookAsync(Guid id, [Header("api-version")] string apiVersion = "1.0", [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/books/{id}")]
    Task<AdminBookDto> GetBookAdminAsync(Guid id, CancellationToken cancellationToken = default);

    [Headers("Accept: application/json")]
    [Get("/api/admin/books/{id}")]
    Task<IApiResponse<AdminBookDto>> GetBookAdminWithResponseAsync(Guid id, CancellationToken cancellationToken = default);
}

public record RateBookRequest(int Rating);
public record ScheduleSaleRequest(decimal Percentage, DateTimeOffset Start, DateTimeOffset End);
