# TUnit Attributes Reference

## Test declaration

```csharp
[Test]
public async Task MyTest() { … }
```

## Categorisation & filtering

```csharp
[Category("Unit")]
[Category("Integration")]
```

Filter on the command line:
```bash
dotnet test -- --treenode-filter "/*/*/*/*[Category=Integration]"
```

---

## Data-driven tests

```csharp
[Test]
[Arguments(1, 2, 3)]
[Arguments(10, 20, 30)]
public async Task Add_ReturnsExpected(int a, int b, int expected) { … }
```

See [data-driven.md](data-driven.md) for `MethodDataSource`, `ClassDataSource`, and
`Matrix` patterns.

---

## Lifecycle hooks

Hooks run at different scopes. The scope is passed as an argument to the attribute.

```csharp
// --- Instance hooks (per test) ---
[Before(Test)]
public async Task SetUp() { … }

[After(Test)]
public async Task TearDown() { … }

// --- Class-level (once per test class) ---
[Before(Class)]
public static async Task ClassSetUp(ClassHookContext context) { … }

[After(Class)]
public static async Task ClassTearDown(ClassHookContext context) { … }

// --- Assembly-level (once per assembly) ---
[Before(Assembly)]
public static async Task AssemblySetUp(AssemblyHookContext context) { … }

// --- Session-level (once per test run) ---
[Before(TestSession)]
public static async Task GlobalSetUp(TestSessionContext context) { … }
```

`[Before(Class)]` and above **must be `static`**. Non-static hooks cause a
compile-time error.

---

## Global hooks (run around every test)

```csharp
public static class Tracing
{
    [BeforeEvery(Test)]
    public static void Start(TestContext ctx)
        => Console.WriteLine($"Starting: {ctx.Metadata.TestName}");

    [AfterEvery(Test)]
    public static void Stop(TestContext ctx)
        => Console.WriteLine($"Done: {ctx.Metadata.TestName}");
}
```

---

## Retry & Repeat

```csharp
[Retry(3)]          // retry on failure, up to 3 times
[Repeat(5)]         // run 5 times regardless of outcome
```

Apply at assembly level for global retry:
```csharp
[assembly: Retry(3)]
```

---

## Parallelism control

```csharp
// Prevent a test from running in parallel with others sharing the same key
[NotInParallel("Database")]
public async Task ModifiesSharedDb() { … }

// Group tests that may run in parallel with each other
[ParallelGroup("ReadOnlyReads")]
public class ReadTests { … }

// Limit how many tests in a class run concurrently
public record ThreeConcurrent : IParallelLimit { public int Limit => 3; }

[ParallelLimiter<ThreeConcurrent>]
public class HeavyTests { … }
```

---

## Skip

```csharp
[Skip("Known flaky — tracked in #123")]
public async Task FlakyTest() { … }
```

---

## Test context injection

TUnit can inject `TestContext` (and `CancellationToken`) into test methods and hooks:

```csharp
[Test]
public async Task MyTest(TestContext context, CancellationToken ct)
{
    Console.WriteLine(context.Metadata.TestName);
    await SomeAsync(ct);
}

[After(Test)]
public async Task Cleanup(TestContext context)
{
    if (context.Execution.Result?.State == TestState.Failed)
        await CaptureScreenshotAsync();
}
```

---

## Dependency injection

Tests can receive DI-registered services as method parameters when using
`TUnit.Extensions.AspNetCore` or a custom `IServiceProvider` hook:

```csharp
[Test]
public async Task CanReadFromDb(IRepository repo)
{
    var item = await repo.GetAsync(id);
    await Assert.That(item).IsNotNull();
}
```
