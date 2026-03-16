namespace BookStore.Web.Services;

public interface INotificationService
{
    event Action<NotificationMessage>? OnNotification;

    void Add(string message, NotificationSeverity severity);
}
