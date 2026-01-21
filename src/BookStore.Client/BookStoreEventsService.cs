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

    public event Action<IDomainEventNotification>? OnNotificationReceived;

    static readonly Dictionary<string, Type> _eventTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "BookCreated", typeof(BookCreatedNotification) },
        { "BookUpdated", typeof(BookUpdatedNotification) },
        { "BookDeleted", typeof(BookDeletedNotification) },
        { "AuthorCreated", typeof(AuthorCreatedNotification) },
        { "CategoryCreated", typeof(CategoryCreatedNotification) },
        { "CategoryUpdated", typeof(CategoryUpdatedNotification) },
        { "CategoryDeleted", typeof(CategoryDeletedNotification) },
        { "CategoryRestored", typeof(CategoryRestoredNotification) },
        { "PublisherCreated", typeof(PublisherCreatedNotification) },
        { "BookCoverUpdated", typeof(BookCoverUpdatedNotification) },
        { "UserVerified", typeof(UserVerifiedNotification) }
    };

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

    async Task ListenToStreamAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Use the relative path as the base address is already configured
                using var response = await _httpClient.GetAsync("/api/notifications/stream", HttpCompletionOption.ResponseHeadersRead, ct);
                _ = response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);

                await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
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
                            if (notification.EventId != Guid.Empty)
                            {
                                _clientContext.UpdateCausationId(notification.EventId.ToString());
                            }

                            OnNotificationReceived?.Invoke(notification);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.SseDeserializationFailed(_logger, ex, item.EventType);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.SseStreamError(_logger, ex);
                await Task.Delay(5000, ct);
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
