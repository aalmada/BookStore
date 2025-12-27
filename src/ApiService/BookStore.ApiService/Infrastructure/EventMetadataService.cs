namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Metadata for event tracking and distributed tracing
/// </summary>
public class EventMetadata
{
    /// <summary>
    /// Unique identifier for this specific event
    /// </summary>
    public Guid EventId { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Correlation ID - tracks the entire business transaction across services
    /// Remains the same throughout the entire workflow
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Causation ID - tracks the immediate cause of this event
    /// Usually the ID of the command or event that triggered this event
    /// </summary>
    public string CausationId { get; set; } = string.Empty;

    /// <summary>
    /// User who initiated the action
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Timestamp when the event was created
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Service to manage correlation and causation IDs from HTTP context
/// </summary>
public class EventMetadataService
{
    readonly IHttpContextAccessor _httpContextAccessor;

    public EventMetadataService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public EventMetadata CreateMetadata()
    {
        var context = _httpContextAccessor.HttpContext;
        
        // Get or create correlation ID from header
        var correlationId = context?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.CreateVersion7().ToString();

        // Get causation ID from header (usually the previous event/command ID)
        var causationId = context?.Request.Headers["X-Causation-ID"].FirstOrDefault()
            ?? correlationId; // If no causation, use correlation as root

        // Get user ID from claims (if authenticated)
        var userId = context?.User?.Identity?.Name;

        return new EventMetadata
        {
            CorrelationId = correlationId,
            CausationId = causationId,
            UserId = userId
        };
    }

    public void SetResponseHeaders(EventMetadata metadata)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            context.Response.Headers["X-Correlation-ID"] = metadata.CorrelationId;
            context.Response.Headers["X-Event-ID"] = metadata.EventId.ToString();
        }
    }
}
