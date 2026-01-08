using System.Net.ServerSentEvents;
using System.Text.Json;
using BookStore.Shared.Notifications;

namespace BookStore.Web.Services;

/// <summary>
/// Service to consume real-time domain event notifications via SSE.
/// Uses native .NET 10 SseParser for robust stream processing.
/// </summary>
public class BookStoreEventsService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BookStoreEventsService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public event Action<IDomainEventNotification>? OnNotificationReceived;

    public BookStoreEventsService(HttpClient httpClient, ILogger<BookStoreEventsService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public void StartListening()
    {
        if (_listenerTask != null) return;

        _cts = new CancellationTokenSource();
        _listenerTask = ListenToStreamAsync(_cts.Token);
    }

    private async Task ListenToStreamAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Use the relative path as the base address is already configured
                using var response = await _httpClient.GetAsync("/api/notifications/stream", HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                
                await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
                {
                    if (string.IsNullOrEmpty(item.Data)) continue;

                    try
                    {
                        var notification = DeserializeNotification(item.EventType, item.Data);
                        if (notification != null)
                        {
                            OnNotificationReceived?.Invoke(notification);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize SSE item: {EventType}", item.EventType);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in SSE stream. Retrying in 5 seconds...");
                await Task.Delay(5000, ct);
            }
        }
    }

    private IDomainEventNotification? DeserializeNotification(string eventType, string data)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        return eventType switch
        {
            "BookCreated" => JsonSerializer.Deserialize<BookCreatedNotification>(data, options),
            "BookUpdated" => JsonSerializer.Deserialize<BookUpdatedNotification>(data, options),
            "BookDeleted" => JsonSerializer.Deserialize<BookDeletedNotification>(data, options),
            "AuthorCreated" => JsonSerializer.Deserialize<AuthorCreatedNotification>(data, options),
            "CategoryCreated" => JsonSerializer.Deserialize<CategoryCreatedNotification>(data, options),
            "CategoryUpdated" => JsonSerializer.Deserialize<CategoryUpdatedNotification>(data, options),
            "CategoryDeleted" => JsonSerializer.Deserialize<CategoryDeletedNotification>(data, options),
            "CategoryRestored" => JsonSerializer.Deserialize<CategoryRestoredNotification>(data, options),
            "PublisherCreated" => JsonSerializer.Deserialize<PublisherCreatedNotification>(data, options),
            "BookCoverUpdated" => JsonSerializer.Deserialize<BookCoverUpdatedNotification>(data, options),
            "UserVerified" => JsonSerializer.Deserialize<UserVerifiedNotification>(data, options),
            _ => null
        };
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
