using BookStore.Client;
using BookStore.Shared.Notifications;
using BookStore.Web.Logging;

namespace BookStore.Web.Services;

/// <summary>
/// A reactive query wrapper that manages data fetching, caching, and automatic invalidation 
/// based on server-sent events. Analogous to "useQuery" in React.
/// </summary>
/// <typeparam name="T">The type of data being fetched.</typeparam>
public class ReactiveQuery<T> : IDisposable
{
    readonly Func<CancellationToken, Task<T>> _queryFn;
    readonly BookStoreEventsService _eventsService;
    readonly QueryInvalidationService _invalidationService;
    readonly Action _onStateChanged;
    readonly ILogger _logger;
    readonly HashSet<string> _queryKeys;
    CancellationTokenSource? _cts;

    /// <summary>
    /// Gets the current data.
    /// </summary>
    public T? Data { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the query is currently loading for the first time.
    /// </summary>
    public bool IsLoading { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether the query is currently fetching data (including background refreshes).
    /// </summary>
    public bool IsFetching { get; private set; }

    /// <summary>
    /// Gets the current error message, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the query produced an error.
    /// </summary>
    public bool IsError => Error != null;

    public ReactiveQuery(
        Func<CancellationToken, Task<T>> queryFn,
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

    long _version;
    bool _isDisposed;

    /// <summary>
    /// Executes the query function and updates the state.
    /// </summary>
    public async Task LoadAsync(bool silent = false, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        var currentVersion = Interlocked.Increment(ref _version);

        // Cancel previous load if any
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        // Create a new CTS for this load attempt, linked to the caller's token
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cts = cts;

        // Only set IsLoading to true if we don't have data, or if not silent.
        // This prevents flickering when refreshing data in the background.
        if (!silent || Data == null)
        {
            IsLoading = true;
        }

        IsFetching = true;
        Error = null;
        _onStateChanged();

        try
        {
            var data = await _queryFn(cts.Token);

            // Only update data if this is still the latest request
            if (Interlocked.Read(ref _version) == currentVersion)
            {
                Data = data;
                Error = null;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Ignore cancellation of our own load
            Log.QueryLoadCancelled(_logger, string.Join(", ", _queryKeys));
        }
        catch (Exception ex)
        {
            // Only update error if this is still the latest request
            if (Interlocked.Read(ref _version) == currentVersion)
            {
                Log.QueryLoadFailed(_logger, ex);
                Error = ex.Message;
            }
        }
        finally
        {
            // Only update state flags if this was the latest load attempt
            if (Interlocked.Read(ref _version) == currentVersion)
            {
                IsLoading = false;
                IsFetching = false;
                _onStateChanged();
            }
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
            Log.QueryInvalidating(_logger, notification.GetType().Name, string.Join(", ", _queryKeys));
            // Invalidate in the background (silent = true) to avoid UI flickering
            _ = LoadAsync(silent: true);
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
            Log.MutationFailed(_logger, ex);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _ = Interlocked.Increment(ref _version); // Ensure no more state updates

        _cts?.Cancel();
        _cts?.Dispose();
        _eventsService.OnNotificationReceived -= HandleNotification;
    }
}
