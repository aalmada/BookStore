# System.Numerics.Vector\<T\>

`Vector<T>` is the portable, width-agnostic SIMD type in .NET. The runtime selects the widest available register (128 / 256 / 512 bits) at JIT time, and `Vector<T>.Count` reflects how many `T` elements fit. You never hard-code the width, which keeps code portable across x86, x64, Arm64, etc.

## Supported types

`Vector<T>` supports all primitive numeric types: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint`, `float`, `double`. Check `Vector<T>.IsSupported` at runtime for any custom type.

## Key members

| Member | Purpose |
|--------|---------|
| `Vector<T>.Count` | Elements per vector (platform-dependent) |
| `Vector<T>.IsSupported` | Whether `T` is supported |
| `Vector.IsHardwareAccelerated` | SIMD available on this machine |
| `Vector<T>.Zero` | Vector of zeros |
| `Vector<T>.One` | Vector of ones |
| `Vector<T>.AllBitsSet` | Bitmask of all 1s (useful for masking) |
| `new Vector<T>(value)` | Broadcast scalar to all lanes |
| `Vector.Sum(v)` | Horizontal sum of all elements |
| `Vector.Dot(a, b)` | Dot product |
| `Vector.Min(a, b)` / `Vector.Max(a, b)` | Element-wise min/max |
| `Vector.Abs(v)` | Absolute value |
| `Vector.Sqrt(v)` | Square root |
| `Vector.ConditionalSelect(mask, a, b)` | Blend two vectors with a mask |
| `Vector.Equals(a, b)` | Element-wise equality (returns bitmask vector) |
| `Vector.LessThan(a, b)` | Element-wise less-than (bitmask) |
| `Vector.GreaterThan(a, b)` | Element-wise greater-than (bitmask) |

Arithmetic operators (`+`, `-`, `*`, `/`, `&`, `\|`, `^`, `~`, `<<`, `>>`) work directly on `Vector<T>`.

## Canonical sum example

```csharp
using System.Numerics;
using System.Runtime.InteropServices;

public static T Sum<T>(ReadOnlySpan<T> source)
    where T : struct, IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
{
    var sum = T.AdditiveIdentity;

    if (Vector.IsHardwareAccelerated && Vector<T>.IsSupported && source.Length >= Vector<T>.Count)
    {
        var sumVector = Vector<T>.Zero;

        // Cast span to span-of-vectors (no copy)
        var vectors = MemoryMarshal.Cast<T, Vector<T>>(source);
        foreach (ref readonly var v in vectors)
            sumVector += v;

        sum = Vector.Sum(sumVector);

        // Trim source to the remainder not covered by full vectors
        source = source[vectors.Length * Vector<T>.Count..];
    }

    // Scalar tail
    foreach (ref readonly var value in source)
        sum += value;

    return sum;
}
```

Key details:
- Use `foreach (ref readonly …)` to avoid copying value types.
- `source.Length >= Vector<T>.Count` guards against processing a span smaller than one vector.
- After the SIMD loop, slice `source` to the remaining elements (`source.Length % Vector<T>.Count`), then process them scalarly.

## Apply (element-wise transform)

Pattern for applying an operation to two spans and writing the result to a third:

```csharp
public static void Add<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    where T : struct, IAdditionOperators<T, T, T>
{
    if (Vector.IsHardwareAccelerated && Vector<T>.IsSupported)
    {
        var leftVectors = MemoryMarshal.Cast<T, Vector<T>>(left);
        var rightVectors = MemoryMarshal.Cast<T, Vector<T>>(right);
        var destVectors  = MemoryMarshal.Cast<T, Vector<T>>(destination);

        for (int i = 0; i < leftVectors.Length; i++)
            destVectors[i] = leftVectors[i] + rightVectors[i];

        var processed = leftVectors.Length * Vector<T>.Count;
        left        = left[processed..];
        right       = right[processed..];
        destination = destination[processed..];
    }

    // Scalar tail
    for (int i = 0; i < left.Length; i++)
        destination[i] = left[i] + right[i];
}
```

## Reaching List\<T\>

`List<T>` wraps an internal array. Access it as a span via `CollectionsMarshal.AsSpan()` to enable SIMD without copying:

```csharp
var span = CollectionsMarshal.AsSpan(myList);
var total = Sum(span);
```

When the `IEnumerable<T>` overload must also benefit from SIMD, detect the concrete type at runtime:

```csharp
public static T Sum<T>(IEnumerable<T> source)
    where T : struct, IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
{
    if (source is T[] arr)        return Sum(arr.AsSpan());
    if (source is List<T> list)   return Sum(CollectionsMarshal.AsSpan(list));
    // generic fallback
    var sum = T.AdditiveIdentity;
    foreach (var v in source) sum += v;
    return sum;
}
```

## Casting structured data

If your type is a value struct with fields of the same primitive type (e.g., a 2D vector with `float X, Y`), you can cast a span of your type to a span of the field type and process it with SIMD:

```csharp
// MyVector2<float> has fields X and Y — laid out adjacently in memory
var asFloats = MemoryMarshal.Cast<MyVector2<float>, float>(vectors);
var sum = Sum(asFloats); // processed in pairs
```

This works because the runtime guarantees contiguous field layout for value types without explicit `StructLayout(Explicit)`.
