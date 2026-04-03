# Generic Math — Common Pitfalls

## 1. Using `System.Math` / `System.MathF` in generic code

**Problem**: `Math.Sin((double)angle)` forces a specific type and loses precision for `float` or `Half`.

**Fix**: Call the static method directly on the type or via a function interface:

```csharp
// Bad
static double Sin(double x) => Math.Sin(x);

// Good — works for float, double, Half
static T Sin<T>(T x) where T : ITrigonometricFunctions<T> => T.Sin(x);

// Also good for non-generic float code:
var r = float.Sin(1.0f);  // no MathF needed
```

## 2. Using `default(T)` instead of `T.Zero`

**Problem**: `default(T)` is zero for value types, but doesn't carry meaning and won't work if `T` is a reference type.

**Fix**: Use `T.Zero` (from `INumberBase<T>`) or `T.AdditiveIdentity` (from `IAdditiveIdentity<T,T>`):

```csharp
// Bad
var sum = default(T);

// Good
var sum = T.AdditiveIdentity; // semantically "zero for addition"
```

## 3. Casting literals instead of using `Create*`

**Problem**: `(T)2` works only for primitive numeric types and won't compile for custom types.

**Fix**: Use the appropriate `Create*` method:

```csharp
// Bad — only works for some types
var half = (T)2;

// Good
var two = T.CreateChecked(2);   // throws OverflowException if 2 doesn't fit
var two = T.CreateSaturating(2); // clamps if 2 doesn't fit
var two = T.CreateTruncating(2); // wraps if 2 doesn't fit
```

Choose `CreateChecked` in most algorithmic code; `CreateSaturating` when you explicitly want clamping behaviour (e.g., pixel values).

## 4. Over-constraining with `INumber<T>` when a narrower interface works

**Problem**: Constraining to `INumber<T>` excludes complex numbers, vectors, and other non-comparable types.

**Fix**: Use the most specific set of operator interfaces the algorithm actually needs:

```csharp
// Unnecessarily broad — excludes complex numbers and custom vectors
static T Sum<T>(IEnumerable<T> source) where T : INumber<T>

// Better — accepts anything with + and an additive identity
static T Sum<T>(IEnumerable<T> source)
    where T : IAdditiveIdentity<T, T>, IAdditionOperators<T, T, T>
```

## 5. Forgetting the self-referencing constraint (`where T : INumber<T>`)

**Problem**: Writing `where T : INumber<int>` or `where T : INumber` (no type argument).

**Fix**: The type parameter must implement the interface with itself as `TSelf`:

```csharp
// Wrong — INumber<int> means only int works
static T Add<T>(T a, T b) where T : INumber<int>

// Correct
static T Add<T>(T a, T b) where T : INumber<T>
```

## 6. Implementing `INumber<T>` on a class

**Problem**: The interface is designed for value types. Reference-type semantics (null, heap allocation) break the expected behaviour.

**Fix**: Use `struct` or `readonly record struct` for custom numeric types.

## 7. Not implementing the checked operator alongside the unchecked one

**Problem**: Adding `operator checked -` on a type that doesn't also have `operator -` causes a compiler error.

**Fix**: Always provide the unchecked variant when implementing a checked variant, and vice versa.

## 8. Relying on `double` constants in generic code

**Problem**: `Math.PI` is `double`; assigning it to `T` requires a cast.

**Fix**: Use the generic constant from the floating-point interface:

```csharp
// Bad
static T CircleArea<T>(T r) where T : IFloatingPointIeee754<T>
    => T.CreateChecked(Math.PI) * r * r;  // unnecessary conversion

// Good
static T CircleArea<T>(T r) where T : IFloatingPointIeee754<T>
    => T.Pi * r * r; // Pi is already T
```

## 9. Mixing integer and floating-point constraints incorrectly

**Problem**: Calling `T.Sqrt` on a type constrained only to `INumber<T>` — `decimal` and integers don't implement `IRootFunctions<T>`.

**Fix**: Constrain to the specific function interface:

```csharp
// Compile error if T is int or decimal
static T Hypotenuse<T>(T a, T b) where T : INumber<T>
    => T.Sqrt(a * a + b * b);

// Correct
static T Hypotenuse<T>(T a, T b)
    where T : IMultiplyOperators<T, T, T>,
              IAdditionOperators<T, T, T>,
              IRootFunctions<T>
    => T.Sqrt(a * a + b * b);
```

## 10. Forgetting to add `using System.Numerics;`

All generic math interfaces are in the `System.Numerics` namespace. A missing `using` produces confusing "type not found" errors rather than a clear indication of what's missing.
