namespace BookStore.Web.Services;

public sealed record NotificationMessage(string Message, NotificationSeverity Severity, DateTimeOffset CreatedAtUtc);
