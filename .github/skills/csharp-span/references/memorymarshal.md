# MemoryMarshal — Low-Level Reinterpretation

`System.Runtime.InteropServices.MemoryMarshal` provides utilities for reinterpreting memory without copying. These are safe (no `unsafe` keyword required) but require care: you're taking responsibility for type safety.

## Cast — reinterpret element type

`MemoryMarshal.Cast<TFrom, TTo>` reinterprets a span's element type. The total byte count is preserved; the element count changes proportionally. No copying occurs.

```csharp
// Reinterpret byte[] as int[]
byte[] raw = new byte[16];
Span<int> ints = MemoryMarshal.Cast<byte, int>(raw.AsSpan()); // 4 ints

// Reinterpret float[] as byte[]
float[] floats = { 1f, 2f, 3f };
Span<byte> bytes = MemoryMarshal.Cast<float, byte>(floats.AsSpan()); // 12 bytes
```

> Alignment rules: `TTo` must be no larger in alignment requirement than `TFrom`, or a runtime exception is thrown. For primitive types (byte, int, float, etc.) this is generally fine. For custom structs, verify alignment explicitly.

## AsBytes — reinterpret any struct span as bytes

A convenience overload for `Cast<T, byte>`:

```csharp
Span<int>  ints  = stackalloc int[4] { 1, 2, 3, 4 };
Span<byte> bytes = MemoryMarshal.AsBytes(ints); // 16 bytes
```

## Read / Write — read/write a struct from a byte span

```csharp
ReadOnlySpan<byte> data = ...;

// Read a struct from the start of the span
MyStruct s = MemoryMarshal.Read<MyStruct>(data);

// Write a struct into a byte span
Span<byte> dest = stackalloc byte[Unsafe.SizeOf<MyStruct>()];
MemoryMarshal.Write(dest, in s);

// TryRead / TryWrite — return false if span is too short
bool ok = MemoryMarshal.TryRead(data, out MyStruct s2);
```

## GetReference — reference to first element (bounds-check elimination)

`MemoryMarshal.GetReference<T>` returns a `ref T` to the first element. Combined with `System.Runtime.CompilerServices.Unsafe.Add`, this eliminates per-element bounds checks inside manually-verified loops:

```csharp
using System.Runtime.CompilerServices;

Span<int> span = ...;
ref int first = ref MemoryMarshal.GetReference(span);

for (int i = 0; i < span.Length; i++)
{
    ref int element = ref Unsafe.Add(ref first, i);
    element = element * 2;
}
```

Only apply this in hot inner loops where the performance difference is measurable. The safety invariant (bounds verified before the loop) must be obvious at the call site.

## CreateSpan / CreateReadOnlySpan — span from a single ref

Construct a span from an arbitrary `ref T` and a length. Useful for interop and low-level code:

```csharp
int value = 42;
Span<int> span = MemoryMarshal.CreateSpan(ref value, 1);

// Read-only variant
ReadOnlySpan<int> ros = MemoryMarshal.CreateReadOnlySpan(ref value, 1);
```

## GetArray — recover the backing array (if any)

```csharp
Memory<byte> mem = someArray.AsMemory();
if (MemoryMarshal.TryGetArray(mem, out ArraySegment<byte> segment))
{
    byte[] array = segment.Array!;
    // useful when an existing API requires a byte[] rather than Span/Memory
}
```

## Common use: struct↔byte round-trip

Pattern for reading fixed-length binary records from a byte buffer:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
readonly struct PacketHeader
{
    public readonly uint Magic;
    public readonly ushort Version;
    public readonly ushort Length;
}

static bool TryReadHeader(ReadOnlySpan<byte> buffer, out PacketHeader header)
{
    if (buffer.Length < Unsafe.SizeOf<PacketHeader>())
    {
        header = default;
        return false;
    }
    header = MemoryMarshal.Read<PacketHeader>(buffer);
    return true;
}
```
