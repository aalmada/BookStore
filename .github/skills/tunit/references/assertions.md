# TUnit Assertion Reference

All assertions are async and must be `await`-ed.

```csharp
await Assert.That(actual).IsEqualTo(expected);
```

---

## Equality & comparison

```csharp
await Assert.That(actual).IsEqualTo(expected);
await Assert.That(actual).IsNotEqualTo(unexpected);
await Assert.That(number).IsGreaterThan(0);
await Assert.That(number).IsGreaterThanOrEqualTo(1);
await Assert.That(number).IsLessThan(100);
await Assert.That(number).IsLessThanOrEqualTo(99);
await Assert.That(number).IsBetween(1, 99);        // inclusive
```

---

## Null & existence

```csharp
await Assert.That(value).IsNull();
await Assert.That(value).IsNotNull();
```

---

## Boolean

```csharp
await Assert.That(flag).IsTrue();
await Assert.That(flag).IsFalse();
```

---

## Type checks

```csharp
await Assert.That(obj).IsTypeOf<MyType>();
await Assert.That(obj).IsNotTypeOf<WrongType>();
await Assert.That(obj).IsAssignableTo<IMyInterface>();
```

---

## Collections

```csharp
await Assert.That(list).IsEmpty();
await Assert.That(list).IsNotEmpty();
await Assert.That(list).Contains(item);
await Assert.That(list).DoesNotContain(item);
await Assert.That(list).Count().IsEqualTo(3);
await Assert.That(list).HasSingleItem();
```

---

## Strings

```csharp
await Assert.That(text).IsEqualTo("exact");
await Assert.That(text).IsEmpty();
await Assert.That(text).IsNotEmpty();
await Assert.That(text).Contains("sub");
await Assert.That(text).DoesNotContain("absent");
await Assert.That(text).StartsWith("prefix");
await Assert.That(text).EndsWith("suffix");
await Assert.That(text).Matches(@"^\d{4}-\d{2}-\d{2}$");  // regex
```

---

## Exceptions

```csharp
// Synchronous throw wrapped in a lambda
await Assert.ThrowsAsync<ArgumentException>(() =>
{
    SomeMethod();
    return Task.CompletedTask;
});

// Async throw
await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    await SomeAsyncMethod());

// Capture and inspect the exception
var ex = await Assert.ThrowsAsync<ApiException>(async () =>
    await client.GetBookAsync(id));
await Assert.That(ex!.StatusCode).IsEqualTo(HttpStatusCode.NotFound);

// Verify NO exception is thrown (rarely needed — just call the method directly)
await Assert.DoesNotThrowAsync(async () => await SafeMethod());
```

---

## Multiple assertions grouped (Assert.Multiple)

Use `Assert.Multiple()` to run several assertions and collect all failures before
reporting, rather than stopping at the first failure.

```csharp
[Test]
public async Task Constructor_SetsAllProperties()
{
    var date = new PartialDate(2023, 5, 15);

    using var scope = Assert.Multiple();
    await Assert.That(date.Year).IsEqualTo(2023);
    await Assert.That(date.Month).IsEqualTo(5);
    await Assert.That(date.Day).IsEqualTo(15);
}
```

> `Assert.Multiple()` returns an `IDisposable`; always wrap in `using var scope`.

---

## Chained member assertions (single await, multiple properties)

```csharp
await Assert.That(user)
    .IsNotNull()
    .And.Member(u => u.Email, e => e.IsEqualTo("john@example.com"))
    .And.Member(u => u.Age, a => a.IsGreaterThan(18));
```

---

## Assert.Fail

Use when a code path should never be reached:

```csharp
Assert.Fail("Expected an exception, but none was thrown.");
```

---

## Awaiting assertions — why it matters

TUnit returns a lazy assertion object from `Assert.That(…)`. The assertion is only
evaluated when `await`-ed. Missing the `await` means the check is never run and
the test passes silently even when the code is wrong. This is the most common
mistake when migrating from xUnit/NUnit.
