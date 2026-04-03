# Memory\<T\>, Async Patterns, and Buffer Pooling

## When to use Memory\<T\> instead of Span\<T\>

`Memory<T>` is a regular struct (not `ref struct`), so it can be stored anywhere a `Span<T>` cannot:

- Fields in classes or regular structs
- Generic type arguments
- Lambda captures
- Across `await` points in `async` methods

The tradeoff: accessing the data requires calling `.Span`, which has a small overhead over direct `Span<T>` indexing. The JIT often optimises it away, but for inner hot loops prefer to call `.Span` once outside the loop.

## Creating Memory\<T\>

```csharp
// From array
byte[] array = new byte[1024];
Memory<byte> mem = array.AsMemory();
Memory<byte> slice = array.AsMemory(offset, length);

// From string (read-only)
ReadOnlyMemory<char> chars = "hello".AsMemory();

// From ArrayPool-rented array (see below)
// (wrap in IMemoryOwner to track ownership)
```

## Converting between Memory\<T\> and Span\<T\>

```csharp
Memory<byte> mem = ...;

// Memory → Span (cheap, do this once before a loop)
Span<byte> span = mem.Span;

// Span → Memory: not directly possible
// If you have a Span and need Memory, return a Memory from the source
// (array, MemoryPool, etc.) and slice that instead.
```

## Async I/O patterns

`Stream.ReadAsync` and `Stream.WriteAsync` accept `Memory<byte>` / `ReadOnlyMemory<byte>` (preferred overloads):

```csharp
// ✅ Preferred — Memory<byte> overload avoids boxing
byte[] buffer = new byte[4096];
int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 4096), cancellationToken);

// Writing
ReadOnlyMemory<byte> toWrite = Encoding.UTF8.GetBytes("hello").AsMemory();
await stream.WriteAsync(toWrite, cancellationToken);
```

Store the buffer as `Memory<byte>` in a class if multiple async calls share it:

```csharp
class Processor
{
    private readonly Memory<byte> _buffer = new byte[4096];

    public async Task ProcessAsync(Stream stream, CancellationToken ct)
    {
        int n;
        while ((n = await stream.ReadAsync(_buffer, ct)) > 0)
        {
            Process(_buffer.Span[..n]);
        }
    }

    private void Process(ReadOnlySpan<byte> data) { /* ... */ }
}
```

## ArrayPool\<T\> — rent and return

`ArrayPool<T>.Shared` is thread-safe and recycles arrays to avoid GC pressure. Always use a `try/finally` to ensure the array is returned:

```csharp
byte[] rented = ArrayPool<byte>.Shared.Rent(minimumLength: 4096);
try
{
    Span<byte> buf = rented.AsSpan(0, 4096); // rented array may be larger
    // use buf...
}
finally
{
    ArrayPool<byte>.Shared.Return(rented, clearArray: false);
}
```

> `Rent` returns an array of **at least** the requested size. Always slice to the exact length you need before using it.

## IMemoryOwner\<T\> and MemoryPool\<T\>

When you need to transfer buffer ownership across components, use `IMemoryOwner<T>` (from `System.Buffers`). Whoever disposes the owner releases the buffer back to the pool:

```csharp
using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(minimumBufferSize: 4096);
Memory<byte> mem = owner.Memory;

// Pass mem (not owner) to consumers — they may not dispose it
await FillAsync(mem, stream, ct);
Process(mem.Span);
// owner.Dispose() called automatically by 'using'
```

Key rule: if your method **returns** an `IMemoryOwner<T>` to the caller, you are transferring ownership — the caller is now responsible for disposal. If you keep it, dispose it yourself.

## Ownership transfer pattern

```csharp
// ✅ Caller takes ownership — method does NOT dispose
static IMemoryOwner<byte> ReadAll(Stream stream)
{
    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(4096);
    // fill owner.Memory...
    return owner; // caller must Dispose()
}

// Usage:
using IMemoryOwner<byte> data = ReadAll(stream);
Process(data.Memory.Span);
```

## Slicing Memory\<T\>

Like `Span<T>`, `Memory<T>` supports `Slice` and range indexers:

```csharp
Memory<byte> mem = buffer.AsMemory();
Memory<byte> first100 = mem[..100];
Memory<byte> rest     = mem[100..];
Memory<byte> mid      = mem.Slice(50, 50);
```

## ReadOnlyMemory\<T\> and strings

`string.AsMemory()` returns `ReadOnlyMemory<char>`, enabling async-safe zero-allocation string handling:

```csharp
ReadOnlyMemory<char> msg = "Hello, World!".AsMemory();
await writer.WriteAsync(msg, ct);
```

## Memory\<T\> in pipelines (System.IO.Pipelines)

If you're processing streams at high throughput, consider `System.IO.Pipelines` — it wraps buffers in `ReadOnlySequence<byte>` which spans multiple non-contiguous `Memory<byte>` segments. Use `ReadOnlySequence<byte>.IsSingleSegment` to fast-path the common case:

```csharp
if (sequence.IsSingleSegment)
{
    Process(sequence.FirstSpan);
}
else
{
    foreach (ReadOnlyMemory<byte> segment in sequence)
        Process(segment.Span);
}
```
