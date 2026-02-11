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
        { "AuthorUpdated", typeof(AuthorUpdatedNotification) },
        { "AuthorDeleted", typeof(AuthorDeletedNotification) },
        { "CategoryCreated", typeof(CategoryCreatedNotification) },
        { "CategoryUpdated", typeof(CategoryUpdatedNotification) },
        { "CategoryDeleted", typeof(CategoryDeletedNotification) },
        { "CategoryRestored", typeof(CategoryRestoredNotification) },
        { "PublisherCreated", typeof(PublisherCreatedNotification) },
        { "PublisherUpdated", typeof(PublisherUpdatedNotification) },
        { "PublisherDeleted", typeof(PublisherDeletedNotification) },
        { "BookCoverUpdated", typeof(BookCoverUpdatedNotification) },
        { "UserVerified", typeof(UserVerifiedNotification) },
        { "UserUpdated", typeof(UserUpdatedNotification) },
        { "TenantCreated", typeof(TenantCreatedNotification) },
        { "TenantUpdated", typeof(TenantUpdatedNotification) },
        { "Ping", typeof(PingNotification) },
        { "Connected", typeof(PingNotification) },
        { "BookStatisticsUpdate", typeof(BookStatisticsUpdateNotification) },
        { "AuthorStatisticsUpdate", typeof(AuthorStatisticsUpdateNotification) },
        { "CategoryStatisticsUpdate", typeof(CategoryStatisticsUpdateNotification) },
        { "PublisherStatisticsUpdate", typeof(PublisherStatisticsUpdateNotification) }
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
                await Task.Delay(5000, token);
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
