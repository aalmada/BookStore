using BookStore.Web.Services.Models;
using Refit;

namespace BookStore.Web.Services;

/// <summary>
/// Refit interface for Book Store API
/// </summary>
public interface IBookStoreApi
{
    /// <summary>
    /// Get all books with optional search query and pagination
    /// </summary>
    [Get("/api/books")]
    Task<PagedListDto<BookDto>> GetBooksAsync(
        [Query] string? search = null,
        [Query] int page = 1,
        [Query] int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific book by ID
    /// </summary>
    [Get("/api/books/{id}")]
    Task<BookDto> GetBookAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paginated list of authors
    /// </summary>
    [Get("/api/authors")]
    Task<PagedListDto<AuthorDto>> GetAuthorsAsync(
        [Query] int page = 1,
        [Query] int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific author by ID
    /// </summary>
    [Get("/api/authors/{id}")]
    Task<AuthorDto> GetAuthorAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paginated list of categories with optional language
    /// </summary>
    [Get("/api/categories")]
    Task<object> GetCategoriesAsync(
        [Query] int page = 1,
        [Query] int pageSize = 20,
        [Header("Accept-Language")] string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific category by ID
    /// </summary>
    [Get("/api/categories/{id}")]
    Task<CategoryDto> GetCategoryAsync(
        Guid id,
        [Header("Accept-Language")] string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get paginated list of publishers
    /// </summary>
    [Get("/api/publishers")]
    Task<PagedListDto<PublisherDto>> GetPublishersAsync(
        [Query] int page = 1,
        [Query] int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific publisher by ID
    /// </summary>
    [Get("/api/publishers/{id}")]
    Task<PublisherDto> GetPublisherAsync(Guid id, CancellationToken cancellationToken = default);
}
