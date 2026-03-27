# TUnit Data-Driven Tests Reference

---

## [Arguments] — inline values

The simplest form. Add one `[Arguments]` attribute per test case; TUnit runs the
test once per attribute.

```csharp
[Test]
[Arguments(1, 2, 3)]
[Arguments(5, 5, 10)]
[Arguments(-1, 1, 0)]
public async Task Add_ReturnsExpected(int a, int b, int expected)
{
    var result = Calculator.Add(a, b);
    await Assert.That(result).IsEqualTo(expected);
}
```

Argument types must exactly match the method parameter types — no implicit
conversions. Use `null` for nullable parameters.

---

## [Arguments] with strings / nulls

```csharp
[Test]
[Arguments("")]
[Arguments(null)]
[Arguments("   ")]
public async Task Validator_RejectsBlankNames(string? name)
{
    var result = Validator.ValidateName(name);
    await Assert.That(result.IsFailure).IsTrue();
}
```

---

## [MethodDataSource] — data from a static method

Use when the data is complex, shared between tests, or generated at runtime.

```csharp
[Test]
[MethodDataSource(nameof(InvalidPrices))]
public async Task Price_MustBePositive(decimal price)
{
    var result = Product.SetPrice(price);
    await Assert.That(result.IsFailure).IsTrue();
}

private static IEnumerable<decimal> InvalidPrices()
{
    yield return 0m;
    yield return -1m;
    yield return decimal.MinValue;
}
```

For multiple parameters, yield **tuples**:

```csharp
[MethodDataSource(nameof(AdditionCases))]
public async Task Add_ReturnsExpected(int a, int b, int expected) { … }

private static IEnumerable<(int a, int b, int expected)> AdditionCases()
{
    yield return (1, 2, 3);
    yield return (10, 20, 30);
}
```

The method can be in a different class:
```csharp
[MethodDataSource(typeof(SharedTestData), nameof(SharedTestData.Users))]
```

---

## [ClassDataSource] — entire object as source

Supply a class instance directly. Every public property becomes a labelled case.

```csharp
public class AdditionTestCase
{
    public int A { get; init; }
    public int B { get; init; }
    public int Expected { get; init; }
}

[Test]
[ClassDataSource<AdditionTestCase>(Shared = SharedType.None)]
public async Task Add_ClassSource(AdditionTestCase tc)
{
    await Assert.That(Calculator.Add(tc.A, tc.B)).IsEqualTo(tc.Expected);
}
```

---

## [Matrix] — cartesian product

Run the test for every combination of the provided values:

```csharp
[Test]
[MatrixDataSource]
public async Task Render_SupportsFormats(
    [Matrix("json", "xml", "csv")] string format,
    [Matrix(true, false)] bool pretty)
{
    var output = Renderer.Render(format, pretty);
    await Assert.That(output).IsNotEmpty();
}
// → 6 test cases (3 × 2)
```

---

## Display names

TUnit auto-generates test names from argument values. To override:

```csharp
[Arguments(1, 2, 3, DisplayName = "Small numbers")]
```

---

## When to use which

| Scenario | Recommended approach |
|----------|---------------------|
| Small fixed set of simple values | `[Arguments]` |
| Larger set or shared across tests | `[MethodDataSource]` |
| Complex object with labelled fields | `[ClassDataSource]` |
| Cartesian product of two+ value sets | `[MatrixDataSource]` |
