# Tensor Libraries for SIMD Span Operations

When you need SIMD-accelerated span operations but don't want to write the vectorization code yourself, two libraries cover most scenarios.

## System.Numerics.Tensors — `TensorPrimitives`

NuGet: `System.Numerics.Tensors`  
Namespace: `System.Numerics.Tensors`  
Docs: https://learn.microsoft.com/dotnet/standard/simd

The `TensorPrimitives` static class provides ready-made, SIMD-optimized operations on `ReadOnlySpan<T>` / `Span<T>`. It is maintained by the .NET team and ships with .NET 9+ in the BCL (also available as a NuGet for earlier versions).

### Quick reference

```csharp
using System.Numerics.Tensors;

// Element-wise operations (source → destination)
TensorPrimitives.Add(x, y, destination);
TensorPrimitives.Subtract(x, y, destination);
TensorPrimitives.Multiply(x, y, destination);
TensorPrimitives.Divide(x, y, destination);
TensorPrimitives.Sqrt(x, destination);
TensorPrimitives.Abs(x, destination);
TensorPrimitives.Log(x, destination);
TensorPrimitives.Exp(x, destination);

// Aggregations
float sum = TensorPrimitives.Sum(x);
float dot = TensorPrimitives.Dot(x, y);
float max = TensorPrimitives.Max(x);
float min = TensorPrimitives.Min(x);
int   maxIdx = TensorPrimitives.IndexOfMax(x);

// In-place (destination == source is allowed)
TensorPrimitives.Sqrt(data, data);
```

### Supported types

.NET 9+ extends support beyond `float` to any `T` implementing the relevant generic-math interface (e.g., `IAdditionOperators<T,T,T>` for `Add`). Check the API docs for the exact constraint on each method.

### When to use

- You need any of the standard operations and your span element type is `float` (or another supported numeric type in .NET 9+).
- You want the fastest possible implementation with zero boilerplate.
- You don't need custom operators or tuple/structured-data support.

---

## NetFabric.Numerics.Tensors

NuGet: `NetFabric.Numerics.Tensors`  
Docs: https://netfabric.github.io/NetFabric.Numerics.Tensors/  
Source: https://github.com/NetFabric/NetFabric.Numerics.Tensors

A community library that extends the `TensorPrimitives` model with:
- Support for **all numeric types** implementing generic-math interfaces (not just `float`).
- Support for **custom operators** via static-virtual interface members — you define the scalar and vector forms once, the library loops efficiently.
- Support for **tuples of values** (2D, 3D, 4D vector structs stored packed in a span).

### Custom operator pattern

Implement one of the operator interfaces:

```csharp
// Unary: one source, one destination
public readonly struct SquareOperator<T> : IUnaryOperator<T, T>
    where T : struct, IMultiplyOperators<T, T, T>
{
    public static T          Invoke(T x)                          => x * x;
    public static Vector<T>  Invoke(ref readonly Vector<T> x)    => x * x;
}

// Binary: two sources, one destination
public readonly struct AddOperator<T> : IBinaryOperator<T, T, T>
    where T : struct, IAdditionOperators<T, T, T>
{
    public static T          Invoke(T x, T y)                                => x + y;
    public static Vector<T>  Invoke(ref readonly Vector<T> x,
                                    ref readonly Vector<T> y)                 => x + y;
}

// Aggregation: collapses span to a single value
public readonly struct SumOperator<T> : IAggregationOperator<T, T>
    where T : struct, IAdditiveIdentity<T, T>, IAdditionOperators<T, T, T>
{
    public static T          Identity                                          => T.AdditiveIdentity;
    public static T          Invoke(T x, T y)                                => x + y;
    public static Vector<T>  Invoke(ref readonly Vector<T> x,
                                    ref readonly Vector<T> y)                 => x + y;
    public static T          Invoke(T x, ref readonly Vector<T> y)            => x + Vector.Sum(y);
}
```

Then pass the operator as a generic type parameter to `Tensor.Apply` / `Tensor.Aggregate`:

```csharp
// Apply SquareOperator to every element, storing result in destination
Tensor.Apply<float, SquareOperator<float>>(source, destination);

// Aggregate with SumOperator
float total = Tensor.Aggregate<float, SumOperator<float>>(source);
```

### Structured data (packed tuples)

If your data is a struct with fields of the same primitive type (e.g., 2D/3D vectors), cast via `MemoryMarshal` before calling tensor operations:

```csharp
// Sum all 2D vectors as if they were flat floats, returning (sumX, sumY) as a ValueTuple
var (sumX, sumY) = Tensor.Sum2D(MemoryMarshal.Cast<MyVector2<float>, float>(span));
```

The library also provides `Sum3D` and `Sum4D` variants.

### Choosing between the two

| Criterion | `TensorPrimitives` | `NetFabric.Numerics.Tensors` |
|-----------|--------------------|-----------------------------|
| Predefined ops only | ✅ simpler | ✅ also supported |
| Custom operators | ❌ | ✅ |
| Types beyond `float` (.NET < 9) | ❌ | ✅ |
| Structured data / packed tuples | ❌ | ✅ |
| Maintained by | .NET team | Community |

If you only need `float` and standard math operations, prefer `TensorPrimitives`. For other numeric types or custom logic, use `NetFabric.Numerics.Tensors`.
