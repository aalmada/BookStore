using System.Net.ServerSentEvents;
using BookStore.ApiService.Infrastructure.Logging;
using BookStore.ApiService.Infrastructure.Notifications;
using BookStore.Shared.Notifications;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookStore.ApiService.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/stream", GetNotificationStream)
            .WithName("GetNotificationStream")
            .WithSummary("Subscribe to real-time notifications via SSE")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");

        _ = group.MapPost("/test-notification", async (
            string type,
            Guid id,
            INotificationService service,
            BookStore.ApiService.Infrastructure.Tenant.ITenantContext tenantContext) =>
        {
            IDomainEventNotification notification = type.ToLowerInvariant() switch
            {
                "author" => new AuthorUpdatedNotification(Guid.CreateVersion7(), id, "Test Author", DateTimeOffset.UtcNow),
                "book" => new BookUpdatedNotification(Guid.CreateVersion7(), id, "Test Book", DateTimeOffset.UtcNow),
                "category" => new CategoryUpdatedNotification(Guid.CreateVersion7(), id, DateTimeOffset.UtcNow),
                "publisher" => new PublisherUpdatedNotification(Guid.CreateVersion7(), id, "Test Publisher", DateTimeOffset.UtcNow),
                _ => new PingNotification()
            };

            await service.NotifyAsync(notification, tenantContext.TenantId);
            return Results.Ok($"Sent {notification.EventType} for {id}");
        })
        .WithName("SendTestNotification")
        .WithSummary("Manually trigger a notification for debugging");

        return group;
    }

    static IResult GetNotificationStream(
        INotificationService notificationService,
        BookStore.ApiService.Infrastructure.Tenant.ITenantContext tenantContext,
        ILogger<INotificationService> logger,
        CancellationToken cancellationToken)
    {
        Log.Notifications.ClientConnected(logger);

        Log.Notifications.CreatingSubscription(logger);
        var stream = notificationService.Subscribe(tenantContext.TenantId, cancellationToken);

        // Wrap stream to send immediate initial event to force header flush
        var streamWithInit = EmitInitialEvent(stream);

        Log.Notifications.SubscriptionCreated(logger);

        return TypedResults.ServerSentEvents(streamWithInit);
    }

    static async IAsyncEnumerable<SseItem<IDomainEventNotification>> EmitInitialEvent(IAsyncEnumerable<SseItem<IDomainEventNotification>> source)
    {
        yield return new SseItem<IDomainEventNotification>(new PingNotification(), "Connected");
        await foreach (var item in source)
        {
            yield return item;
        }
    }
}
