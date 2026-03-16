namespace BookStore.Web.Services;

public sealed class NotificationService : INotificationService
{
    public event Action<NotificationMessage>? OnNotification;

    public void Add(string message, NotificationSeverity severity)
        => OnNotification?.Invoke(new NotificationMessage(message, severity, DateTimeOffset.UtcNow));
}
