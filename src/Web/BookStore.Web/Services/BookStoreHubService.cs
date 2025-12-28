using Microsoft.AspNetCore.SignalR.Client;

namespace BookStore.Web.Services;

/// <summary>
/// SignalR hub service for real-time notifications from the Book Store API
/// </summary>
public class BookStoreHubService : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly ILogger<BookStoreHubService> _logger;

    public event Action<BookNotification>? OnBookCreated;
    public event Action<BookNotification>? OnBookUpdated;
    public event Action<Guid>? OnBookDeleted;

    public BookStoreHubService(IConfiguration config, ILogger<BookStoreHubService> logger)
    {
        _logger = logger;
        
        var apiBaseUrl = config["services:apiservice:https:0"] 
            ?? config["services:apiservice:http:0"]
            ?? "https://localhost:7001";

        _connection = new HubConnectionBuilder()
            .WithUrl($"{apiBaseUrl}/hub/bookstore")
            .WithAutomaticReconnect()
            .Build();

        // Subscribe to book notifications
        _connection.On<BookNotification>("BookCreatedNotification", notification =>
        {
            _logger.LogInformation("Received BookCreated notification for {BookId}: {Title}", 
                notification.EntityId, notification.Title);
            OnBookCreated?.Invoke(notification);
        });

        _connection.On<BookNotification>("BookUpdatedNotification", notification =>
        {
            _logger.LogInformation("Received BookUpdated notification for {BookId}: {Title}", 
                notification.EntityId, notification.Title);
            OnBookUpdated?.Invoke(notification);
        });

        _connection.On<BookDeletedNotification>("BookDeletedNotification", notification =>
        {
            _logger.LogInformation("Received BookDeleted notification for {BookId}", 
                notification.EntityId);
            OnBookDeleted?.Invoke(notification.EntityId);
        });
    }

    public async Task StartAsync()
    {
        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("SignalR connection started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting SignalR connection");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}

/// <summary>
/// Book notification DTO matching the backend notification
/// </summary>
public record BookNotification(
    Guid EntityId,
    string Title,
    DateTimeOffset Timestamp);

/// <summary>
/// Book deleted notification DTO
/// </summary>
public record BookDeletedNotification(
    Guid EntityId,
    DateTimeOffset Timestamp);
