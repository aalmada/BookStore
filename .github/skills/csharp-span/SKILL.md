---
name: csharp-span
description: Use Span<T>, ReadOnlySpan<T>, Memory<T>, and ReadOnlyMemory<T> to write zero-allocation, high-performance C# code that avoids heap pressure — covering stackalloc buffers, array slicing without copying, zero-allocation string parsing, async-safe Memory<T>, ArrayPool<T> buffer reuse, and MemoryMarshal reinterpretation. Trigger whenever the user writes or reviews C# code that slices arrays, parses strings without allocation, processes byte buffers, reads/writes binary data, uses stackalloc, rents buffers, works with async I/O pipelines, or asks about avoiding heap allocations — even if they don't mention Span or Memory by name. Always prefer this skill over guessing; Span<T> and Memory<T> have subtle ref-struct restrictions and ownership rules that are easy to get wrong.
---

# C# Span\<T\> and Memory\<T\> Skill

`Span<T>` and `Memory<T>` provide a unified, allocation-free abstraction over contiguous memory — whether that memory lives on the stack, heap, or in unmanaged storage. The goal is always the same: let you work with slices of data without copying them.

## Type overview

| Type | `ref struct`? | Heap-storable? | Async-safe? | Use when |
|------|:---:|:---:|:---:|---------|
| `Span<T>` | ✅ | ❌ | ❌ | Synchronous, stack-only work |
| `ReadOnlySpan<T>` | ✅ | ❌ | ❌ | Reading without mutation |
| `Memory<T>` | ❌ | ✅ | ✅ | Async methods, stored fields |
| `ReadOnlyMemory<T>` | ❌ | ✅ | ✅ | Async read-only access |

**Key rule**: prefer `Span<T>` for synchronous APIs; use `Memory<T>` only when you need to store or await across the buffer. Both have read-only counterparts — always prefer those when you don't need to mutate.

## Reference files

| Topic | File |
|-------|------|
| `Span<T>` creation, slicing, string ops, stackalloc, common methods | [references/span.md](references/span.md) |
| `Memory<T>`, async patterns, `IMemoryOwner<T>`, `ArrayPool<T>`, `MemoryPool<T>` | [references/memory.md](references/memory.md) |
| `MemoryMarshal` — reinterpretation, unsafe pointer access, `CreateSpan` | [references/memorymarshal.md](references/memorymarshal.md) |
| Official usage rules (10 rules from Microsoft docs) | [references/usage-rules.md](references/usage-rules.md) |

Load the reference file(s) relevant to the user's task before writing code. For string parsing or zero-alloc text work, start with `span.md`. For async I/O buffers, start with `memory.md`. For reinterpreting raw bytes as structs, start with `memorymarshal.md`. When in doubt about ownership or API design, read `usage-rules.md`.

## Choosing `Span<T>` vs `Memory<T>`

The practical decision rule:

- **Synchronous and doesn't cross `async` boundaries?** → `Span<T>` / `ReadOnlySpan<T>`  
- **Used in `async` methods, stored as a field, or passed to a constructor?** → `Memory<T>` / `ReadOnlyMemory<T>`  
- **Callers have `Memory<T>` but you want `Span<T>`?** → call `.Span` to convert at the consumption site  
- **Wrapping a P/Invoke?** → sync = `Span<T>`, async = `Memory<T>`

## Common entry points

```csharp
// From array
Span<byte> s = array.AsSpan();
Span<byte> slice = array.AsSpan(offset, length);

// From string (read-only)
ReadOnlySpan<char> chars = text.AsSpan();

// Stack allocation (small buffers only — no GC pressure)
Span<byte> buf = stackalloc byte[256];

// From Memory<T>
Span<byte> fromMem = memory.Span;
```

## Critical constraints

`Span<T>` is a `ref struct` — the compiler enforces these at compile time:

- Cannot be stored in a class field or interface
- Cannot be a generic type argument (`List<Span<T>>` is illegal)
- Cannot be used across `await` boundaries
- Cannot be boxed

If any of these constraints apply, switch to `Memory<T>`.
