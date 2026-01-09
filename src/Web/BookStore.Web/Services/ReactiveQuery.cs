using BookStore.Client;
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
    readonly QueryInvalidationService _invalidationService;
    readonly Action _onStateChanged;
    readonly ILogger _logger;
    readonly HashSet<string> _queryKeys;

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
        QueryInvalidationService invalidationService,
        IEnumerable<string> queryKeys,
        Action onStateChanged,
        ILogger logger)
    {
        _queryFn = queryFn;
        _eventsService = eventsService;
        _invalidationService = invalidationService;
        _queryKeys = [.. queryKeys];
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
        if (_invalidationService.ShouldInvalidate(notification, _queryKeys))
        {
            _logger.LogInformation("ReactiveQuery invalidating due to event: {EventType}. Matched Keys: {Keys}", notification.GetType().Name, string.Join(", ", _queryKeys));
            // Fire and forget reload
            _ = LoadAsync();
        }
    }

    /// <summary>
    /// Optimistically mutates the current data.
    /// Use this to apply local changes immediately while waiting for server confirmation.
    /// </summary>
    /// <param name="mutator">Function to transform the current data.</param>
    public void MutateData(Func<T, T> mutator)
    {
        if (Data == null)
        {
            return;
        }

        try
        {
            Data = mutator(Data);
            _onStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mutate data optimistically.");
        }
    }

    public void Dispose() => _eventsService.OnNotificationReceived -= HandleNotification;
}
