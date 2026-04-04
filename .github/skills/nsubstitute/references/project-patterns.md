   # NSubstitute — BookStore Project Patterns

## HandlerTestBase

Most Wolverine handler tests inherit from `HandlerTestBase`, which centralises shared substitutes:

```csharp
public abstract class HandlerTestBase
{
    protected IDocumentSession Session { get; } = Substitute.For<IDocumentSession>();
    protected IHttpContextAccessor HttpContextAccessor { get; } = Substitute.For<IHttpContextAccessor>();
    protected HybridCache Cache { get; } = Substitute.For<HybridCache>();
    protected ILogger Logger { get; } = Substitute.For<ILogger>();
    protected ILogger<T> GetLogger<T>() => Substitute.For<ILogger<T>>();

    protected HandlerTestBase()
    {
        _ = HttpContextAccessor.HttpContext.Returns(new DefaultHttpContext());
        _ = Session.CorrelationId.Returns("test-correlation-id");
    }
}
```

Tests that need additional substitutes declare them as `readonly` fields:

```csharp
public class EmailHandlersTests
{
    readonly IEmailService _emailService = Substitute.For<IEmailService>();
    readonly ILogger<EmailHandlers> _logger = Substitute.For<ILogger<EmailHandlers>>();
    // ...
}
```

---

## Marten nested interfaces (IDocumentSession.Events)

Marten's `IDocumentSession` exposes `IEventStore` through a property. NSubstitute auto-creates a nested substitute for it, so you can stub it directly:

```csharp
// FetchStreamStateAsync — returns a StreamState or null
_ = Session.Events.FetchStreamStateAsync(id)
           .Returns(new StreamState { Version = 1 });

// AggregateStreamAsync — load a projected aggregate
_ = Session.Events.AggregateStreamAsync<BookAggregate>(id)
           .Returns(existingAggregate);

// Null aggregate (stream doesn't exist)
_ = Session.Events.AggregateStreamAsync<BookAggregate>(id)
           .Returns((BookAggregate?)null);

// FetchStreamAsync — raw event list
_ = Session.Events.FetchStreamAsync(id).Returns(events);

// FetchStreamStateAsync — not found (null)
_ = Session.Events
           .FetchStreamStateAsync(id)
           .Returns(Task.FromResult<StreamState?>(null));
```

Verifying events were appended:

```csharp
// StartStream (new aggregate)
_ = Session.Events.Received(1).StartStream<UserProfile>(
    userId,
    Arg.Is<UserProfileCreated>(e => e.UserId == userId));

// Append to existing stream
_ = Session.Events.Received(1).Append(
    userId,
    Arg.Is<BookUpdated>(e => e.Title == command.Title));

// Assert NOT called
_ = Session.Events.DidNotReceive().StartStream<UserProfile>(
    Arg.Any<Guid>(), Arg.Any<UserProfileCreated>());
```

---

## Discarding `.Returns()` results

Assign to `_ =` to satisfy the C# compiler (`.Returns()` returns the substitute itself, which has no meaningful use):

```csharp
// ✅ correct
_ = Session.Events.FetchStreamStateAsync(id).Returns(streamState);

// ❌ generates CS4014 / unused-value warning
Session.Events.FetchStreamStateAsync(id).Returns(streamState);
```

---

## ILogger substitutes

`ILogger` and `ILogger<T>` are interfaces, so they can be substituted directly. This project uses `[LoggerMessage]` source generators for logging, so you typically don't need to verify specific log calls — but if you do:

```csharp
var logger = Substitute.For<ILogger<MyHandler>>();

// Verify any log at a given level
logger.Received().Log(
    LogLevel.Warning,
    Arg.Any<EventId>(),
    Arg.Any<object>(),
    Arg.Any<Exception?>(),
    Arg.Any<Func<object, Exception?, string>>());
```

Verifying structured log messages is noisy; prefer testing observable side effects (return values, events, saved data) instead of log output.

---

## HybridCache substitutes

`HybridCache` is an abstract class, so substituion works but only virtual members are interceptable:

```csharp
protected HybridCache Cache { get; } = Substitute.For<HybridCache>();

// Stub GetOrCreateAsync to return a known value
_ = Cache.GetOrCreateAsync(
    "key",
    Arg.Any<CancellationToken>(),
    Arg.Any<HybridCacheEntryOptions?>(),
    Arg.Any<IReadOnlyList<string>?>())
    .Returns(myValue);

// Verify cache was invalidated
await Cache.Received(1).RemoveByTagAsync("books", Arg.Any<CancellationToken>());
```
