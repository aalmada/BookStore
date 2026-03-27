---
name: tunit
description: >
  Write, review, and fix TUnit tests in .NET projects. Use this skill whenever
  you're writing a new test class, adding assertions, creating data-driven tests,
  setting up test lifecycle hooks, or debugging why a test compiles but behaves
  unexpectedly. Also triggers for: migrating from xUnit/NUnit to TUnit, choosing
  the right assertion, using Bogus for test data, wiring up NSubstitute mocks,
  writing integration tests, or any question about parallelism and test ordering.
  Prefer this skill over guessing — TUnit's async-first API has several
  non-obvious patterns that differ from xUnit/NUnit.
---

# TUnit Testing Skill

TUnit is a modern .NET testing framework that is async-first, source-generated,
and runs on Microsoft.Testing.Platform.

---

## Quick-start anatomy

```csharp
public class OrderHandlerTests
{
    [Test]
    [Category("Unit")]                             // categorise for filtering
    public async Task PlaceOrder_Valid_ReturnsOk()
    {
        // Arrange …
        // Act …
        // Assert — always await the assertion
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Status).IsEqualTo("Confirmed");
    }
}
```

Key rule: **every assertion line must be `await`-ed**. Forgetting the `await`
silently skips the assertion.

---

## Reference files — read before writing code

| Topic | File |
|-------|------|
| Assertion API (equality, nulls, booleans, collections, strings, exceptions, multiple) | [references/assertions.md](references/assertions.md) |
| All attributes (Test, Category, Arguments, Before/After, Retry, Repeat, NotInParallel…) | [references/attributes.md](references/attributes.md) |
| Data-driven tests (Arguments, MethodDataSource, ClassDataSource) | [references/data-driven.md](references/data-driven.md) |
| Integration test patterns (Aspire, test infrastructure, async helpers) | [references/integration-patterns.md](references/integration-patterns.md) |

---

## Core rules

```
✅ [Test] async Task               ❌ [Fact] / [TestMethod] / [TestCase]
✅ await Assert.That(...)          ❌ Assert.Equal / FluentAssertions
✅ Per-test data creation          ❌ shared mutable state between tests
```

---

## Running tests

```bash
dotnet test                                              # all projects (parallel)
dotnet test -- --maximum-parallel-tests 4               # cap parallelism
dotnet test -- --treenode-filter "/*/*/*/*[Category=Unit]" # filter by category
dotnet test --project tests/MyProject.Tests/MyProject.Tests.csproj
```

---

## Common mistakes & fixes

| Mistake | Fix |
|---------|-----|
| Assertion silently skipped | Add `await` to every `Assert.That(…)` call |
| Tests interfere with each other | Create all test data inside each test; never share mutable fields |
| Polling for eventual consistency | Use condition helpers or WaitForConditionAsync patterns; never `Task.Delay` |
| `Assert.Fail` not reached after exception | Use `Assert.That(…).Throws<T>()` pattern instead of try/catch |
| Missing `[Before(Class)]` / `[After(Class)]` | Hooks must be `public static async Task`; see attributes reference |
| Data-driven test parameters don't compile | Types in `[Arguments]` must exactly match method parameter types |
