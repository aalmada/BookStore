# Real-Time Notifications with SignalR

## Overview

The BookStore application uses **SignalR** integrated with **Wolverine** to provide real-time event notifications from the backend to all connected clients. This enables instant UI updates when data changes, creating a responsive and collaborative experience.

## Features

### Backend (API Service)
- **Wolverine SignalR Integration** - Native transport for publishing events
- **Event Forwarding** - Domain events automatically broadcast to clients
- **Transactional Guarantees** - Notifications only sent after successful commits
- **Cascading Messages** - Handlers return notifications that Wolverine publishes

### Frontend (Blazor Web)
- **SignalR Client** - Automatic reconnection and event handling
- **Optimistic Updates** - Instant UI feedback with eventual consistency
- **Event Reconciliation** - Confirms optimistic updates with server events
- **Visual Feedback** - Pulsing animation and "Saving..." badges

## How It Works

### 1. Event Flow

```
User Action (Create Book)
    ↓
Handler Processes Command
    ↓
Event Stored in Marten
    ↓
Handler Returns Notification (Cascading Message)
    ↓
Wolverine Publishes to SignalR (after commit)
    ↓
All Connected Clients Receive Event
    ↓
UI Updates Automatically
```

### 2. Backend Configuration

The API is configured to use SignalR transport in [`Program.cs`](file:///Users/antaoalmada/Projects/BookStore/src/ApiService/BookStore.ApiService/Program.cs):

```csharp
builder.Services.AddWolverine(opts =>
{
    // Enable SignalR transport
    opts.UseSignalR();
    
    // Route notifications to SignalR
    opts.Publish(x =>
    {
        x.MessagesImplementing<IDomainEventNotification>();
        x.ToSignalR();
    });
});

// Map SignalR hub
app.MapHub<WolverineHub>("/hub/bookstore");
```

### 3. Handler Implementation

Handlers return notifications as cascading messages:

```csharp
public static (IResult, BookCreatedNotification) Handle(
    CreateBook command, 
    IDocumentSession session)
{
    // Create book and store event
    var @event = BookAggregate.Create(...);
    session.Events.StartStream<BookAggregate>(command.Id, @event);
    
    // Create notification (published after transaction commits)
    var notification = new BookCreatedNotification(
        command.Id,
        command.Title,
        DateTimeOffset.UtcNow);
        
    return (Results.Created(...), notification);
}
```

### 4. Frontend Integration

The Blazor frontend connects to the SignalR hub and listens for events:

```csharp
// Subscribe to events
HubService.OnBookCreated += HandleBookCreated;

// Start connection
await HubService.StartAsync();

// Handle notifications
private void HandleBookCreated(BookNotification notification)
{
    // Confirm optimistic update
    OptimisticService.ConfirmBook(notification.EntityId);
    
    // Refresh UI
    await LoadBooksAsync();
}
```

## Optimistic Updates

The frontend implements **optimistic updates** to provide instant feedback while maintaining eventual consistency.

### How It Works

1. **Optimistic Addition** - Book appears immediately in UI
2. **Server Processing** - Backend processes command asynchronously
3. **SignalR Event** - Notification broadcast when complete
4. **Reconciliation** - Optimistic entry replaced with real data

### Visual Feedback

Optimistic books are displayed with:
- **Pulsing animation** (opacity fades in/out)
- **"Saving..." badge** with clock icon
- **Slightly transparent** appearance (85% opacity)

### Code Example

```csharp
// Add optimistic book (instant UI update)
OptimisticService.AddOptimisticBook(
    bookId, 
    "New Book Title", 
    "Author Name", 
    "Publisher Name");

// SignalR event arrives → automatically reconciled
// Optimistic entry removed, real data shown
```

### Stale Entry Cleanup

If a SignalR event never arrives (network issue, error):
- Cleanup timer runs every 5 seconds
- Removes optimistic books older than 30 seconds
- Prevents UI clutter from failed operations

## Benefits

### 1. Real-Time Collaboration
- Multiple users see changes instantly
- No manual refresh needed
- Consistent state across all clients

### 2. Instant Feedback
- Optimistic updates provide 0ms perceived latency
- Users see their actions immediately
- Better user experience

### 3. Eventual Consistency
- Backend uses async projections
- Optimistic UI bridges the gap
- SignalR events provide reconciliation

### 4. Reliable Delivery
- Wolverine's outbox pattern ensures delivery
- Transactional guarantees
- Automatic reconnection

## Infrastructure Requirements

### Development (Single Server)
- No additional infrastructure needed
- SignalR works out of the box

### Production (Multi-Server)
For scale-out deployments, you need a **backplane**:

#### Option 1: Redis Backplane
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(connectionString);
```

#### Option 2: Azure SignalR Service
```csharp
builder.Services.AddSignalR()
    .AddAzureSignalR(connectionString);
```

#### Option 3: Sticky Sessions
Configure load balancer for session affinity (not recommended).

## Testing

### Manual Testing

1. **Start the application**:
   ```bash
   aspire run
   ```

2. **Open two browser windows** to the book catalog

3. **Create a new book** via API or admin UI

4. **Observe**: Both windows update automatically

### Expected Behavior

- ✅ SignalR connection established on page load
- ✅ Book appears optimistically (pulsing)
- ✅ SignalR event received within ~100-500ms
- ✅ Optimistic entry replaced with real data
- ✅ All connected clients see the update

## Troubleshooting

### SignalR Connection Fails

Check browser console for errors:
```javascript
// Should see: "SignalR connection started successfully"
```

Verify hub endpoint is accessible:
```bash
curl https://localhost:7001/hub/bookstore
```

### Events Not Received

1. Check that handler returns notification
2. Verify `UseSignalR()` is configured
3. Check `ToSignalR()` routing
4. Review server logs for errors

### Optimistic Updates Not Clearing

1. Check SignalR connection is active
2. Verify event IDs match
3. Check cleanup timer is running
4. Review browser console for errors

## Learn More

- [Wolverine SignalR Integration](https://wolverinefx.net/guide/messaging/transports/signalr.html)
- [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/)
- [Optimistic UI Patterns](https://www.apollographql.com/docs/react/performance/optimistic-ui/)
