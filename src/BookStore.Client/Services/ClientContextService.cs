namespace BookStore.Client.Services;

/// <summary>
/// Scoped service to manage client context (Correlation, Causation, Browser Info) for the current session.
/// Thread-safe for concurrent access.
/// </summary>
public class ClientContextService
{
    readonly object _lock = new();

    /// <summary>
    /// Gets the correlation ID for the current business transaction/session.
    /// Initialized with a new Version 7 GUID.
    /// </summary>
    public string CorrelationId { get; } = Guid.CreateVersion7().ToString();

    /// <summary>
    /// Gets the causation ID for the next outgoing request.
    /// Initially same as CorrelationId, but can be updated based on incoming events.
    /// </summary>
    public string CausationId { get; private set; }

    public ClientContextService() => CausationId = CorrelationId;

    /// <summary>
    /// Updates the causation ID to link subsequent requests to a specific cause (e.g., an event ID).
    /// </summary>
    /// <param name="id">The new causation ID.</param>
    public void UpdateCausationId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        lock (_lock)
        {
            CausationId = id;
        }
    }

    public record BrowserInfo(string UserAgent, string Screen, string Language, string Timezone);

    public BrowserInfo? Browser { get; private set; }

    public void SetBrowserInfo(BrowserInfo info)
    {
        lock (_lock)
        {
            Browser = info;
        }
    }

    /// <summary>
    /// Resets the causation ID to the correlation ID.
    /// </summary>
    public void ResetCausationId()
    {
        lock (_lock)
        {
            CausationId = CorrelationId;
        }
    }
}
