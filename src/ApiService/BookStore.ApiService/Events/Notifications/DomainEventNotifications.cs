namespace BookStore.ApiService.Events.Notifications;

/// <summary>
/// Base interface for domain event notifications sent via SignalR
/// </summary>
public interface IDomainEventNotification
{
    Guid EntityId { get; }
    string EventType { get; }
    DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Notification when a book is created
/// </summary>
public record BookCreatedNotification(
    Guid EntityId,
    string Title,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "BookCreated";
}

/// <summary>
/// Notification when a book is updated
/// </summary>
public record BookUpdatedNotification(
    Guid EntityId,
    string Title,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "BookUpdated";
}

/// <summary>
/// Notification when a book is deleted
/// </summary>
public record BookDeletedNotification(
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "BookDeleted";
}

/// <summary>
/// Notification when an author is created
/// </summary>
public record AuthorCreatedNotification(
    Guid EntityId,
    string Name,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "AuthorCreated";
}

/// <summary>
/// Notification when a category is created
/// </summary>
public record CategoryCreatedNotification(
    Guid EntityId,
    string Name,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryCreated";
}

/// <summary>
/// Notification when a publisher is created
/// </summary>
public record PublisherCreatedNotification(
    Guid EntityId,
    string Name,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "PublisherCreated";
}
