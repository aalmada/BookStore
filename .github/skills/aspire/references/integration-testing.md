# Aspire Integration Testing Reference

Aspire integration tests start the full Aspire application stack—containers, services, and infrastructure—then exercise it over real HTTP. No mocking, no manual container setup.

---

## NuGet Package

```xml
<PackageReference Include="Aspire.Hosting.Testing" />
```

Add a project reference to the AppHost from the test project:

```xml
<ProjectReference Include="../../src/YourApp.AppHost/YourApp.AppHost.csproj" />
```

---

## Project Setup

Useful global usings to add to the csproj:

```xml
<ItemGroup>
  <Using Include="System.Net" />
  <Using Include="Microsoft.Extensions.DependencyInjection" />
  <Using Include="Aspire.Hosting.ApplicationModel" />
  <Using Include="Aspire.Hosting.Testing" />
</ItemGroup>
```

---

## Bootstrapping the Application

Use `DistributedApplicationTestingBuilder` to start the entire stack. Scope setup to the test session using your test framework's session-level lifecycle hook so Aspire starts only once per test run.

```csharp
// GlobalSetup / TestSession lifecycle hook
var builder = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.YourApp_AppHost>([
        "--SomeFeature:Enabled=false",   // override config via CLI args
        "--RateLimit:Disabled=true"
    ]);

builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
    logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "[HH:mm:ss] "; });
});

App = await builder.BuildAsync();
await App.StartAsync();
```

What Aspire handles automatically:
- Container lifecycle (start, stop, dispose)
- Health checks and readiness probes
- Service-discovery connection strings
- Dependency ordering (e.g., API waits for database)

> [!IMPORTANT]
> Call `BuildAsync` only once; the builder is one-shot.

---

## Waiting for Resources to Be Healthy

Use `ResourceNotificationService` (or `app.ResourceNotifications`) to block until a resource is ready before running tests.

```csharp
// Retrieve from DI after building
NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();

// Wait for a named resource (add a timeout to prevent hung tests)
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
await NotificationService.WaitForResourceHealthyAsync("apiservice", cts.Token);
```

Use in infrastructure health tests:

```csharp
[Test]
[Arguments("postgres")]
[Arguments("cache")]
[Arguments("blobs")]
public async Task ResourceIsHealthy(string resourceName)
{
    await GlobalHooks.NotificationService!
        .WaitForResourceHealthyAsync(resourceName, CancellationToken.None)
        .WaitAsync(TimeSpan.FromSeconds(30));
}
```

---

## Creating HTTP Clients

`DistributedApplication.CreateHttpClient` resolves the service endpoint from Aspire's service discovery—no hardcoded URLs.

```csharp
// Plain HttpClient
var httpClient = App.CreateHttpClient("apiservice");
httpClient.DefaultRequestHeaders.Add("X-Tenant-ID", "myTenant");
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

// Typed client (Refit, etc.)
using Refit;
var client = RestService.For<IMyApiClient>(httpClient);
```

> [!NOTE]
> Set `client.Timeout` explicitly when making long-lived streaming requests (e.g., SSE) to prevent Aspire's default timeout from closing the connection mid-stream.

---

## Session-Scoped Shared State

Authenticate once at the start of the test session and share the token across all tests. This avoids errors caused by many parallel authentication requests hitting circuit breakers.

```csharp
public static class GlobalHooks
{
    public static DistributedApplication? App { get; private set; }
    public static ResourceNotificationService? NotificationService { get; private set; }
    public static string? AdminAccessToken { get; private set; }

    [Before(TestSession)]          // TUnit — use the equivalent for your framework
    public static async Task SetUp()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.YourApp_AppHost>();
        App = await builder.BuildAsync();
        NotificationService = App.Services.GetRequiredService<ResourceNotificationService>();
        await App.StartAsync();

        // Wait for API, then authenticate once
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        await NotificationService.WaitForResourceHealthyAsync("apiservice", cts.Token);
        AdminAccessToken = await AuthenticateAdminAsync();
    }
}
```

---

## Waiting for Async Side-Effects (SSE / Events)

When the system uses event sourcing or async projections, a command response (HTTP 201) does not mean the read model is updated yet. Connect to the event stream **before** issuing the command, then wait for the expected event.

Pattern:

1. Start listening to the SSE/notification stream.
2. Signal "connected" so the action can start.
3. Execute the command.
4. Wait for the expected event type to arrive.
5. Only then assert against the read model.

```csharp
public static async Task<bool> ExecuteAndWaitForEventAsync(
    Guid entityId,
    string eventType,
    Func<Task> action,
    TimeSpan timeout)
{
    var app = GlobalHooks.App!;
    using var client = app.CreateHttpClient("apiservice");
    client.Timeout = TimeSpan.FromMinutes(5); // prevent Aspire from closing the stream
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", GlobalHooks.AdminAccessToken);

    using var cts = new CancellationTokenSource(timeout);
    var tcs = new TaskCompletionSource<bool>();
    var connectedTcs = new TaskCompletionSource();

    var listenTask = Task.Run(async () =>
    {
        using var response = await client.GetAsync(
            "/api/notifications/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);
        response.EnsureSuccessStatusCode();
        connectedTcs.TrySetResult();

        using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        await foreach (var item in SseParser.Create(stream).EnumerateAsync(cts.Token))
        {
            if (item.EventType == eventType &&
                (entityId == Guid.Empty || item.Data?.Contains(entityId.ToString()) == true))
            {
                tcs.TrySetResult(true);
                return;
            }
        }
        tcs.TrySetResult(false);
    }, cts.Token);

    await connectedTcs.Task; // don't act until the stream is open
    await action();

    return await tcs.Task.WaitAsync(timeout);
}
```

