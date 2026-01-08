namespace BookStore.Shared.Notifications;

/// <summary>
/// Base interface for domain event notifications sent via real-time stream (SSE)
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
/// Notification when a category is updated
/// </summary>
public record CategoryUpdatedNotification(
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryUpdated";
}

/// <summary>
/// Notification when a category is deleted
/// </summary>
public record CategoryDeletedNotification(
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryDeleted";
}

/// <summary>
/// Notification when a category is restored
/// </summary>
public record CategoryRestoredNotification(
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryRestored";
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

/// <summary>
/// Notification when a book cover is updated
/// </summary>
public record BookCoverUpdatedNotification(
    Guid EntityId,
    string CoverUrl) : IDomainEventNotification
{
    public string EventType => "BookCoverUpdated";
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Notification when a user verifies their email
/// </summary>
public record UserVerifiedNotification(
    Guid EntityId,
    string Email,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "UserVerified";
}

/// <summary>
/// Notification when an author is updated
/// </summary>
public record AuthorUpdatedNotification(
    Guid EntityId,
    string Name,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "AuthorUpdated";
}

/// <summary>
/// Notification when an author is deleted
/// </summary>
public record AuthorDeletedNotification(
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "AuthorDeleted";
}

/// <summary>
/// Notification when a publisher is updated
/// </summary>
public record PublisherUpdatedNotification(
    Guid EntityId,
    string Name,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "PublisherUpdated";
}

/// <summary>
/// Notification when a publisher is deleted
/// </summary>
public record PublisherDeletedNotification(
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "PublisherDeleted";
}
