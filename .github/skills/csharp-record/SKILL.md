---
name: csharp-record
description: Use C# records to model immutable, data-centric types with value equality, nondestructive mutation (`with`), and concise primary-constructor syntax. Trigger whenever the user writes, reviews, or asks about C# `record`, `record class`, `record struct`, `readonly record struct`, DTOs, value objects, positional parameters, `with` expressions, or data models — even if they don't mention "record" by name. Always prefer records over classes or structs for data-only types where value equality and immutability are beneficial.
---

# C# Records Skill

Records are a first-class way to express **data-centric types** in C#. The compiler synthesises value equality, `ToString`, `Deconstruct`, and nondestructive-copy (`with`) support automatically, so you get correct behaviour with minimal code.

## Why this matters

Hand-rolling equality on a class is tedious and error-prone. Records eliminate that boilerplate and communicate intent clearly: a `record` says "this type exists to hold data", the same way `IEnumerable<T>` says "this type is a sequence". You also get:

- **Value equality** — two records with the same data are equal by `==` and `.Equals`.
- **Nondestructive mutation** — `with` expressions copy and patch without mutable state.
- **Built-in `ToString`** — for free structured logging and debugging.
- **Deconstruction** — positional records deconstruct out of the box.
- **Thread safety** — immutable records are safe to share across threads without locking.

## Quick reference

| Topic | See |
|-------|-----|
| All declaration forms (`record`, `record class`, `record struct`, `readonly record struct`) | [declaration.md](references/declaration.md) |
| Immutability, `init`, shallow vs deep immutability, when to prefer mutable | [immutability.md](references/immutability.md) |
| Value equality, `with` expressions, `ToString`, `Deconstruct` | [features.md](references/features.md) |
| Record inheritance, abstract records, equality in hierarchies | [inheritance.md](references/inheritance.md) |
| Common mistakes and when NOT to use records | [pitfalls.md](references/pitfalls.md) |

## Choosing the right form at a glance

| Form | Reference type? | Positional immutable by default | Use for |
|------|-----------------|----------------------------------|---------|
| `record` / `record class` | ✅ | ✅ (`init`) | DTOs, domain events, value objects |
| `readonly record struct` | ❌ (value) | ✅ (`init`) | Small 2–4 field value types on hot paths |
| `record struct` | ❌ (value) | ❌ (mutable) | Rarely — only when you need mutable struct semantics |

When in doubt, reach for `record` (a reference type). Switch to `readonly record struct` only when profiling shows heap pressure from many small allocations.

## Minimal examples

**Positional (most concise):**
```csharp
public record Point(double X, double Y);
```

**Explicit properties (more control):**
```csharp
public record Point
{
    public required double X { get; init; }
    public required double Y { get; init; }
}
```

**Value-type variant:**
```csharp
public readonly record struct Point(double X, double Y);
```

## What to read next

- Deciding which form to use → [declaration.md](references/declaration.md)
- Controlling mutability and `init` → [immutability.md](references/immutability.md)
- Working with value equality and `with` → [features.md](references/features.md)
- Building record hierarchies → [inheritance.md](references/inheritance.md)
- Getting unexpected behaviour → [pitfalls.md](references/pitfalls.md)
