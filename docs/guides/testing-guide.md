# Testing Guide

This guide covers **unit testing** practices and tools used in the Book Store project.

> [!NOTE]
> This guide focuses on **unit testing** with TUnit. For **integration testing** (end-to-end tests with Aspire), see the [Integration Testing Guide](integration-testing-guide.md).

## Testing Framework

The project uses **TUnit**, a modern testing framework for .NET that provides:

- **Source-Generated Tests** - Compile-time test discovery for faster execution
- **Parallel Execution** - Tests run in parallel by default for improved performance
- **Built-in Code Coverage** - No need for additional packages like Coverlet
- **Fluent Assertions** - Modern async-first assertion syntax
- **Dependency Injection** - Native support for injecting dependencies into test methods
- **Microsoft.Testing.Platform** - Integration with .NET 10's new testing platform

## Running Tests

### Command Line

```bash
# Run all tests (recommended)
dotnet test

# Run tests for specific project
dotnet test --project tests/ApiService/BookStore.ApiService.UnitTests/BookStore.ApiService.UnitTests.csproj

# Run tests directly (alternative method)
dotnet run --project tests/ApiService/BookStore.ApiService.UnitTests/BookStore.ApiService.UnitTests.csproj
```

### IDE Support

TUnit works with all major .NET IDEs:

- **Visual Studio 2022** (17.13+) - Fully supported, no additional configuration needed
- **JetBrains Rider** - Enable "Testing Platform support" in Settings → Build, Execution, Deployment → Unit Testing
- **Visual Studio Code** - Install C# Dev Kit and enable "Use Testing Platform Protocol"

## Test Structure

### Test Files

```
tests/ApiService/BookStore.ApiService.UnitTests/
├── Handlers/
│   ├── BookHandlerTests.cs          # Book command handler tests
│   ├── AuthorHandlerTests.cs        # Author command handler tests
│   ├── CategoryHandlerTests.cs      # Category command handler tests
│   └── PublisherHandlerTests.cs     # Publisher command handler tests
├── Infrastructure/
│   ├── CultureCacheTests.cs         # Culture caching tests
│   └── LocalizationHelperTests.cs   # Localization helper tests
└── JsonSerializationTests.cs        # JSON standards verification
```

### Test Anatomy

```csharp
using TUnit.Core;
using TUnit.Assertions;

public class BookHandlerTests
{
    [Test]
    public async Task CreateBookHandler_ShouldStartStreamWithBookAddedEvent()
    {
        // Arrange
        var command = new CreateBook(...);
        var session = Substitute.For<IDocumentSession>();
        
        // Act
        var result = BookHandlers.Handle(command, session);
        
        // Assert
        _ = await Assert.That(result).IsNotNull();
        session.Events.Received(1).StartStream<BookAggregate>(...);
    }
}
```

## TUnit Assertions

TUnit uses a fluent, async-first assertion syntax:

### Basic Assertions

```csharp
// Equality
_ = await Assert.That(actual).IsEqualTo(expected);
_ = await Assert.That(actual).IsNotEqualTo(unexpected);

// Null checks
_ = await Assert.That(value).IsNotNull();
_ = await Assert.That(value).IsNull();

// Boolean
_ = await Assert.That(condition).IsTrue();
_ = await Assert.That(condition).IsFalse();

// Type checks
_ = await Assert.That(result).IsTypeOf<ExpectedType>();
_ = await Assert.That(result).IsNotTypeOf<UnexpectedType>();
```

### Collection Assertions

```csharp
// Contains
_ = await Assert.That(collection).Contains(item);
_ = await Assert.That(collection).DoesNotContain(item);

// Empty/Not Empty
_ = await Assert.That(collection).IsEmpty();
_ = await Assert.That(collection).IsNotEmpty();

// Count
_ = await Assert.That(collection).Count().IsEqualTo(3);
```

### String Assertions

