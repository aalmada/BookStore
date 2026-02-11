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
[JsonDerivedType(typeof(BookStatisticsUpdateNotification), "BookStatisticsUpdate")]
[JsonDerivedType(typeof(AuthorCreatedNotification), "AuthorCreated")]
[JsonDerivedType(typeof(AuthorUpdatedNotification), "AuthorUpdated")]
[JsonDerivedType(typeof(AuthorDeletedNotification), "AuthorDeleted")]
[JsonDerivedType(typeof(AuthorStatisticsUpdateNotification), "AuthorStatisticsUpdate")]
[JsonDerivedType(typeof(CategoryCreatedNotification), "CategoryCreated")]
[JsonDerivedType(typeof(CategoryUpdatedNotification), "CategoryUpdated")]
[JsonDerivedType(typeof(CategoryDeletedNotification), "CategoryDeleted")]
[JsonDerivedType(typeof(CategoryRestoredNotification), "CategoryRestored")]
[JsonDerivedType(typeof(CategoryStatisticsUpdateNotification), "CategoryStatisticsUpdate")]
[JsonDerivedType(typeof(PublisherCreatedNotification), "PublisherCreated")]
[JsonDerivedType(typeof(PublisherUpdatedNotification), "PublisherUpdated")]
[JsonDerivedType(typeof(PublisherDeletedNotification), "PublisherDeleted")]
[JsonDerivedType(typeof(PublisherStatisticsUpdateNotification), "PublisherStatisticsUpdate")]
[JsonDerivedType(typeof(BookCoverUpdatedNotification), "BookCoverUpdated")]
[JsonDerivedType(typeof(UserVerifiedNotification), "UserVerified")]
[JsonDerivedType(typeof(UserUpdatedNotification), "UserUpdated")]
[JsonDerivedType(typeof(TenantCreatedNotification), "TenantCreated")]
[JsonDerivedType(typeof(TenantUpdatedNotification), "TenantUpdated")]
public interface IDomainEventNotification
{
    Guid EventId { get; }
    Guid EntityId { get; }
    string EventType { get; }
    DateTimeOffset Timestamp { get; }
    long Version { get; }
}

/// <summary>
/// Notification when a book is created
/// </summary>
public record BookCreatedNotification(
    Guid EventId,
    Guid EntityId,
    string Title,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "BookUpdated";
}

/// <summary>
/// Notification when a book is deleted
/// </summary>
public record BookDeletedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "CategoryCreated";
}

/// <summary>
/// Notification when a category is updated
/// </summary>
public record CategoryUpdatedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "CategoryUpdated";
}

/// <summary>
/// Notification when a category is deleted
/// </summary>
public record CategoryDeletedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "CategoryDeleted";
}

/// <summary>
/// Notification when a category is restored
/// </summary>
public record CategoryRestoredNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "PublisherCreated";
}

/// <summary>
/// Notification when a book cover is updated
/// </summary>
public record BookCoverUpdatedNotification(
    Guid EventId,
    Guid EntityId,
    string? CoverUrl,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "AuthorUpdated";
}

/// <summary>
/// Notification when an author is deleted
/// </summary>
public record AuthorDeletedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "PublisherUpdated";
}

/// <summary>
/// Notification when a publisher is deleted
/// </summary>
public record PublisherDeletedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
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
    public long Version => 0;
}

/// <summary>
/// Notification when a user is updated
/// </summary>
public record UserUpdatedNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    int FavoritesCount = 0,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "UserUpdated";
}

/// <summary>
/// Notification when a tenant is created
/// </summary>
public record TenantCreatedNotification(
    Guid EventId,
    string EntityId,
    string Name,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "TenantCreated";
    Guid IDomainEventNotification.EntityId => Guid.Empty; // Tenants use string IDs, so we return Empty for the Guid property
}

/// <summary>
/// Notification when a tenant is updated
/// </summary>
public record TenantUpdatedNotification(
    Guid EventId,
    string EntityId,
    string Name,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "TenantUpdated";
    Guid IDomainEventNotification.EntityId => Guid.Empty;
}

/// <summary>
/// Notification when book statistics are updated
/// </summary>
public record BookStatisticsUpdateNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "BookStatisticsUpdate";
}

/// <summary>
/// Notification when category statistics are updated
/// </summary>
public record CategoryStatisticsUpdateNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "CategoryStatisticsUpdate";
}

/// <summary>
/// Notification when author statistics are updated
/// </summary>
public record AuthorStatisticsUpdateNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "AuthorStatisticsUpdate";
}

/// <summary>
/// Notification when publisher statistics are updated
/// </summary>
public record PublisherStatisticsUpdateNotification(
    Guid EventId,
    Guid EntityId,
    DateTimeOffset Timestamp,
    long Version = 0) : IDomainEventNotification
{
    public string EventType => "PublisherStatisticsUpdate";
}
