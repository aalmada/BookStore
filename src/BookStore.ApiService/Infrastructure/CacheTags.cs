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

    // Order tags
    public const string OrderItemPrefix = "order";
    public const string OrderList = "orders";

    // Security stamp tags
    public const string SecurityStampPrefix = "security-stamp";

    /// <summary>
    /// Creates a cache tag for a specific item by ID.
    /// </summary>
    public static string ForItem(string prefix, Guid id) => $"{prefix}:{id}";

    /// <summary>
    /// Creates a cache tag for a specific user's security stamp in a tenant.
    /// </summary>
    public static string ForSecurityStamp(string tenantId, Guid userId)
        => $"{SecurityStampPrefix}:{tenantId}:{userId:D}";
}