```csharp
// Contains
_ = await Assert.That(text).Contains("substring");
_ = await Assert.That(text).DoesNotContain("missing");

// Starts/Ends With
_ = await Assert.That(text).StartsWith("prefix");
_ = await Assert.That(text).EndsWith("suffix");

// Regex
_ = await Assert.That(text).Matches(@"pattern");
```

### Exception Assertions

```csharp
// Synchronous
await Assert.ThrowsAsync<ArgumentException>(() => 
    throw new ArgumentException("message"));

// Asynchronous
await Assert.ThrowsAsync<InvalidOperationException>(async () => 
    await SomeAsyncMethod());
```

## Test Categories

### 1. Unit Tests (Handler Tests)

Test individual command handlers in isolation using mocked dependencies.

**Example**: [BookHandlerTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/ApiService/BookStore.ApiService.UnitTests/Handlers/BookHandlerTests.cs)

```csharp
[Test]
public async Task UpdateBookHandler_WithMissingBook_ShouldReturnNotFound()
{
    // Arrange
    var command = new UpdateBook(...);
    var session = Substitute.For<IDocumentSession>();
    
    // Stream doesn't exist
    session.Events.FetchStreamStateAsync(command.Id)
        .Returns(Task.FromResult<StreamState?>(null));
    
    // Act
    var result = await BookHandlers.Handle(command, session, context);
    
    // Assert
    _ = await Assert.That(result).IsTypeOf<NotFound>();
}
```

### 2. JSON Serialization Tests

Verify that the API follows JSON standards (ISO 8601, camelCase, etc.).

**Example**: [JsonSerializationTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/ApiService/BookStore.ApiService.UnitTests/JsonSerializationTests.cs)

```csharp
[Test]
public async Task DateTimeOffset_Should_Serialize_As_ISO8601_With_UTC()
{
    var testObject = new { Timestamp = new DateTimeOffset(2025, 12, 26, 17, 16, 9, 123, TimeSpan.Zero) };
    var json = JsonSerializer.Serialize(testObject, _options);
    
    _ = await Assert.That(json).Contains("\"timestamp\":\"2025-12-26T17:16:09.123+00:00\"");
}
```

### 3. Integration Tests

The project has two types of integration tests:

1.  **Workload Integration Tests (`BookStore.ApiService.IntegrationTests`)**: Tests the API service behavior with real dependencies (DB, Cache) orchestrated by Aspire.
2.  **AppHost Tests (`BookStore.AppHost.Tests`)**: Verifies the entire distributed application startup and connectivity.

> [!TIP]
> For comprehensive integration testing patterns, strategies, and best practices, see the [Integration Testing Guide](integration-testing-guide.md).

