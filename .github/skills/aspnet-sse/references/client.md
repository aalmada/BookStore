# SSE Client Consumption

## .NET / Blazor client using SseParser

.NET 8+ ships `System.Net.ServerSentEvents.SseParser` for consuming SSE streams. Use `HttpCompletionOption.ResponseHeadersRead` so the response body streams instead of buffering:

```csharp
using System.Net.ServerSentEvents;

public class BookStoreEventsService : IAsyncDisposable
{
    readonly HttpClient _httpClient;
    CancellationTokenSource? _cts;
    Task? _listenerTask;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public event Action<IDomainEventNotification>? OnNotificationReceived;

    public void StartListening()
    {
        if (_listenerTask != null) return;
        _cts = new CancellationTokenSource();
        _listenerTask = ListenToStreamAsync(_cts.Token);
    }

    async Task ListenToStreamAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var response = await _httpClient.GetAsync(
                    "/api/notifications/stream",
                    HttpCompletionOption.ResponseHeadersRead,
                    token);
                _ = response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(token);

                await foreach (var item in SseParser.Create(stream).EnumerateAsync(token))
                {
                    if (string.IsNullOrEmpty(item.Data)) continue;

                    var notification = DeserializeNotification(item.EventType, item.Data);
                    if (notification != null)
                    {
                        // Propagate causation ID for correlated trace
                        if (notification.EventId != Guid.Empty)
                            _clientContext.UpdateCausationId(notification.EventId.ToString());

                        OnNotificationReceived?.Invoke(notification);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break; // Graceful shutdown
            }
            catch (Exception)
            {
                await Task.Delay(RetryDelay, token); // Reconnect after delay
            }
        }
    }

    IDomainEventNotification? DeserializeNotification(string eventType, string data)
    {
        // Map event type string to concrete type, then deserialize
        if (_eventTypeMapping.TryGetValue(eventType, out var notificationType))
            return (IDomainEventNotification?)JsonSerializer.Deserialize(data, notificationType);
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null) await _cts.CancelAsync();
        if (_listenerTask != null) await _listenerTask;
    }
}
```

## Event type → concrete type mapping

Build a dictionary at startup by reflecting over the assembly:

```csharp
static Dictionary<string, Type> InitializeEventTypeMapping() =>
    typeof(IDomainEventNotification).Assembly.GetTypes()
        .Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract
                 && typeof(IDomainEventNotification).IsAssignableFrom(t))
        .ToDictionary(
            t => t.Name.EndsWith("Notification")
                ? t.Name[..^"Notification".Length]
                : t.Name,
            t => t,
            StringComparer.OrdinalIgnoreCase);
```

Add special aliases for synthetic events (like the `Connected` ping):

```csharp
mapping["Connected"] = typeof(PingNotification);
```

## Blazor component integration

Subscribe to `OnNotificationReceived` in `OnInitializedAsync` and call `StateHasChanged` to re-render:

```csharp
@inject BookStoreEventsService Events
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        Events.OnNotificationReceived += HandleNotification;
        Events.StartListening();
    }

    void HandleNotification(IDomainEventNotification notification)
    {
        if (notification is BookCreatedNotification or BookUpdatedNotification or BookDeletedNotification)
        {
            // Refresh your data and re-render
            InvokeAsync(async () => { await LoadBooksAsync(); StateHasChanged(); });
        }
    }

    public void Dispose() => Events.OnNotificationReceived -= HandleNotification;
}
```

## JavaScript / Browser EventSource

```javascript
const eventSource = new EventSource('/api/notifications/stream');

// Subscribe to specific event types
eventSource.addEventListener('BookCreated', (event) => {
    const notification = JSON.parse(event.data);
    console.log(`Book ${notification.entityId} created: ${notification.title}`);
    refreshBookList();
});

eventSource.addEventListener('BookUpdated', (event) => { /* ... */ });
eventSource.addEventListener('BookDeleted', (event) => { /* ... */ });

// EventSource auto-reconnects — no explicit retry needed
eventSource.onerror = (err) => console.warn('SSE error, will reconnect', err);
```

> **Multi-tenancy**: If the API requires a tenant header, plain `EventSource` can't set custom headers. Use `fetch` with `ReadableStream` instead, or pass tenant information as a query parameter.

## Reconnect and Last-Event-ID

When `SseItem` includes `EventId`, the browser sends `Last-Event-ID` on reconnect. Server-side you can use this to replay missed events — though this project currently doesn't implement replay; clients simply re-fetch on reconnect.
