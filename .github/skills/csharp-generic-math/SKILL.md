---
name: csharp-generic-math
description: Use C# generic math (System.Numerics interfaces, static abstract members) to write type-parameter-agnostic arithmetic and mathematical algorithms in .NET 7+. Trigger whenever the user writes or reviews generic methods that do math, asks about INumber, IAdditionOperators, IFloatingPoint, or other System.Numerics interfaces, wants to support multiple numeric types without overloads, is implementing custom numeric/vector types, or uses Math/MathF — even if they don't mention "generic math" by name. Always prefer T.Zero/T.One and type-level math over System.Math when a type parameter is involved.
---

# C# Generic Math Skill

.NET 7 + C# 11 introduced *static abstract/virtual interface members* in `System.Numerics`, letting you write a single generic method or type that works for all numeric types — `int`, `float`, `double`, `Half`, `decimal`, custom vectors, and more — without writing per-type overloads.

## Why this matters

Before .NET 7, arithmetic operators couldn't appear in interface contracts, so libraries had to either duplicate code for every numeric type or fall back to slow reflection/boxing. Generic math eliminates both problems: the compiler resolves every `T.Zero` or `+` call at build time, producing the same code as a hand-written overload.

Key wins:
- **Single generic implementation** replaces N per-type overloads.
- **Compile-time resolution** — zero boxing, zero runtime dispatch.
- **Reaches custom types** — any type that implements the right interfaces works automatically.
- **Type-safe constants** — `T.Zero`, `T.One`, `T.Pi` instead of magic literals.
- **Deprecates `System.Math`/`System.MathF`** — call `float.Sin(x)` or `T.Sqrt(x)` instead.

## Quick reference

| Topic | See |
|-------|-----|
| Choosing the right constraint (`INumber<T>` vs narrower interfaces) | [constraints.md](references/constraints.md) |
| All numeric, operator, function, and parsing interfaces | [interfaces.md](references/interfaces.md) |
| Implementing generic math on custom types | [custom-types.md](references/custom-types.md) |
| Common mistakes, pitfalls, and analyzer hints | [pitfalls.md](references/pitfalls.md) |

## Core constraints at a glance

```csharp
where T : INumber<T>               // any real number (int, float, double, decimal, …)
where T : IFloatingPoint<T>        // floating-point only (float, double, Half, decimal)
where T : IFloatingPointIeee754<T> // IEEE 754 only (float, double, Half)
where T : IBinaryInteger<T>        // integer only (int, long, byte, …)
where T : IAdditionOperators<T,T,T>// only needs + (narrowest, most inclusive)
```

Prefer the *narrowest* constraint that satisfies the algorithm. `INumber<T>` is convenient but rejects complex-number types; specific operator interfaces are more inclusive.

## Essential patterns

### Generic sum

```csharp
static T Sum<T>(IEnumerable<T> source)
    where T : IAdditiveIdentity<T, T>, IAdditionOperators<T, T, T>
{
    var sum = T.AdditiveIdentity; // type-safe zero
    foreach (var value in source)
        sum += value;
    return sum;
}
```

### Generic average

```csharp
static TResult Average<T, TResult>(T first, T second)
    where T : INumber<T>
    where TResult : INumber<TResult>
{
    return TResult.CreateChecked((first + second) / T.CreateChecked(2));
}
```

### Type-specific math (no System.Math)

```csharp
// Prefer: call static methods on the type itself
var sinFloat  = float.Sin(float.Pi);
var sinDouble  = double.Sin(double.Pi);

// Generic:
static T UnitCircleY<T>(T angle)
    where T : ITrigonometricFunctions<T>
    => T.Sin(angle);
```

### Creating numeric values safely

| Method | Behaviour when value doesn't fit |
|--------|----------------------------------|
| `T.CreateChecked(value)` | throws `OverflowException` |
| `T.CreateSaturating(value)` | clamps to `T.MinValue`/`T.MaxValue` |
| `T.CreateTruncating(value)` | wraps (bit-truncation) |

### Useful static members on numeric types

```csharp
T.Zero            // additive identity
T.One             // multiplicative identity
T.AdditiveIdentity
T.MultiplicativeIdentity
T.MinValue / T.MaxValue   // via IMinMaxValue<T>
T.Pi / T.E / T.Tau        // via IFloatingPointConstants<T>
T.IsNaN(value)
T.IsInfinity(value)
T.IsInteger(value)
```

## What to read next

- Picking the right interface → [constraints.md](references/constraints.md)
- Full interface catalogue → [interfaces.md](references/interfaces.md)
- Writing your own numeric type → [custom-types.md](references/custom-types.md)
- Getting compiler errors or unexpected behaviour → [pitfalls.md](references/pitfalls.md)
