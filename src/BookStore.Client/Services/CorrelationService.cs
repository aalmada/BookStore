namespace BookStore.Client.Services;

/// <summary>
/// Scoped service to manage correlation and causation IDs for the current client session.
/// </summary>
public class CorrelationService
{
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

    public CorrelationService() => CausationId = CorrelationId;

    /// <summary>
    /// Updates the causation ID to link subsequent requests to a specific cause (e.g., an event ID).
    /// </summary>
    /// <param name="id">The new causation ID.</param>
    public void UpdateCausationId(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            CausationId = id;
        }
    }

    /// <summary>
    /// Resets the causation ID to the correlation ID.
    /// </summary>
    public void ResetCausationId() => CausationId = CorrelationId;
}
