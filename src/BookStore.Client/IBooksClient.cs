using BookStore.Shared.Models;
using Refit;

namespace BookStore.Client;

/// <summary>
/// Client for managing books in the system.
/// </summary>
public interface IBooksClient
{
    /// <summary>
    /// Gets a paged list of books based on search criteria.
    /// </summary>

    [Headers("Accept: application/json")]
    [Get("/api/books")]
    Task<PagedListDto<BookDto>> GetBooksAsync(
        [Query] BookSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of the current user's favorite books.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/books/favorites")]
    Task<PagedListDto<BookDto>> GetFavoriteBooksAsync(
        [Query] OrderedPagedRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific book by its ID.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/books/{id}")]
    Task<BookDto> GetBookAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific book by its ID with full API response.
    /// </summary>
    [Get("/api/books/{id}")]

    Task<IApiResponse<BookDto>> GetBookWithResponseAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all books for admin management (includes deleted books).
    /// </summary>
    [Get("/api/admin/books")]

    Task<ICollection<AdminBookDto>> GetAllBooksAdminAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new book (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Post("/api/admin/books")]
    Task<BookDto> CreateBookAsync([Body] CreateBookRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new book with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]
    [Post("/api/admin/books")]
    Task<IApiResponse<BookDto>> CreateBookWithResponseAsync([Body] CreateBookRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing book (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/books/{id}")]
    Task UpdateBookAsync(Guid id, [Body] UpdateBookRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing book with full API response (Admin only).
    /// </summary>
    [Headers("Content-Type: application/json")]

    [Put("/api/admin/books/{id}")]
    Task<IApiResponse> UpdateBookWithResponseAsync(Guid id, [Body] UpdateBookRequest body, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a book (Admin only).
    /// </summary>
    [Delete("/api/admin/books/{id}")]

    Task SoftDeleteBookAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a book with full API response (Admin only).
    /// </summary>
    [Delete("/api/admin/books/{id}")]

    Task<IApiResponse> SoftDeleteBookWithResponseAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a cover image for a book (Admin only).
    /// </summary>
    [Multipart]

    [Post("/api/admin/books/{id}/cover")]
    Task UploadBookCoverAsync(Guid id, StreamPart file, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a book to the user's favorites.
    /// </summary>
    [Headers("Accept: application/json")]

    [Post("/api/books/{id}/favorites")]
    Task AddBookToFavoritesAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book from the user's favorites.
    /// </summary>
    [Headers("Accept: application/json")]

    [Delete("/api/books/{id}/favorites")]
    Task RemoveBookFromFavoritesAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rates a book.
    /// </summary>
    [Post("/api/books/{id}/rating")]
    Task RateBookAsync(Guid id, [Body] RateBookRequest request, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a book rating.
    /// </summary>
    [Delete("/api/books/{id}/rating")]
    Task RemoveBookRatingAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a sale for a book.
    /// </summary>
    [Post("/api/books/{id}/sales")]
    Task ScheduleBookSaleAsync(Guid id, [Body] ScheduleSaleRequest request, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedules a sale for a book with full API response.
    /// </summary>
    [Post("/api/books/{id}/sales")]
    Task<IApiResponse> ScheduleBookSaleWithResponseAsync(Guid id, [Body] ScheduleSaleRequest request, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled sale for a book.
    /// </summary>
    [Delete("/api/books/{id}/sales")]
    Task CancelBookSaleAsync(Guid id, [Query] DateTimeOffset saleStart, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled sale for a book with full API response.
    /// </summary>
    [Delete("/api/books/{id}/sales")]
    Task<IApiResponse> CancelBookSaleWithResponseAsync(Guid id, [Query] DateTimeOffset saleStart, [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores a soft-deleted book (Admin only).
    /// </summary>
    [Post("/api/admin/books/{id}/restore")]
    Task RestoreBookAsync(Guid id, [Header("api-version")] string apiVersion = "1.0", [Header("If-Match")] string? etag = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific book by its ID for admin management.
    /// </summary>
    [Headers("Accept: application/json")]

    [Get("/api/admin/books/{id}")]
    Task<AdminBookDto> GetBookAdminAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific book by its ID for admin management with full API response.
    /// </summary>
    [Headers("Accept: application/json")]
    [Get("/api/admin/books/{id}")]
    Task<IApiResponse<AdminBookDto>> GetBookAdminWithResponseAsync(Guid id, CancellationToken cancellationToken = default);
}

public record RateBookRequest(int Rating);
public record ScheduleSaleRequest(decimal Percentage, DateTimeOffset Start, DateTimeOffset End);
