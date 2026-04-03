# System.Runtime.Intrinsics — Vector128/256/512\<T\>

The `System.Runtime.Intrinsics` namespace provides fixed-width SIMD types: `Vector128<T>`, `Vector256<T>`, and `Vector512<T>`. Unlike `Vector<T>`, their lane count is determined by the explicit bit-width you choose, not the runtime.

## When to use over `Vector<T>`

- You need to control which vector size is used per code path.
- An algorithm requires platform-specific instructions (`Sse41.Blend`, `Avx2.GatherVector256`, `AdvSimd.Arm64.AddPairwise`, etc.).
- You need an operation `Vector<T>` doesn't expose (e.g., horizontal add, shuffle/permute, gather/scatter).
- In .NET 8+, `Vector256<T>` is reimplemented as two `Vector128<T>` operations internally, so even partial hardware acceleration is exploited.

## Checking hardware acceleration

```csharp
if (Vector128.IsHardwareAccelerated) { /* 128-bit SIMD available */ }
if (Vector256.IsHardwareAccelerated) { /* 256-bit */               }
if (Vector512.IsHardwareAccelerated) { /* 512-bit / AVX-512 */     }
```

For platform-specific intrinsics:

```csharp
if (Avx2.IsSupported)   { /* use Avx2.* methods */ }
if (Sse41.IsSupported)  { /* use Sse41.* methods */ }
if (AdvSimd.IsSupported){ /* use AdvSimd.* methods (Arm) */ }
```

## Common factory / utility methods

All three types share a similar static API (shown for `Vector256` but the pattern applies to 128 and 512):

```csharp
// Broadcast scalar to all lanes
var v = Vector256.Create(1.0f);

// Load from memory (no copy)
var v = Vector256.LoadUnsafe(ref source[0]);

// Store to memory
v.StoreUnsafe(ref destination[0]);

// Element-wise arithmetic (cross-platform, .NET 7+)
var sum = Vector256.Add(a, b);     // or: a + b
var diff = Vector256.Subtract(a, b);
var prod = Vector256.Multiply(a, b);

// Comparisons (return bitmask vector)
var mask = Vector256.Equals(a, b);
var lt   = Vector256.LessThan(a, b);

// Blend / select lanes
var blend = Vector256.ConditionalSelect(mask, a, b);

// Reduction
float hSum = Vector256.Sum(floatVec);
```

## Explicit-width iteration pattern

When you want a specific width (e.g., always use 256-bit if available, fall back to 128-bit):

```csharp
public static void Negate(Span<float> data)
{
    int i = 0;

    if (Vector256.IsHardwareAccelerated)
    {
        int limit = data.Length - Vector256<float>.Count + 1;
        ref float r = ref MemoryMarshal.GetReference(data);
        for (; i < limit; i += Vector256<float>.Count)
        {
            var v = Vector256.LoadUnsafe(ref Unsafe.Add(ref r, i));
            Vector256.Negate(v).StoreUnsafe(ref Unsafe.Add(ref r, i));
        }
    }
    else if (Vector128.IsHardwareAccelerated)
    {
        int limit = data.Length - Vector128<float>.Count + 1;
        ref float r = ref MemoryMarshal.GetReference(data);
        for (; i < limit; i += Vector128<float>.Count)
        {
            var v = Vector128.LoadUnsafe(ref Unsafe.Add(ref r, i));
            Vector128.Negate(v).StoreUnsafe(ref Unsafe.Add(ref r, i));
        }
    }

    // Scalar tail
    for (; i < data.Length; i++)
        data[i] = -data[i];
}
```

## Platform-specific intrinsics

The `System.Runtime.Intrinsics.X86` and `System.Runtime.Intrinsics.Arm` namespaces expose CPU instruction sets directly. Guard every call behind `IsSupported`:

```csharp
using System.Runtime.Intrinsics.X86;

// Horizontal add of adjacent pairs (SSE3)
if (Sse3.IsSupported)
{
    var result = Sse3.HorizontalAdd(a, b);
}

// Gather — load non-contiguous elements (AVX2)
if (Avx2.IsSupported)
{
    var gathered = Avx2.GatherVector256(ref baseAddr, indices, scale: 4);
}
```

For Arm:

```csharp
using System.Runtime.Intrinsics.Arm;

if (AdvSimd.IsSupported)
{
    var result = AdvSimd.Add(a, b);
}
```

## Vector512 and AVX-512 (.NET 8+)

`Vector512<T>` and Intel AVX-512 support were added in .NET 8. AVX-512 provides 512-bit registers and new capabilities (ternary logic, compressed stores, masked operations):

```csharp
using System.Runtime.Intrinsics.X86;

if (Avx512F.IsSupported)
{
    // Fused multiply-add: (a * b) + c in one instruction
    var fma = Avx512F.FusedMultiplyAdd(a, b, c);
}
```

## ConstExpected attribute (.NET 8+)

Some intrinsics require a constant operand (e.g., shuffle control masks). .NET 8 annotates these parameters with `[ConstExpected]`. Pass a literal or `const` variable — a non-constant value will compile but produce a warning and may silently fall back to a slower path.

```csharp
// Correct — literal is constant
var shuffled = Avx2.Shuffle(v, 0b_11_10_01_00);

// Wrong — runtime value loses SIMD benefit
int controlByte = GetControlByte(); // [ConstExpected] violated
var shuffled = Avx2.Shuffle(v, (byte)controlByte); // compiler warns
```
