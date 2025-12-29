namespace BookStore.Web.Services;

/// <summary>
/// Service for managing optimistic updates with eventual consistency reconciliation
/// </summary>
public class OptimisticUpdateService
{
    readonly Dictionary<Guid, OptimisticBook> _optimisticBooks = [];
    readonly object _lock = new();

    public event Action? OnBooksChanged;

    /// <summary>
    /// Add a book optimistically (before server confirmation)
    /// </summary>
    public void AddOptimisticBook(Guid id, string title, string? authorNames = null, string? publisherName = null)
    {
        lock (_lock)
        {
            _optimisticBooks[id] = new OptimisticBook
            {
                Id = id,
                Title = title,
                AuthorNames = authorNames ?? "Unknown",
                PublisherName = publisherName,
                IsOptimistic = true,
                AddedAt = DateTimeOffset.UtcNow
            };
        }

        OnBooksChanged?.Invoke();
    }

    /// <summary>
    /// Confirm an optimistic book with actual server data
    /// </summary>
    public void ConfirmBook(Guid id)
    {
        lock (_lock)
        {
            if (_optimisticBooks.ContainsKey(id))
            {
                _ = _optimisticBooks.Remove(id);
                OnBooksChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Get all optimistic books
    /// </summary>
    public List<OptimisticBook> GetOptimisticBooks()
    {
        lock (_lock)
        {
            return [.. _optimisticBooks.Values];
        }
    }

    /// <summary>
    /// Remove stale optimistic books (older than 30 seconds)
    /// </summary>
    public void CleanupStaleBooks()
    {
        lock (_lock)
        {
            var staleBooks = _optimisticBooks
                .Where(kvp => DateTimeOffset.UtcNow - kvp.Value.AddedAt > TimeSpan.FromSeconds(30))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in staleBooks)
            {
                _ = _optimisticBooks.Remove(id);
            }

            if (staleBooks.Count != 0)
            {
                OnBooksChanged?.Invoke();
            }
        }
    }

    /// <summary>
    /// Check if a book is optimistic
    /// </summary>
    public bool IsOptimistic(Guid id)
    {
        lock (_lock)
        {
            return _optimisticBooks.ContainsKey(id);
        }
    }
}

/// <summary>
/// Represents an optimistically added book
/// </summary>
public class OptimisticBook
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AuthorNames { get; set; }
    public string? PublisherName { get; set; }
    public DateOnly? PublishedDate { get; set; }
    public bool IsOptimistic { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
