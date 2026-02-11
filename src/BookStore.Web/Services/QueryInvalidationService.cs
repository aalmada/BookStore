using BookStore.Shared.Notifications;

namespace BookStore.Web.Services;

/// <summary>
/// Service that defines the rules for which cache keys should be invalidated 
/// when specific domain events occur.
/// </summary>
public class QueryInvalidationService
{
    readonly ILogger<QueryInvalidationService> _logger;

    public QueryInvalidationService(ILogger<QueryInvalidationService> logger) => _logger = logger;

    /// <summary>
    /// Determines if a notification should invalidate any of the provided query keys.
    /// </summary>
    /// <param name="notification">The domain event notification.</param>
    /// <param name="queryKeys">The keys associated with the current query.</param>
    /// <returns>True if the query should be invalidated.</returns>
    public bool ShouldInvalidate(IDomainEventNotification notification, IEnumerable<string> queryKeys)
    {
        var keysToInvalidate = GetInvalidationKeys(notification);

        // Check if any of the keys produced by the event match the query's keys
        // We look for:
        // 1. Exact matches
        // 2. "Wildcard" matches where the event invalidates a parent key (e.g. "Books" invalidates "Book:123"?? No, usually the other way around or specific rules)
        // Actually, in React Query, we usually invalidate by matching keys.
        // Here, let's keep it simple: 
        // If the query listens to "Books" (list), and we return "Books", it matches.
        // If the query listens to "Book:123", and we return "Book:123", it matches.

        foreach (var key in keysToInvalidate)
        {
            if (queryKeys.Contains(key))
            {
                return true;
            }
        }

        return false;
    }

    IEnumerable<string> GetInvalidationKeys(IDomainEventNotification notification)
    {
        switch (notification)
        {
            case BookCreatedNotification n:
                yield return "Books";
                yield return $"Book:{n.EntityId}";
                break;
            case BookUpdatedNotification n:
                yield return "Books";
                yield return $"Book:{n.EntityId}";
                break;
            case BookDeletedNotification n:
                yield return "Books";
                yield return $"Book:{n.EntityId}";
                break;
            case BookCoverUpdatedNotification n:
                yield return "Books"; // Cover shown in list
                yield return $"Book:{n.EntityId}";
                break;
            case BookStatisticsUpdateNotification n:
                yield return "Books";
                yield return $"Book:{n.EntityId}";
                break;

            case AuthorCreatedNotification n:
                yield return "Authors";
                yield return $"Author:{n.EntityId}";
                break;
            case AuthorUpdatedNotification n:
                yield return "Authors";
                yield return "Books"; // Author name in book list might change?
                yield return $"Author:{n.EntityId}";
                break;
            case AuthorDeletedNotification n:
                yield return "Authors";
                yield return "Books";
                yield return $"Author:{n.EntityId}";
                break;
            case AuthorStatisticsUpdateNotification n:
                yield return "Authors";
                yield return $"Author:{n.EntityId}";
                break;

            case CategoryCreatedNotification:
            case CategoryUpdatedNotification:
            case CategoryDeletedNotification:
            case CategoryRestoredNotification:
            case CategoryStatisticsUpdateNotification:
                yield return "Categories";
                break;

            case PublisherCreatedNotification:
            case PublisherUpdatedNotification:
            case PublisherDeletedNotification:
            case PublisherStatisticsUpdateNotification:
                yield return "Publishers";
                break;

            // User specific
            case UserVerifiedNotification n:
                yield return $"User:{n.EntityId}";
                break;

            case UserUpdatedNotification n:
                yield return "Users";
                yield return $"User:{n.EntityId}";
                break;

            case TenantCreatedNotification:
            case TenantUpdatedNotification:
                yield return "Tenants";
                break;
        }
    }
}
