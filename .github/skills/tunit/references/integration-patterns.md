# TUnit Integration Test Patterns Reference

Integration tests in TUnit work exactly like unit tests — same `[Test]` attribute,
same assertion API. The difference is in setup: integration tests start real
infrastructure (database, HTTP server, message bus, etc.) via lifecycle hooks.

---

## Global session setup with [Before(TestSession)]

Start expensive infrastructure once for the entire test run:

```csharp
public static class GlobalHooks
{
    public static MyApp? App { get; private set; }

    [Before(TestSession)]
    public static async Task StartApp()
    {
        App = await MyApp.StartAsync();
    }

    [After(TestSession)]
    public static async Task StopApp()
    {
        if (App is not null)
            await App.DisposeAsync();
    }
}
```

---

## Aspire integration (DistributedApplicationTestingBuilder)

If your project uses .NET Aspire, use `DistributedApplicationTestingBuilder`
to start the whole distributed app in tests:

```csharp
[Before(TestSession)]
public static async Task SetUp()
{
    var builder = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.MyApp_AppHost>();

    App = await builder.BuildAsync();
    await App.StartAsync();
}
```

Then create HTTP clients pointed at named services:

```csharp
var client = App.CreateHttpClient("apiservice");
```

---

## Class-level setup (per-test-class infrastructure)

```csharp
public class OrderTests
{
    private static IOrdersClient _client = null!;

    [Before(Class)]
    public static async Task SetUpClient()
    {
        var token = await AuthHelper.GetTokenAsync();
        _client = RestService.For<IOrdersClient>(
            HttpClientHelpers.GetAuthenticatedClient(token));
    }
}
```

---

## Waiting for eventual consistency

Never use `Task.Delay` or `Thread.Sleep` to wait for async side effects.
Instead, poll with a timeout:

```csharp
// Generic helper pattern — adapt to your project
public static async Task WaitForConditionAsync(
    Func<Task<bool>> condition,
    TimeSpan timeout,
    TimeSpan? interval = null)
{
    var deadline = DateTimeOffset.UtcNow + timeout;
    var poll = interval ?? TimeSpan.FromMilliseconds(200);
    while (DateTimeOffset.UtcNow < deadline)
    {
        if (await condition()) return;
        await Task.Delay(poll);
    }
    throw new TimeoutException("Condition not met within timeout.");
}
```

Usage:
```csharp
await WaitForConditionAsync(
    async () => (await client.GetOrderAsync(id)).Status == "Shipped",
    TimeSpan.FromSeconds(10));
```

---

## Generating test data with Bogus

Bogus produces realistic, reproducible fake data. It avoids hand-rolled magic
strings and makes test intent clearer.

```csharp
using Bogus;

public static class Fakes
{
    static readonly Faker _f = new();

    public static CreateOrderRequest Order() => new()
    {
        Id        = Guid.CreateVersion7(),
        ProductId = Guid.CreateVersion7(),
        Quantity  = _f.Random.Int(1, 10),
        Note      = _f.Lorem.Sentence()
    };
}
```

Use in tests:
```csharp
var request = Fakes.Order();
var created = await client.CreateOrderAsync(request);
await Assert.That(created.Id).IsEqualTo(request.Id);
```

---

## Mocking with NSubstitute

For unit tests, prefer NSubstitute over Moq or hand-written fakes:

```csharp
var repo = Substitute.For<IOrderRepository>();
repo.GetAsync(Arg.Any<Guid>()).Returns(Task.FromResult<Order?>(null));

var handler = new GetOrderHandler(repo);
var result = await handler.HandleAsync(new GetOrderQuery(Guid.CreateVersion7()));

await Assert.That(result).IsNull();
await repo.Received(1).GetAsync(Arg.Any<Guid>());
```

---

## Parallelism in integration tests

By default TUnit runs tests in parallel. Integration tests that share state
(e.g., a single database) need explicit coordination:

```csharp
// All tests in this class share a "Database" non-parallelism key
[NotInParallel("Database")]
public class DatabaseMutationTests { … }
```

Or limit concurrency with a parallel limiter:

```csharp
public record TwoBrowsers : IParallelLimit { public int Limit => 2; }

[ParallelLimiter<TwoBrowsers>]
public class BrowserTests { … }
```

---

## Test isolation checklist

- [ ] Each test creates its own data — no dependency on shared seeded rows
- [ ] IDs are generated per-test (`Guid.CreateVersion7()`)
- [ ] Shared resources accessed via `[Before(Class)]` or `[Before(TestSession)]`
- [ ] No `Task.Delay` — use condition polling helpers
- [ ] Mutable static state is reset between tests where necessary