**Example**: [WebTests.cs](file:///Users/antaoalmada/Projects/BookStore/tests/BookStore.AppHost.Tests/WebTests.cs)

```csharp
[Test]
public async Task GetWebResourceRootReturnsOkStatusCode(CancellationToken cancellationToken)
{
    var appHost = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.BookStore_AppHost>(cancellationToken);
    
    await using var app = await appHost.BuildAsync(cancellationToken);
    await app.StartAsync(cancellationToken);
    
    var httpClient = app.CreateHttpClient("webfrontend");
    var response = await httpClient.GetAsync("/", cancellationToken);
    
    _ = await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
}
```

## Dependency Injection in Tests

TUnit supports dependency injection for test methods:

```csharp
[Test]
public async Task MyTest(CancellationToken cancellationToken)
{
    // CancellationToken is automatically injected by TUnit
    await SomeAsyncOperation(cancellationToken);
}
```

## Mocking with NSubstitute

The project uses NSubstitute for mocking, which works seamlessly with TUnit:

```csharp
// Create mock
var session = Substitute.For<IDocumentSession>();

// Setup return values
session.CorrelationId.Returns("test-correlation-id");
session.Events.FetchStreamStateAsync(id).Returns(Task.FromResult<StreamState?>(null));

// Verify calls
session.Events.Received(1).StartStream<BookAggregate>(
    command.Id,
    Arg.Is<BookAdded>(e => e.Title == "Clean Code"));
```

## Code Coverage

TUnit includes built-in code coverage without requiring additional packages:

```bash
# Run tests with coverage (built-in)
dotnet test

# Coverage is automatically collected and reported
```

Coverage reports are generated in standard formats (Cobertura, lcov) and can be viewed in:
- Visual Studio Code (Coverage Gutters extension)
- JetBrains Rider (built-in coverage viewer)
- CI/CD pipelines (GitHub Actions, Azure DevOps)

## CI/CD Integration

### GitHub Actions

TUnit includes **built-in GitHub Actions reporting** that automatically generates test summaries in workflow runs.

#### Automatic Detection

When running in GitHub Actions, TUnit automatically:
- Detects the `GITHUB_ACTIONS` environment variable
- Generates a test summary in the workflow run summary
- Uses collapsible style by default for clean, navigable results
- Shows only failed/skipped tests in details (passed tests are counted but not listed)

#### Workflow Configuration

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-

- name: Restore dependencies
  run: dotnet restore

- name: Build
  run: dotnet build --no-restore --configuration Release

- name: Run tests
  run: dotnet test --configuration Release --no-build ${{ github.event_name == 'pull_request' && '--fail-fast' || '' }}
  # TUnit automatically generates GitHub Actions test summary
  # Results appear in the workflow run summary with collapsible details
  # --fail-fast: Stop on first failure in PRs for quick feedback
```

**Key Features:**
- **NuGet Caching** - Speeds up builds by caching packages
- **Separated Steps** - Restore → Build → Test for clarity and efficiency
- **--no-build** - Tests use already-built assemblies (faster)
- **--fail-fast** - Stops on first failure in PRs for quick feedback
- **Automatic Reporting** - TUnit creates summary without configuration

#### Reporter Styles

TUnit supports two output styles:

**Collapsible (Default)** - Clean summary with expandable details:
```yaml
- name: Run tests
  run: dotnet test
  # Uses collapsible style by default
```

**Full Style** - All details shown directly:
```yaml
- name: Run tests
  run: dotnet test -- --github-reporter-style full
```

Or via environment variable:
```yaml
- name: Run tests
  env:
    TUNIT_GITHUB_REPORTER_STYLE: full
  run: dotnet test
```

#### Benefits

- **No artifact uploads needed** - Results appear directly in workflow summary
- **Automatic detection** - Works without configuration
- **Clean output** - Collapsible details keep summaries navigable
- **Focused results** - Only shows failed/skipped tests in details
- **File size aware** - Respects GitHub's 1MB summary limit

See [.github/workflows/ci.yml](file:///Users/antaoalmada/Projects/BookStore/.github/workflows/ci.yml) for the complete workflow.

## Configuration

### global.json

The project includes a `global.json` file that configures Microsoft.Testing.Platform as the test runner:

```json
{
    "test": {
        "runner": "Microsoft.Testing.Platform"
    }
}
```

This enables `dotnet test` to work with TUnit on .NET 10+.

### Project Configuration

The test project includes:

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
  <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="TUnit" />
  <PackageReference Include="NSubstitute" />
  <PackageReference Include="Aspire.Hosting.Testing" />
</ItemGroup>
```

## Best Practices

### 1. Use Async/Await

All TUnit assertions are async, so tests should be `async Task`:

```csharp
[Test]
public async Task MyTest()  // ✓ Correct
{
    _ = await Assert.That(result).IsNotNull();
}

[Test]
public void MyTest()  // ✗ Incorrect
{
    Assert.That(result).IsNotNull();  // Won't compile
}
```

### 2. Use Fluent Assertions

TUnit's fluent syntax is more readable:

```csharp
// ✓ TUnit style
_ = await Assert.That(result).IsEqualTo(expected);

// ✗ Old xUnit style (don't use)
Assert.Equal(expected, result);
```

### 3. Inject Dependencies

Use TUnit's dependency injection instead of accessing static contexts:

```csharp
// ✓ Correct - inject CancellationToken
[Test]
public async Task MyTest(CancellationToken cancellationToken)
{
    await SomeOperation(cancellationToken);
}

// ✗ Incorrect - accessing TestContext
[Test]
public async Task MyTest()
{
    var token = TestContext.Current.CancellationToken;  // Don't do this
}
```

### 4. Keep Tests Focused

Each test should verify one specific behavior:

```csharp
// ✓ Good - tests one thing
[Test]
public async Task CreateBook_WithValidData_ShouldReturnSuccess()

```

#### Use `[NotInParallel]` When Needed

Some tests can't run in parallel (database tests, file system tests). Use `[NotInParallel]`:

```csharp
// Tests that modify shared state
[Test, NotInParallel]
public async Task Updates_configuration_file()
{
    await ConfigurationManager.SetAsync("key", "value");
    var result = await ConfigurationManager.GetAsync("key");
    _ = await Assert.That(result).IsEqualTo("value");
}
```

#### Control Execution Order with `[DependsOn]`

When tests need to run in a specific order, use `[DependsOn]`:

```csharp
// ✓ Good: Use DependsOn for ordering while maintaining parallelism
[Test]
public async Task Step1_CreateBook()
{
    // Runs first
}

[Test]
[DependsOn(nameof(Step1_CreateBook))]
public async Task Step2_UpdateBook()
{
    // Runs after Step1_CreateBook completes
    // Other unrelated tests can still run in parallel
}

[Test]
[DependsOn(nameof(Step2_UpdateBook))]
public async Task Step3_DeleteBook()
{
    // Runs after Step2_UpdateBook completes
}
```

**Why `[DependsOn]` is better than `[NotInParallel]` with `Order`:**
- More intuitive: explicitly declares dependencies between tests
- More flexible: tests can depend on multiple other tests
- Maintains parallelism: unrelated tests still run in parallel
- Better for complex workflows: clear dependency chains

> [!IMPORTANT]
> If tests need ordering, they might be too tightly coupled. Consider:
> - Refactoring into a single test
> - Using proper setup/teardown
> - Making tests truly independent

### Test Organization

#### One Test Class Per Production Class

```csharp
// BookHandlers.cs → BookHandlerTests.cs
public class BookHandlerTests
{
    [Test]
    public async Task CreateBookHandler_ShouldStartStreamWithBookAddedEvent() { }
    
    [Test]
    public async Task UpdateBookHandler_WithMissingBook_ShouldReturnNotFound() { }
}
```

#### Group Related Tests

Use nested classes or separate files to group related tests:

```csharp
public class BookHandlerTests
{
    public class CreateBookTests
    {
        [Test]
        public async Task WithValidData_ShouldSucceed() { }
        
        [Test]
        public async Task WithInvalidIsbn_ShouldFail() { }
    }
    
    public class UpdateBookTests
    {
        [Test]
        public async Task WithMissingBook_ShouldReturnNotFound() { }
        
        [Test]
        public async Task WithValidData_ShouldSucceed() { }
    }
}
```

### Common Anti-Patterns to Avoid

#### ✗ Avoid Test Interdependence

Tests should not depend on each other's state or execution order (unless using `[DependsOn]` intentionally).

#### ✗ Avoid Shared Instance State

```csharp
// ✗ Bad: Shared state between tests
public class BadTests
{
    private int counter = 0; // Shared state!
    
    [Test]
    public async Task Test1()
    {
        counter++;
        _ = await Assert.That(counter).IsEqualTo(1); // May fail if tests run in parallel
    }
}

// ✓ Good: Each test is independent
public class GoodTests
{
    [Test]
    public async Task Test1()
    {
        var counter = 0; // Local state
        counter++;
        _ = await Assert.That(counter).IsEqualTo(1);
    }
}
```

#### ✗ Avoid Testing Implementation Details

Test behavior, not implementation:

```csharp
// ✗ Bad: Testing implementation details
[Test]
public async Task CreateBook_CallsRepositorySaveMethod()
{
    // Don't test that a specific method was called
}

// ✓ Good: Testing behavior
[Test]
public async Task CreateBook_PersistsBookToDatabase()
{
    var book = await bookService.CreateBook(...);
    var retrieved = await bookService.GetBook(book.Id);
    _ = await Assert.That(retrieved).IsNotNull();
}
```

## Performance Best Practices

Following performance best practices ensures your test suite runs efficiently and provides fast feedback.

### Optimize Test Setup

#### Use Static Lazy Initialization for Shared Resources

For expensive objects that can be safely shared across tests, use static lazy initialization:

```csharp
public class JsonSerializationTests
{
    // ✓ Good: Static lazy initialization - shared across all tests
    private static readonly Lazy<JsonSerializerOptions> _options = new(() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    }, LazyThreadSafetyMode.ExecutionAndPublication);
    
    [Test]
    public async Task MyTest()
    {
        var options = _options.Value; // Only created once
        var json = JsonSerializer.Serialize(data, options);
        // ...
    }
}

// ✗ Bad: Creating expensive object per test
public class IneffcientTests
{
    readonly JsonSerializerOptions _options;
    
    public IneffcientTests()
    {
        _options = new JsonSerializerOptions { ... }; // Created for each test
    }
}
```

**Benefits:**
- Reduces allocations
- Faster test execution
- Thread-safe initialization
- Lazy evaluation (only created when needed)

### Test Categories for CI Optimization

Use `[Category]` attributes to organize tests by speed and enable selective execution:

```csharp
// Fast unit tests
[Test]
[Category("Unit")]
public async Task CreateBook_WithValidData_ShouldSucceed()
{
    // Fast, isolated test
}

// Slower integration tests
[Test]
[Category("Integration")]
public async Task GetWebResourceRootReturnsOkStatusCode(CancellationToken cancellationToken)
{
    // Slower test requiring full application stack
}
```

**CI Workflow Optimization:**

```yaml
# Run fast unit tests first for quick feedback
- name: Run unit tests (fast feedback)
  run: dotnet test --treenode-filter "/**[Category=Unit]" --fail-fast

# Run integration tests after unit tests pass
- name: Run integration tests
  run: dotnet test --treenode-filter "/**[Category=Integration]"
```

**Benefits:**
- Fast feedback in PRs (unit tests run first)
- Fail fast on unit test failures
- Better CI resource utilization
- Clear test organization

### Parallel Execution

#### Tests Run in Parallel By Default

TUnit runs tests in parallel for better performance. Ensure tests are independent:

```csharp
// ✓ Good: Independent test with unique data
[Test]
public async Task Can_create_book()
{
    var bookId = Guid.CreateVersion7(); // Unique per test
    var command = new CreateBook(..., bookId, ...);
    // Test is isolated and can run in parallel
}
```

#### Use `[ParallelLimiter]` for Resource Constraints

Limit parallel execution for tests that share limited resources:

```csharp
public class DatabaseConnectionLimit : IParallelLimit
{
    public int Limit => 5; // Max 5 concurrent database connections
}

[ParallelLimiter<DatabaseConnectionLimit>]
public class DatabaseIntegrationTests
{
    // All tests here respect the connection limit
}
```

#### Use `[ParallelGroup]` for Related Tests

Group tests that share resources to run sequentially within the group:

```csharp
[ParallelGroup("DatabaseTests")]
public class UserRepositoryTests
{
    // These tests share database resources
}

[ParallelGroup("DatabaseTests")]
public class OrderRepositoryTests
{
    // These also share database resources
}

[ParallelGroup("ApiTests")]
public class ApiIntegrationTests
{
    // These can run in parallel with database tests
}
```

### Memory Management

#### Dispose Resources Properly

Implement `IAsyncDisposable` for test classes that create disposable resources:

```csharp
public class ResourceTests : IAsyncDisposable
{
    private readonly List<IDisposable> _disposables = new();
    
    [Test]
    public async Task TestWithResources()
    {
        var resource = new LargeResource();
        _disposables.Add(resource);
        
        await resource.ProcessAsync();
    }
    
    public async ValueTask DisposeAsync()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}
```

#### Avoid Memory Leaks in Static Fields

```csharp
// ✗ Bad: Static collection that grows indefinitely
private static readonly List<TestResult> _allResults = new();

// ✓ Good: Bounded collection or proper cleanup
private static readonly Queue<TestResult> _recentResults = new();
private const int MaxResults = 100;

[After(HookType.Test)]
public void StoreResult()
{
    _recentResults.Enqueue(GetCurrentResult());
    while (_recentResults.Count > MaxResults)
    {
        _recentResults.Dequeue();
    }
}
```

### Optimize Assertions

#### Avoid Expensive Operations in Assertions

```csharp
// ✗ Bad: Expensive operation in assertion
_ = await Assert.That(await GetAllUsersFromDatabase())
    .Count()
    .IsEqualTo(1000);

// ✓ Good: Use efficient queries
var userCount = await GetUserCountFromDatabase();
_ = await Assert.That(userCount).IsEqualTo(1000);
```

### CI/CD Performance

Our CI workflow implements several performance optimizations:

1. **NuGet Caching** - Speeds up builds by caching packages
2. **Separated Test Execution** - Unit tests run first for fast feedback
3. **Fail-Fast Mode** - Stops on first failure in PRs
4. **Test Filtering** - Runs unit and integration tests separately

See [.github/workflows/ci.yml](file:///Users/antaoalmada/Projects/BookStore/.github/workflows/ci.yml) for the complete implementation.

### Performance Monitoring

Track test performance over time:

```bash
# Run tests with timing information
dotnet test --logger "console;verbosity=detailed"

# Monitor slow tests
# TUnit automatically reports test durations in GitHub Actions summaries
```

## Troubleshooting

### Tests Not Discovered

**Issue**: Tests don't appear in Test Explorer

**Solution**: 
1. Rebuild the solution (`dotnet build`)
2. Restart your IDE
3. Ensure test methods are marked with `[Test]` attribute

### CancellationToken Errors

**Issue**: `TestContext does not contain a definition for 'CancellationToken'`

**Solution**: Inject `CancellationToken` as a method parameter instead of accessing via `TestContext`

### dotnet test Fails on .NET 10

**Issue**: "Testing with VSTest target is no longer supported"

**Solution**: Ensure `global.json` exists with the correct configuration (see Configuration section above)

## Migration from xUnit

If you're familiar with xUnit, here are the key differences:

| xUnit | TUnit |
|-------|-------|
| `[Fact]` | `[Test]` |
| `[Theory]` | `[Test]` |
| `[InlineData(...)]` | `[Arguments(...)]` |
| `Assert.Equal(expected, actual)` | `_ = await Assert.That(actual).IsEqualTo(expected)` |
| `Assert.NotNull(value)` | `_ = await Assert.That(value).IsNotNull()` |
| `Assert.True(condition)` | `_ = await Assert.That(condition).IsTrue()` |
| `Assert.Contains(item, collection)` | `_ = await Assert.That(collection).Contains(item)` |

See the [TUnit migration guide](https://tunit.dev/docs/migration/xunit/) for more details.

## Learn More

- [TUnit Documentation](https://tunit.dev/)
- [TUnit GitHub Repository](https://github.com/thomhurst/TUnit)
- [Migration Guide from xUnit](https://tunit.dev/docs/migration/xunit/)
- [NSubstitute Documentation](https://nsubstitute.github.io/)
- [Aspire.Hosting.Testing](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/testing)
