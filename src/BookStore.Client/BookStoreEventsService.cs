using System.Net.ServerSentEvents;
using System.Text.Json;
using BookStore.Client.Logging;
using BookStore.Shared.Notifications;
using Microsoft.Extensions.Logging;

namespace BookStore.Client;

/// <summary>
/// Service to consume real-time domain event notifications via SSE.
/// Uses native .NET 10 SseParser for robust stream processing.
/// </summary>
public class BookStoreEventsService : IAsyncDisposable
{
    readonly HttpClient _httpClient;
    readonly ILogger<BookStoreEventsService> _logger;
    readonly Services.ClientContextService _clientContext;
    CancellationTokenSource? _cts;
    Task? _listenerTask;

    /// <summary>
    /// Gets or sets the delay before attempting to reconnect to the SSE stream.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public event Action<IDomainEventNotification>? OnNotificationReceived;

    static readonly Dictionary<string, Type> _eventTypeMapping = InitializeEventTypeMapping();

    static Dictionary<string, Type> InitializeEventTypeMapping()
    {
        var mapping = typeof(IDomainEventNotification).Assembly.GetTypes()
            .Where(t => (t.IsClass || t.IsValueType) && !t.IsAbstract && typeof(IDomainEventNotification).IsAssignableFrom(t))
            .ToDictionary(
                t => t.Name.EndsWith("Notification") ? t.Name[..^"Notification".Length] : t.Name,
                t => t,
                StringComparer.OrdinalIgnoreCase);

        // Add special mappings/aliases
        mapping["Connected"] = typeof(PingNotification);

        return mapping;
    }

    public BookStoreEventsService(
        HttpClient httpClient,
        ILogger<BookStoreEventsService> logger,
        Services.ClientContextService clientContext)
    {
        _httpClient = httpClient;
        _logger = logger;
        _clientContext = clientContext;
    }

    public void StartListening()
    {
        if (_listenerTask != null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listenerTask = ListenToStreamAsync(_cts.Token);
    }

    async Task ListenToStreamAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                Log.SseStreamStarted(_logger, _httpClient.BaseAddress);

                // Use the absolute path if base address is set
                using var response = await _httpClient.GetAsync("/api/notifications/stream", HttpCompletionOption.ResponseHeadersRead, token);
                _ = response.EnsureSuccessStatusCode();

                Log.SseConnectionEstablished(_logger);

                using var stream = await response.Content.ReadAsStreamAsync(token);

                await foreach (var item in SseParser.Create(stream).EnumerateAsync(token))
                {
                    if (string.IsNullOrEmpty(item.Data))
                    {
                        continue;
                    }

                    try
                    {
                        var notification = DeserializeNotification(item.EventType, item.Data);
                        if (notification != null)
                        {
                            Log.SseEventReceived(_logger, notification.EventType, notification.EntityId);

                            if (notification.EventId != Guid.Empty)
                            {
                                _clientContext.UpdateCausationId(notification.EventId.ToString());
                            }

                            OnNotificationReceived?.Invoke(notification);
                        }
                        else
                        {
                            Log.SseDeserializationFailed(_logger, item.EventType, item.Data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.SseDeserializationFailed(_logger, item.EventType, item.Data);
                        Log.SseProcessingError(_logger, ex);
                    }
                }

                Log.SseEndOfStream(_logger);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                Log.SseListeningStopped(_logger);
                break;
            }
            catch (Exception ex)
            {
                Log.SseStreamError(_logger, ex);
                await Task.Delay(RetryDelay, token);
            }
        }
    }

    IDomainEventNotification? DeserializeNotification(string eventType, string data)
    {
        if (_eventTypeMapping.TryGetValue(eventType, out var type))
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize(data, type, options) as IDomainEventNotification;
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
        }

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException) { }
        }
    }
}
