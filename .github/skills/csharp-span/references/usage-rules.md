# Memory\<T\> and Span\<T\> Usage Rules

These 10 rules come from the official Microsoft .NET guidelines. They cover when to use each type, how to handle ownership, and how to avoid common pitfalls.

Source: https://learn.microsoft.com/dotnet/standard/memory-and-spans/memory-t-usage-guidelines

---

## Rule 1 — Prefer Span\<T\> over Memory\<T\> for synchronous parameters

`Span<T>` is more versatile and has lower overhead. Callers with `Memory<T>` can always call `.Span` to pass a span; the reverse is not true.

```csharp
// ✅ Accepts all sources: array, stackalloc, string (.AsSpan()), Memory<T>.Span
void Process(ReadOnlySpan<byte> data) { ... }

// ❌ Unnecessarily restrictive
void Process(ReadOnlyMemory<byte> data) { ... }
```

## Rule 2 — Use ReadOnlySpan\<T\> or ReadOnlyMemory\<T\> for read-only buffers

Signal intent clearly and enable the widest range of callers (including `string` for `ReadOnlySpan<char>`).

```csharp
// ✅
void Display(ReadOnlySpan<char> text) { ... }

// 'Display' can now be called with:
Display("literal");
Display(someString.AsSpan());
Display(stackalloc char[32]);
Display(someSpan);
```

## Rule 3 — If your method accepts Memory\<T\> and returns void, don't use the buffer after returning

Your lease ends when the method returns. The caller may modify or recycle the buffer immediately.

```csharp
// ✅ OK — all usage within the synchronous scope
void Fill(Memory<byte> buffer)
{
    buffer.Span.Fill(0);
} // lease ends here — never stash the Memory<T> for later use

// ❌ Violation — storing buffer for use outside the method's lease
Memory<byte> _saved;
void Fill(Memory<byte> buffer) { _saved = buffer; } // caller may now reuse it!
```

## Rule 4 — If your method accepts Memory\<T\> and returns Task, don't use the buffer after the Task completes

The lease ends when the `Task` reaches a terminal state (completed, faulted, cancelled).

```csharp
// ✅
async Task ProcessAsync(Memory<byte> buffer)
{
    await DoSomethingAsync(buffer);
    // buffer is used only up to this point
} // lease ends when Task is done

// ❌ Continuation captures buffer beyond Task completion
async Task ProcessAsync(Memory<byte> buffer)
{
    var task = DoSomethingAsync(buffer);
    await task;
    buffer.Span[0] = 0; // is the lease still valid? Only if caller guarantees it
}
```

## Rule 5 — Constructors that accept Memory\<T\> are consumers of that buffer

Objects that store `Memory<T>` in a field during construction are considered to hold that buffer for the lifetime of the object. Callers must not reuse or recycle the buffer while the object is alive.

## Rule 6 — Settable Memory\<T\> properties imply the same ownership semantics

Once set, instance methods may consume the buffer at any time. Document clearly who owns the buffer and for how long.

## Rule 7 — If you have an IMemoryOwner\<T\>, you must either dispose it or transfer ownership (never both)

`IMemoryOwner<T>` implements `IDisposable`. The owner is the single entity responsible for the buffer's lifetime.

```csharp
// ✅ Owner disposes
using IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(4096);
Process(owner.Memory.Span);
// 'using' disposes automatically

// ✅ Owner transfers and does NOT dispose
IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.Rent(4096);
return owner; // caller must Dispose
```

## Rule 8 — If your public API accepts IMemoryOwner\<T\>, you accept ownership

Your code is now responsible for disposal. Don't accept `IMemoryOwner<T>` in a public API unless you genuinely intend to take over the buffer's lifetime.

## Rule 9 — Synchronous P/Invoke wrappers should accept Span\<T\>

```csharp
// ✅ Synchronous native call
[DllImport("native")]
static extern int NativeRead(ref byte buffer, int length);

public static int Read(Span<byte> buffer)
{
    return NativeRead(ref MemoryMarshal.GetReference(buffer), buffer.Length);
}
```

## Rule 10 — Asynchronous P/Invoke wrappers should accept Memory\<T\>

```csharp
// ✅ Async native call
public static ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
{
    // pin and call asynchronously...
}
```

---

## Summary cheat-sheet

| Scenario | Use |
|----------|-----|
| Synchronous parameter | `Span<T>` |
| Read-only synchronous | `ReadOnlySpan<T>` |
| Async method parameter | `Memory<T>` |
| Read-only async | `ReadOnlyMemory<T>` |
| Stored in class field | `Memory<T>` |
| Buffer ownership management | `IMemoryOwner<T>` |
| Renting temporary buffers | `ArrayPool<T>.Shared` |
| Pooled buffers with ownership | `MemoryPool<T>.Shared` |
