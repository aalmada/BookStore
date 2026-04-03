# Span Patterns for SIMD

Efficient SIMD code in .NET combines three techniques: **span type selection**, **bounds-check elimination**, and **remainder handling**. This file shows how they fit together.

## Why spans, not arrays or IEnumerable

SIMD requires contiguous memory. `Span<T>` / `ReadOnlySpan<T>` give you that guarantee and integrate with `MemoryMarshal.Cast<>` to reinterpret memory as vectors without any copying. `IEnumerable<T>` provides no layout guarantee and prevents vectorization.

## Casting a span to a span of vectors

`MemoryMarshal.Cast<TFrom, TTo>()` reinterprets memory without copying. Use it to convert a `Span<T>` to a `Span<Vector<T>>` (and back):

```csharp
ReadOnlySpan<float> source = ...;
ReadOnlySpan<Vector<float>> vectors = MemoryMarshal.Cast<float, Vector<float>>(source);
// vectors.Length == source.Length / Vector<float>.Count  (floor division)
```

The cast rounds *down*, so `vectors.Length * Vector<T>.Count` may be less than `source.Length`. Process the remainder scalarly.

## Bounds-check elimination

The JIT performs bounds checking by default. Inside SIMD hot loops, use `MemoryMarshal.GetReference()` and `Unsafe.Add()` to remove those checks:

```csharp
// ❌ Has bounds checks
for (int i = 0; i < vectors.Length; i++)
    destVectors[i] = leftVectors[i] + rightVectors[i];

// ✅ No bounds checks
ref var leftRef  = ref MemoryMarshal.GetReference(leftVectors);
ref var rightRef = ref MemoryMarshal.GetReference(rightVectors);
ref var destRef  = ref MemoryMarshal.GetReference(destVectors);

for (int i = 0; i < leftVectors.Length; i++)
    Unsafe.Add(ref destRef, i) = Unsafe.Add(ref leftRef, i) + Unsafe.Add(ref rightRef, i);
```

Only apply this inside carefully guarded inner loops where you've already verified lengths. The safety argument must be obvious at the call site.

## Full pattern: SIMD + 4-at-a-time CPU-level parallelism + scalar tail

Modern CPUs can execute several independent scalar operations in parallel (instruction-level parallelism). Unrolling the scalar tail 4× reduces loop overhead and helps the CPU saturate its execution units even when SIMD is unavailable:

```csharp
static void Add<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination)
    where T : struct, IAdditionOperators<T, T, T>
{
    int startIndex = 0;

    if (Vector.IsHardwareAccelerated && Vector<T>.IsSupported)
    {
        var leftVecs  = MemoryMarshal.Cast<T, Vector<T>>(left);
        var rightVecs = MemoryMarshal.Cast<T, Vector<T>>(right);
        var destVecs  = MemoryMarshal.Cast<T, Vector<T>>(destination);

        ref var lRef = ref MemoryMarshal.GetReference(leftVecs);
        ref var rRef = ref MemoryMarshal.GetReference(rightVecs);
        ref var dRef = ref MemoryMarshal.GetReference(destVecs);

        for (int i = 0; i < leftVecs.Length; i++)
            Unsafe.Add(ref dRef, i) = Unsafe.Add(ref lRef, i) + Unsafe.Add(ref rRef, i);

        startIndex = leftVecs.Length * Vector<T>.Count;
    }

    // 4-at-a-time scalar tail for the remaining elements
    ScalarTail4(left, right, destination, startIndex);
}

static void ScalarTail4<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> dst, int index)
    where T : struct, IAdditionOperators<T, T, T>
{
    ref var lRef = ref MemoryMarshal.GetReference(left);
    ref var rRef = ref MemoryMarshal.GetReference(right);
    ref var dRef = ref MemoryMarshal.GetReference(dst);

    int end4 = left.Length - 3;
    for (; index < end4; index += 4)
    {
        Unsafe.Add(ref dRef, index)     = Unsafe.Add(ref lRef, index)     + Unsafe.Add(ref rRef, index);
        Unsafe.Add(ref dRef, index + 1) = Unsafe.Add(ref lRef, index + 1) + Unsafe.Add(ref rRef, index + 1);
        Unsafe.Add(ref dRef, index + 2) = Unsafe.Add(ref lRef, index + 2) + Unsafe.Add(ref rRef, index + 2);
        Unsafe.Add(ref dRef, index + 3) = Unsafe.Add(ref lRef, index + 3) + Unsafe.Add(ref rRef, index + 3);
    }

    switch (left.Length - index)
    {
        case 3: Unsafe.Add(ref dRef, index+2) = Unsafe.Add(ref lRef, index+2) + Unsafe.Add(ref rRef, index+2); goto case 2;
        case 2: Unsafe.Add(ref dRef, index+1) = Unsafe.Add(ref lRef, index+1) + Unsafe.Add(ref rRef, index+1); goto case 1;
        case 1: Unsafe.Add(ref dRef, index)   = Unsafe.Add(ref lRef, index)   + Unsafe.Add(ref rRef, index);   break;
    }
}
```

> Note: `TensorPrimitives.Add()` from `System.Numerics.Tensors` already implements this pattern for you. Write it manually only when you need a custom operation that TensorPrimitives doesn't provide.

## Accessing List\<T\> internals

```csharp
using System.Runtime.InteropServices;

List<float> list = ...;
Span<float> span = CollectionsMarshal.AsSpan(list);
// span shares the List's internal array — in-place SIMD is safe here
```

Do not append to the list while holding the span.

## Multi-threading with Memory\<T\>

`Span<T>` is a `ref struct` and cannot be captured in lambdas. Use `Memory<T>` to slice work across threads:

```csharp
static void ParallelAdd<T>(ReadOnlyMemory<T> left, ReadOnlyMemory<T> right, Memory<T> destination)
    where T : struct, IAdditionOperators<T, T, T>
{
    int cores     = Environment.ProcessorCount;
    int chunkSize = Math.Max(left.Length / cores, 1_000); // avoid tiny chunks

    if (cores < 2 || left.Length < cores * 1_000)
    {
        Add(left.Span, right.Span, destination.Span);
        return;
    }

    var actions = new Action[left.Length / chunkSize];
    int start = 0;
    for (int i = 0; i < actions.Length; i++)
    {
        int len = (i == actions.Length - 1) ? left.Length - start : chunkSize;
        var l = left.Slice(start, len);
        var r = right.Slice(start, len);
        var d = destination.Slice(start, len);
        actions[i] = () => Add(l.Span, r.Span, d.Span);
        start += len;
    }
    Parallel.Invoke(actions);
}
```

## Benchmarking SIMD code with BenchmarkDotNet

Use separate JIT jobs to enforce each SIMD level, ensuring the benchmark truly exercises the target code path:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

[Config(typeof(SimdConfig))]
public class MyBenchmarks
{
    [Params(1000)]
    public int Length;

    private float[] _left, _right, _dest;

    [GlobalSetup]
    public void Setup() { /* fill arrays */ }

    [Benchmark(Baseline = true)]
    public void Scalar() => ScalarAdd(_left, _right, _dest);

    [Benchmark]
    public void Simd() => Add(_left, _right, _dest);
}

class SimdConfig : ManualConfig
{
    public SimdConfig()
    {
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithEnvironmentVariable("DOTNET_EnableHWIntrinsic", "0").WithId("Scalar"));
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithEnvironmentVariable("DOTNET_JitDisabledAssemblies", "avx2").WithId("Vector128"));
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithId("Vector256"));
    }
}
```
