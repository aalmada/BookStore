using System.Text.Json.Serialization;

namespace BookStore.Shared.Notifications;

/// <summary>
/// Base interface for domain event notifications sent via real-time stream (SSE)
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "NotificationType")]
[JsonDerivedType(typeof(PingNotification), "Ping")]
[JsonDerivedType(typeof(BookCreatedNotification), "BookCreated")]
[JsonDerivedType(typeof(BookUpdatedNotification), "BookUpdated")]
[JsonDerivedType(typeof(BookDeletedNotification), "BookDeleted")]
[JsonDerivedType(typeof(AuthorCreatedNotification), "AuthorCreated")]
[JsonDerivedType(typeof(AuthorUpdatedNotification), "AuthorUpdated")]
[JsonDerivedType(typeof(AuthorDeletedNotification), "AuthorDeleted")]
[JsonDerivedType(typeof(CategoryCreatedNotification), "CategoryCreated")]
[JsonDerivedType(typeof(CategoryUpdatedNotification), "CategoryUpdated")]
[JsonDerivedType(typeof(CategoryDeletedNotification), "CategoryDeleted")]
[JsonDerivedType(typeof(CategoryRestoredNotification), "CategoryRestored")]
[JsonDerivedType(typeof(PublisherCreatedNotification), "PublisherCreated")]
[JsonDerivedType(typeof(PublisherUpdatedNotification), "PublisherUpdated")]
[JsonDerivedType(typeof(PublisherDeletedNotification), "PublisherDeleted")]
[JsonDerivedType(typeof(BookCoverUpdatedNotification), "BookCoverUpdated")]
[JsonDerivedType(typeof(UserVerifiedNotification), "UserVerified")]
[JsonDerivedType(typeof(UserUpdatedNotification), "UserUpdated")]
public interface IDomainEventNotification
{
    Guid EventId { get; }
    Guid EntityId { get; }
    string EventType { get; }
    DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Notification when a book is created
/// </summary>
public record BookCreatedNotification(
    Guid EventId,
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
    Guid EventId,
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
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "BookDeleted";
}

/// <summary>
/// Notification when an author is created
/// </summary>
public record AuthorCreatedNotification(
    Guid EventId,
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
    Guid EventId,
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
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryUpdated";
}

/// <summary>
/// Notification when a category is deleted
/// </summary>
public record CategoryDeletedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryDeleted";
}

/// <summary>
/// Notification when a category is restored
/// </summary>
public record CategoryRestoredNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "CategoryRestored";
}

/// <summary>
/// Notification when a publisher is created
/// </summary>
public record PublisherCreatedNotification(
    Guid EventId,
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
    Guid EventId,
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
    Guid EventId,
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
    Guid EventId,
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
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "AuthorDeleted";
}

/// <summary>
/// Notification when a publisher is updated
/// </summary>
public record PublisherUpdatedNotification(
    Guid EventId,
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
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "PublisherDeleted";
}

/// <summary>
/// Notification for ping/connection keep-alive
/// </summary>
public record PingNotification : IDomainEventNotification
{
    public Guid EventId => Guid.Empty;
    public Guid EntityId => Guid.Empty;
    public string EventType => "Ping";
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

/// <summary>
/// Notification when a user is updated
/// </summary>
public record UserUpdatedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp) : IDomainEventNotification
{
    public string EventType => "UserUpdated";
}