Usage:

```csharp
var received = await ExecuteAndWaitForEventAsync(
    entityId,
    "BookCreated",
    async () => await httpClient.PostAsJsonAsync("/api/admin/books", createRequest),
    TimeSpan.FromSeconds(30));

Assert.That(received, Is.True);
var book = await client.GetFromJsonAsync<BookDto>($"/api/books/{entityId}");
```

> [!CAUTION]
> Never use `Task.Delay` or `Thread.Sleep` to wait for async events. Always subscribe to the event stream or use a `WaitForConditionAsync` helper.

---

## Fake Test Data (Bogus)

Generate realistic, varied test data with Bogus. Never use hardcoded or hand-rolled random strings.

```csharp
using Bogus;

static readonly Faker _faker = new();

public static CreateBookRequest GenerateFakeBookRequest() => new()
{
    Id = Guid.CreateVersion7(),
    Title = _faker.Commerce.ProductName(),
    Isbn = _faker.Commerce.Ean13(),
    Language = "en",
    Prices = new Dictionary<string, decimal>
    {
        ["USD"] = decimal.Parse(_faker.Commerce.Price(10, 100))
    }
};
```

---

## Per-Test Data Isolation

Each test must create its own data. Never rely on data left over from other tests or a global seed.

```csharp
[Test]
public async Task CreateEntity_EndToEndFlow_ShouldReturnOk()
{
    // Arrange — fresh data for this test only
    var client = await HttpClientHelpers.GetAuthenticatedClientAsync<IMyApiClient>();
    var request = FakeDataGenerators.GenerateFakeBookRequest();

    // Act
    var created = await MyHelpers.CreateEntityAsync(client, request);

    // Assert
    await Assert.That(created.Title).IsEqualTo(request.Title);
}
```

---

## Class-Level Setup

Use a class-scoped lifecycle hook for setup that is shared across tests in the same class but must not cross class boundaries.

```csharp
public class MultiTenancyTests
{
    static string _tenant1 = string.Empty;
    static string _tenant2 = string.Empty;

    [Before(Class)]
    public static async Task ClassSetup()
    {
        _tenant1 = FakeDataGenerators.GenerateFakeTenantId();
        _tenant2 = FakeDataGenerators.GenerateFakeTenantId();
        await DatabaseHelpers.CreateTenantViaApiAsync(_tenant1);
        await DatabaseHelpers.CreateTenantViaApiAsync(_tenant2);
    }
}
```

---

## Test Retry

Mark the assembly-level retry to handle transient failures in CI:

```csharp
[assembly: Retry(3)]
```

---

## Environment Variable Tests (No Full Startup)

To verify that resources resolve connection strings and environment variables correctly without starting all containers:

```csharp
[Test]
public async Task WebResource_HasApiServiceEnvVar()
{
    var builder = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.YourApp_AppHost>();
    var frontend = builder.CreateResourceBuilder<ProjectResource>("webfrontend");

    var config = await ExecutionConfigurationBuilder
        .Create(frontend.Resource)
        .WithEnvironmentVariablesConfig()
        .BuildAsync(
            new(DistributedApplicationOperation.Publish),
            NullLogger.Instance,
            CancellationToken.None);

    var envVars = config.EnvironmentVariables.ToDictionary();
    Assert.That(envVars, Does.ContainKey("APISERVICE_HTTPS"));
}
```

---

## Common Mistakes

| ❌ Wrong | ✅ Right |
|----------|----------|
| `Task.Delay` / `Thread.Sleep` for async commands | Subscribe to event stream before the command; await the event |
| Poll the read model after a command | Use `WaitForConditionAsync` or an SSE helper |
| Hardcoded port/URL in tests | `app.CreateHttpClient("resource-name")` |
| Authenticate per test in parallel tests | Authenticate once in session setup; share the token |
| Rely on a global seed for test data | Create all data inside each test |
| Skip timeout on `WaitForResourceHealthyAsync` | Always add `.WaitAsync(timeout)` |
| Call `BuildAsync` more than once | One builder → one `BuildAsync` call |
| Ignore `Retry` on flaky infrastructure tests | Use `[assembly: Retry(n)]` |

---

## Summary Checklist

- [ ] `Aspire.Hosting.Testing` NuGet package added
- [ ] AppHost project referenced from the test project
- [ ] `DistributedApplicationTestingBuilder.CreateAsync<TAppHost>` in session-scoped setup
- [ ] `WaitForResourceHealthyAsync` before first HTTP call
- [ ] `app.CreateHttpClient("resource-name")` for service-discovered clients
- [ ] Single shared auth token per session (no per-test auth)
- [ ] SSE subscription opened **before** issuing the command
- [ ] Bogus for all test data generation
- [ ] No `Task.Delay` / `Thread.Sleep`
- [ ] `[assembly: Retry(n)]` for flaky infra tolerance
