---
name: csharp-simd
description: Use SIMD (Single Instruction, Multiple Data) to write high-performance vectorized .NET code. Covers System.Numerics.Vector<T> for portable cross-platform SIMD, System.Runtime.Intrinsics.Vector128/256/512<T> for explicit-width control, TensorPrimitives for ready-made span operations, and the canonical span iteration patterns (MemoryMarshal.Cast, bounds-check elimination, remainder handling). Trigger whenever the user wants to speed up loops over large arrays or spans, asks about vectorization, hardware intrinsics, Vector<T>, Vector128/Vector256/Vector512, TensorPrimitives, SIMD, auto-vectorization, or high-performance numeric processing in C# — even if they don't mention SIMD by name. Prefer this skill over guessing; SIMD in .NET has several subtleties that are easy to get wrong (hardware guards, type support checks, remainder handling, span casting rules).
---

# C# SIMD Skill

SIMD ("Single Instruction, Multiple Data") executes one operation on many elements at once using dedicated CPU registers. The JIT eliminates dead code paths when SIMD is unavailable, so you pay no penalty on unsupported hardware.

.NET exposes SIMD through two complementary layers:

| Layer | Namespace | Best for |
|-------|-----------|----------|
| `Vector<T>` | `System.Numerics` | Portable, generic, automatic width — start here |
| `Vector128/256/512<T>` | `System.Runtime.Intrinsics` | Explicit width, platform-specific instructions |

Both layers cooperate with `Span<T>` via `MemoryMarshal.Cast<>`.

## Reference files

| Topic | File |
|-------|------|
| `Vector<T>` API and patterns | [references/vector-t.md](references/vector-t.md) |
| `Vector128/256/512<T>` and intrinsics | [references/intrinsics.md](references/intrinsics.md) |
| Span patterns, bounds-check elimination, remainder handling | [references/span-patterns.md](references/span-patterns.md) |
| High-level libraries: `TensorPrimitives` and `NetFabric.Numerics.Tensors` | [references/tensor-libraries.md](references/tensor-libraries.md) |

Load the reference file(s) relevant to the user's task before writing code.

## Choosing the right API

**Use `Vector<T>`** (from `System.Numerics`) when:
- You want portable code that works on x86, x64, Arm, etc.
- You're working with generic numeric types (`where T : struct`).
- You don't need explicit control over vector width.

**Use `Vector128/256/512<T>`** (from `System.Runtime.Intrinsics`) when:
- You need a specific width for an algorithm (e.g., byte-shuffle operations that require exactly 128 bits).
- You want to call platform-specific instructions (`Sse`, `Avx2`, `AdvSimd`).
- `Vector<T>` doesn't expose an operation you need.

**Use `TensorPrimitives`** (from  `System.Numerics.Tensors` NuGet) when:
- You need common operations (Add, Sum, Dot, Min, Max, Sqrt, etc.) on `ReadOnlySpan<T>` without writing any SIMD code yourself.

## Essential guard pattern

Always check before using SIMD. The JIT removes the dead branch entirely when the condition is compile-time false:

```csharp
if (Vector.IsHardwareAccelerated && Vector<T>.IsSupported)
{
    // SIMD path
}
// scalar fallback
```

For fixed-width vectors:

```csharp
if (Vector256.IsHardwareAccelerated)
{
    // 256-bit path
}
```

## Performance principles

1. **Use `Span<T>` / `ReadOnlySpan<T>`, not arrays or `IEnumerable<T>`** — SIMD requires contiguous memory; spans provide it without copies.
2. **Eliminate bounds checks** using `MemoryMarshal.GetReference()` + `Unsafe.Add()` in hot loops (details in [span-patterns.md](references/span-patterns.md)).
3. **Handle remainders** — span lengths rarely divide evenly by `Vector<T>.Count`. Always process the tail with a scalar loop.
4. **Reach `List<T>`** via `CollectionsMarshal.AsSpan()` to avoid paying the `IEnumerable` overhead.
5. **Measure with BenchmarkDotNet** — compare Scalar / Vector128 / Vector256 / Vector512 jobs to confirm gains. See [span-patterns.md](references/span-patterns.md) for a job config example.
