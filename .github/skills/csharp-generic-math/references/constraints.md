# Choosing Constraints for Generic Math

The key design principle: use the **narrowest constraint** that makes your algorithm compile. Narrow constraints maximise the set of types your code accepts.

## Decision guide

```
Need any real number (int, float, decimal)?  → INumber<T>
Need only floating-point?                    → IFloatingPoint<T>
Need IEEE 754 (NaN, Inf, Pi, trig)?         → IFloatingPointIeee754<T>
Need only integers (bit ops, DivRem)?        → IBinaryInteger<T>
Need only +?                                 → IAdditionOperators<T,T,T> + IAdditiveIdentity<T,T>
Need only *?                                 → IMultiplyOperators<T,T,T> + IMultiplicativeIdentity<T,T>
Need + and *?                                → INumber<T> is fine
Custom vector/matrix (not a "number")?       → compose individual operator interfaces
```

## Why not always use `INumber<T>`?

`INumber<T>` extends `IComparable<T>`, which excludes complex numbers (they aren't ordered). If your algorithm doesn't need ordering, prefer `INumberBase<T>` or individual operator interfaces so it works for complex types too.

## `INumber<T>` vs `INumberBase<T>`

| | `INumberBase<T>` | `INumber<T>` |
|---|---|---|
| Complex numbers | ✅ | ❌ |
| Ordering (`<`, `>`) | ❌ | ✅ |
| `Clamp`, `Sign` | ❌ | ✅ |
| `Zero`, `One` | ✅ | ✅ |
| `Create*` conversions | ✅ | ✅ |

Use `INumberBase<T>` when writing algorithms for matrices/complex types. Use `INumber<T>` when you need comparison or clamping.

## Multiple constraints

Combine interfaces freely to express exactly what the algorithm needs:

```csharp
// Sum over anything that has + and an additive identity (zero):
static T Sum<T>(IEnumerable<T> source)
    where T : IAdditiveIdentity<T, T>, IAdditionOperators<T, T, T>

// Dot product: needs +, *, and identity values:
static T Dot<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
    where T : IAdditiveIdentity<T, T>,
              IAdditionOperators<T, T, T>,
              IMultiplyOperators<T, T, T>

// Normalise: needs /, Sqrt — floating-point only:
static T Normalize<T>(T value, T magnitude)
    where T : IDivisionOperators<T, T, T>, IRootFunctions<T>
```

## The self-referencing (CRTP) pattern

All numeric interfaces use a self-referencing type parameter so static members resolve to the concrete type at compile time:

```csharp
// Correct — T implements INumber<T>
where T : INumber<T>

// Wrong — this doesn't let you call T.Zero
where T : INumber<int>
```

## Parsing + math

If you also need to parse values (e.g., from user input), combine with `IParsable<T>`:

```csharp
static T ParseAndClamp<T>(string input, T min, T max)
    where T : IParsable<T>, INumber<T>
{
    var value = T.Parse(input, CultureInfo.InvariantCulture);
    return T.Clamp(value, min, max);
}
```
