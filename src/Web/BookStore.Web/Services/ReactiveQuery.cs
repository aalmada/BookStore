using BookStore.Shared.Notifications;

namespace BookStore.Web.Services;

/// <summary>
/// A reactive query wrapper that manages data fetching, caching, and automatic invalidation 
/// based on server-sent events. Analogous to "useQuery" in React.
/// </summary>
/// <typeparam name="T">The type of data being fetched.</typeparam>
public class ReactiveQuery<T> : IDisposable
{
    readonly Func<Task<T>> _queryFn;
    readonly BookStoreEventsService _eventsService;
    readonly Func<IDomainEventNotification, bool> _invalidationPredicate;
    readonly Action _onStateChanged;
    readonly ILogger _logger;

    /// <summary>
    /// Gets the current data.
    /// </summary>
    public T? Data { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the query is currently loading.
    /// </summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Gets the current error message, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the query produced an error.
    /// </summary>
    public bool IsError => Error != null;

    public ReactiveQuery(
        Func<Task<T>> queryFn,
        BookStoreEventsService eventsService,
        Func<IDomainEventNotification, bool> invalidationPredicate,
        Action onStateChanged,
        ILogger logger)
    {
        _queryFn = queryFn;
        _eventsService = eventsService;
        _invalidationPredicate = invalidationPredicate;
        _onStateChanged = onStateChanged;
        _logger = logger;

        // Subscribe to events immediately
        _eventsService.OnNotificationReceived += HandleNotification;
    }

    /// <summary>
    /// Executes the query function and updates the state.
    /// </summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        _onStateChanged();

        try
        {
            Data = await _queryFn();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReactiveQuery failed to load data.");
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
            _onStateChanged();
        }
    }

    /// <summary>
    /// Manually invalidates the query, causing it to reload.
    /// </summary>
    public async Task InvalidateAsync() => await LoadAsync();

    void HandleNotification(IDomainEventNotification notification)
    {
        if (_invalidationPredicate(notification))
        {
            _logger.LogInformation("ReactiveQuery invalidating due to event: {EventType}", notification.GetType().Name);
            // Fire and forget reload
            _ = LoadAsync();
        }
    }

    public void Dispose() => _eventsService.OnNotificationReceived -= HandleNotification;
}
