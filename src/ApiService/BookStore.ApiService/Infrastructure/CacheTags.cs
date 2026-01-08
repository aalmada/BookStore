namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Centralized cache tag constants used for HybridCache invalidation.
/// These tags must match between cache operations (GetOrCreate) and invalidation (RemoveByTag).
/// </summary>
public static class CacheTags
{
    // Category tags
    public const string CategoryItemPrefix = "category";
    public const string CategoryList = "categories";

    // Book tags
    public const string BookItemPrefix = "book";
    public const string BookList = "books";

    // Author tags
    public const string AuthorItemPrefix = "author";
    public const string AuthorList = "authors";

    // Publisher tags
    public const string PublisherItemPrefix = "publisher";
    public const string PublisherList = "publishers";

    /// <summary>
    /// Creates a cache tag for a specific item by ID.
    /// </summary>
    public static string ForItem(string prefix, Guid id) => $"{prefix}:{id}";
}
