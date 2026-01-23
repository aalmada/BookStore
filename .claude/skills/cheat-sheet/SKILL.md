---
name: cheat-sheet
description: Quick reference for common patterns and code snippets. Use this when you need a fast lookup of project conventions.
---

# BookStore Cheat Sheet

## IDs & Timestamps
```csharp
var id = Guid.CreateVersion7();           // ✅ UUIDv7
var now = DateTimeOffset.UtcNow;          // ✅ UTC timestamp
```

## Event (past tense, record)
```csharp
public record BookAdded(Guid Id, string Title, decimal Price);
```

## Command (record)
```csharp
public record AddBookCommand(string Title, decimal Price);
```

## Aggregate Apply Method
```csharp
public void Apply(BookAdded @event)
{
    Id = @event.Id;
    Title = @event.Title;
}
```

## Handler (static, Wolverine)
```csharp
public static class AddBookHandler
{
    public static BookAdded Handle(AddBookCommand cmd) =>
        new(Guid.CreateVersion7(), cmd.Title, cmd.Price);
}
```

## HybridCache Query
```csharp
var result = await cache.GetOrCreateAsync(
    $"books:{culture}",
    async ct => await session.Query<BookProjection>().ToListAsync(ct),
    tags: [CacheTags.BookList],
    cancellationToken: ct);
```

## Cache Invalidation
```csharp
await cache.RemoveByTagAsync(CacheTags.BookList, ct);
```

## SSE Notification
```csharp
public record BookUpdatedNotification(Guid Id) : IDomainEventNotification;
```

## TUnit Test
```csharp
[Test]
public async Task Should_Create_Book()
{
    var result = await client.CreateBookAsync(request);
    await Assert.That(result.Id).IsNotNull();
}
```

## Namespace
```csharp
namespace BookStore.ApiService.Handlers;  // ✅ File-scoped
```

## Related Skills
- `/scaffold-write` - Full command implementation
- `/scaffold-read` - Full query implementation
- `/scaffold-aggregate` - Full aggregate setup
