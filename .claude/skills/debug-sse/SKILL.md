---
name: debug-sse
description: Debug Server-Sent Events (SSE) notification issues when real-time updates aren't working. Use this when mutations don't trigger frontend updates.
---

Use this guide to troubleshoot Server-Sent Events (SSE) issues.

## Quick Path (80% of issues)

// turbo
1. **Test SSE endpoint**:
   ```bash
   curl -N -H "Accept: text/event-stream" http://localhost:5000/api/events
   ```
   Should see: `data: {"type":"Connected",...}`

2. **Check MartenCommitListener** has case for your projection
3. **Check QueryInvalidationService** maps notification to query keys
4. **Check ReactiveQuery** uses matching `queryKeys`

If still broken, continue to full debugging below.

---

## Symptoms

- ✗ Frontend doesn't update after mutation
- ✗ `ReactiveQuery` doesn't invalidate
- ✗ SSE connection fails or disconnects
- ✗ Events not received in browser

## Related Skills

**Prerequisites**:
- `/start-solution` - Solution must be running to debug SSE

**First Steps**:
- `/verify-feature` - Run basic checks (build, tests) before deep debugging

**Related Debugging**:
- `/debug-cache` - If issue seems cache-related instead of SSE
- `/doctor` - Check if environment setup is correct

**After Fixing**:
- `/verify-feature` - Confirm fix works
- `/scaffold-test` - Add tests to prevent regression

## Debugging Steps

### 1. Verify SSE Endpoint is Working

Test the SSE endpoint directly:

```bash
# Terminal 1: Connect to SSE stream
curl -N -H "Accept: text/event-stream" http://localhost:5000/api/events
```

You should see:
```
: connected

data: {"type":"Connected","timestamp":"2026-01-15T20:00:00Z"}
```

**If connection fails**:
- ✗ Check API service is running
- ✗ Verify `/api/events` endpoint exists in `EventsEndpoint.cs`
- ✗ Check firewall/network issues

### 2. Verify Notifications Are Defined

Check `src/Shared/BookStore.Shared/Notifications/DomainEventNotifications.cs`:

```csharp
// ✅ Correct - implements IDomainEventNotification
public record BookCreatedNotification(Guid Id, string Title) : IDomainEventNotification;

// ✗ Wrong - missing interface
public record BookCreatedNotification(Guid Id, string Title);
```

**If notification is missing**:
1. Create notification in `DomainEventNotifications.cs`
2. Implement `IDomainEventNotification` interface
3. Include all data needed for frontend invalidation

### 3. Verify MartenCommitListener Configuration

Open `src/BookStore.ApiService/Infrastructure/MartenCommitListener.cs`:

**Check if your projection has a handler**:

```csharp
private async Task ProcessDocumentChangeAsync(
    IDocumentChange change,
    CancellationToken cancellationToken)
{
    switch (change)
    {
        case BookProjection proj:
            await HandleBookChangeAsync(proj, cancellationToken);
            break;

        // ❌ Missing: Your projection case
        case AuthorProjection proj:
            await HandleAuthorChangeAsync(proj, cancellationToken);
            break;
    }
}
```

**Check if handler sends notification**:

```csharp
private async Task HandleBookChangeAsync(
    BookProjection book,
    CancellationToken cancellationToken)
{
    var notification = new BookUpdatedNotification(book.Id, book.Title);

    // ✅ Correct - sends notification
    await _notificationService.NotifyAsync(notification, cancellationToken);

    // ✗ Wrong - forgot to send
    // (no NotifyAsync call)
}
```

**If handler is missing**:
1. Add case for your projection
2. Create handler method that calls `NotifyAsync`
3. Use appropriate notification type

### 4. Verify QueryInvalidationService Mapping

Open `src/Web/BookStore.Web/Services/QueryInvalidationService.cs`:

**Check if notification maps to query keys**:

```csharp
public IEnumerable<string> GetInvalidationKeys(IDomainEventNotification notification)
{
    return notification switch
    {
        BookCreatedNotification => new[] { "Books" },
        BookUpdatedNotification => new[] { "Books" },

        // ❌ Missing: Your notification
        AuthorUpdatedNotification => new[] { "Authors" },

        _ => Array.Empty<string>()
    };
}
```

**If mapping is missing**:
1. Add case for your notification type
2. Return query keys that should be invalidated
3. Match keys used in `ReactiveQuery` setup

### 5. Verify Frontend EventsService

Check browser console for SSE connection:

**In Chrome DevTools**:
1. Open Network tab
2. Look for "events" request (type: eventsource)
3. Check status is "pending" (active connection)
4. View "EventStream" tab to see events

