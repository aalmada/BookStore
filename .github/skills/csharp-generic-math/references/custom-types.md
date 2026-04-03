# Implementing Generic Math on Custom Types

When writing your own numeric or vector type, implement the `System.Numerics` interfaces so it participates in generic algorithms written by others — and your own.

## Minimal value type with addition

```csharp
public readonly record struct MyVector2<T>(T X, T Y)
    : IAdditiveIdentity<MyVector2<T>, MyVector2<T>>,
      IAdditionOperators<MyVector2<T>, MyVector2<T>, MyVector2<T>>
    where T : struct, INumber<T>
{
    public static MyVector2<T> AdditiveIdentity
        => new(T.AdditiveIdentity, T.AdditiveIdentity);

    public static MyVector2<T> operator +(MyVector2<T> left, MyVector2<T> right)
        => new(left.X + right.X, left.Y + right.Y);
}
```

This type can now be passed to any generic `Sum<T>` or `Average<T>` method that constrains `T` to `IAdditiveIdentity` + `IAdditionOperators`.

## The self-referencing pattern (CRTP in C#)

All numeric interfaces use `TSelf` — the implementing type itself — so static members such as `AdditiveIdentity` and `operator +` resolve to the concrete type at compile time:

```csharp
// Correct
public readonly record struct Celsius<T>(T Value)
    : IAdditionOperators<Celsius<T>, Celsius<T>, Celsius<T>>
    where T : INumber<T>
{
    public static Celsius<T> operator +(Celsius<T> a, Celsius<T> b)
        => new(a.Value + b.Value);
}
```

## Implementing `INumber<TSelf>` fully

`INumber<TSelf>` brings in many transitively required interfaces. The minimal set you must implement includes:

- `IComparable<T>`, `IEquatable<T>`
- `IFormattable`, `ISpanFormattable`
- `IParsable<T>`, `ISpanParsable<T>`
- `INumberBase<T>` (which requires `Zero`, `One`, `Radix`, `Create*`, `Is*` predicates, all four arithmetic operators, unary `+`/`-`)
- `IComparisonOperators<T, T, bool>`
- `INumber<T>` additions: `Clamp`, `CopySign`, `Max`, `Min`, `Sign`, `MaxNumber`, `MinNumber`

This is a large surface area. If you only need a subset (e.g., only `+` and a zero identity), implement only `IAdditiveIdentity` + `IAdditionOperators` rather than the full `INumber<T>`.

## Checked operators

If you implement subtraction or negation, also implement the checked variants to support `checked {}` contexts:

```csharp
public static MyVal operator -(MyVal left, MyVal right)      // unchecked
    => new(left.Value - right.Value);

public static MyVal operator checked -(MyVal left, MyVal right) // checked
    => new(checked(left.Value - right.Value));
```

## Constants and identity values

Define constants as `static` properties or fields returning the type itself. Use `T.AdditiveIdentity` / `T.MultiplicativeIdentity` from the element type when implementing a composite type:

```csharp
public static MyVector2<T> Zero => new(T.Zero, T.Zero);
public static MyVector2<T> AdditiveIdentity => new(T.AdditiveIdentity, T.AdditiveIdentity);
```

## Conversion helpers (`Create*`)

When implementing `INumberBase<T>`, you must provide the three conversion factory methods:

```csharp
public static MyScalar<T> CreateChecked<TOther>(TOther value)
    where TOther : INumberBase<TOther>
    => new(T.CreateChecked(value));

public static MyScalar<T> CreateSaturating<TOther>(TOther value)
    where TOther : INumberBase<TOther>
    => new(T.CreateSaturating(value));

public static MyScalar<T> CreateTruncating<TOther>(TOther value)
    where TOther : INumberBase<TOther>
    => new(T.CreateTruncating(value));
```

## Using `readonly record struct`

For numeric value types, `readonly record struct` is usually the right choice:

- Immutable by default — avoids defensive copies in `in`/`ref readonly` parameters.
- Value equality and deconstruction for free.
- `with` expression works for derived types.
- Works seamlessly as a SIMD element type.

```csharp
public readonly record struct Kelvin<T>(T Value)
    : IAdditiveIdentity<Kelvin<T>, Kelvin<T>>,
      IAdditionOperators<Kelvin<T>, Kelvin<T>, Kelvin<T>>
    where T : struct, INumber<T>
{
    public static Kelvin<T> AdditiveIdentity => new(T.AdditiveIdentity);
    public static Kelvin<T> operator +(Kelvin<T> a, Kelvin<T> b) => new(a.Value + b.Value);
}
```