**If connection is closed**:
- Check `EventsService.StartListening()` is called in `OnInitializedAsync`
- Verify base URL is correct
- Check for JavaScript errors

### 6. Verify ReactiveQuery Configuration

Check component using `ReactiveQuery`:

```csharp
// ✅ Correct - query keys match invalidation mapping
bookQuery = new ReactiveQuery<PagedListDto<BookDto>>(
    queryFn: FetchBooksAsync,
    eventsService: EventsService,
    invalidationService: InvalidationService,
    queryKeys: new[] { "Books" },  // Matches QueryInvalidationService
    onStateChanged: StateHasChanged,
    logger: Logger
);

// ✗ Wrong - query keys don't match
queryKeys: new[] { "AllBooks" }  // Doesn't match "Books"
```

**If query doesn't invalidate**:
1. Ensure `queryKeys` match `QueryInvalidationService` mapping
2. Verify `EventsService` is subscribed
3. Check `onStateChanged` callback is provided

### 7. Test End-to-End

Perform a mutation and watch the flow:

```bash
# Terminal 1: Watch SSE stream
curl -N -H "Accept: text/event-stream" http://localhost:5000/api/events

# Terminal 2: Trigger mutation
curl -X POST http://localhost:5000/api/admin/books \
  -H "Content-Type: application/json" \
  -d '{"title":"Test Book",...}'
```

**Expected flow**:
1. Command executed
2. Event stored in Marten
3. `MartenCommitListener` triggered
4. Notification sent via SSE
5. Browser receives event
6. `QueryInvalidationService` maps to keys
7. `ReactiveQuery` invalidates
8. Query refetches
9. UI updates

**If any step fails**, locate where:
- Check logs in Aspire dashboard
- Add debug logging to `MartenCommitListener`
- Use browser console to see received events

## Common Issues & Fixes

### Issue: Events Not Sent

**Symptom**: MartenCommitListener not triggered

**Fix**:
- Ensure `MartenCommitListener` is registered in DI
- Check Marten event store configuration
- Verify projection lifecycle (`Inline` vs `Async`)

### Issue: Wrong Event Type

**Symptom**: Notification sent but frontend doesn't invalidate

**Fix**:
```csharp
// Check notification type name matches exactly
case "BookUpdatedNotification":  // ✅ Correct
case "BookUpdated":              // ✗ Wrong
```

### Issue: Multiple Tabs Don't Update

**Symptom**: Updates only visible in tab that made change

**Fix**:
- SSE works per-connection, each tab needs own connection
- Each tab should call `EventsService.StartListening()`
- Verify SignalR isn't being used (project uses SSE)

### Issue: SSE Connection Drops

**Symptom**: Connection works then stops

**Fix**:
- Check server-side timeout configuration
- Verify no proxy/load balancer kills long connections
- Add reconnection logic in `EventsService`

## Verification Checklist

- [ ] SSE endpoint accessible at `/api/events`
- [ ] Notification class implements `IDomainEventNotification`
- [ ] `MartenCommitListener` has handler for projection
- [ ] Handler calls `NotifyAsync` with notification
- [ ] `QueryInvalidationService` maps notification to keys
- [ ] Frontend `ReactiveQuery` uses matching query keys
- [ ] `EventsService.StartListening()` called on mount
- [ ] Browser DevTools shows active EventSource connection
- [ ] End-to-end test confirms UI updates after mutation

## Debugging Tools

**Backend**:
- Aspire Dashboard → Structured Logs → Filter by "notification"
- Add logging in `MartenCommitListener`: `_logger.LogInformation("Sending {Type}", notification.GetType().Name)`

**Frontend**:
- Browser Console → Look for EventSource logs
- React DevTools → Check component re-renders
- Network tab → Verify EventSource connection

## Related Skills

**First Steps**:
- `/verify-feature` - Run basic checks (build, tests) before deep debugging

**Related Debugging**:
- `/debug-cache` - If issue seems cache-related instead of SSE
- `/doctor` - Check if environment setup is correct

**After Fixing**:
- `/verify-feature` - Confirm fix works
- `/scaffold-test` - Add tests to prevent regression

**See Also**:
- [scaffold-write](../scaffold-write/SKILL.md) - SSE notification setup in MartenCommitListener
- [scaffold-frontend-feature](../scaffold-frontend-feature/SKILL.md) - Frontend SSE integration
- [real-time-notifications](../../../docs/guides/real-time-notifications.md) - SSE architecture and data flow
- ApiService AGENTS.md - Backend notification patterns
- Web AGENTS.md - Frontend SSE patterns
