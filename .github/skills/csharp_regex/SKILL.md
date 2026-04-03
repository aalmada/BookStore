---
name: csharp_regex
description: Use the [GeneratedRegex] source generator for ALL regex in .NET 7+ C#. Produces AOT-safe, trimming-friendly, compile-time regex with zero startup cost and better throughput than RegexOptions.Compiled. Trigger whenever the user writes, reviews, or asks about regex in C# — including new Regex(...), Regex.IsMatch, pattern matching, string validation, or text parsing — even if they don't mention source generation. Never suggest new Regex(...) with RegexOptions.Compiled when [GeneratedRegex] is available.
---

# C# Source-Generated Regex Skill

Use `[GeneratedRegex]` on a `partial` method or property so the compiler emits a fully-optimised, AOT-safe `Regex` implementation at build time. This delivers the throughput of `RegexOptions.Compiled` with near-zero startup cost, and the output is ordinary C# that you can read and debug.

## Why this matters

`new Regex(pattern, RegexOptions.Compiled)` pays a heavy startup cost (reflection-emit, JIT compilation) and is not AOT-safe. `[GeneratedRegex]` compiles the pattern into readable C# during the build, giving you:

- **Better throughput** than `RegexOptions.Compiled` (and more, in some benchmarks)
- **Near-zero startup cost** — no JIT compile at runtime
- **AOT / trimming safe** — no `Reflection.Emit`
- **Debuggable** — step through the generated C# in any debugger
- **Automatic singleton caching** — no `static readonly` field needed

Diagnostic `SYSLIB1045` fires when the compiler detects a place that could use `[GeneratedRegex]` but doesn't.

## Quick reference

| Concept | See |
|---------|-----|
| Constraints, method vs property, minimal examples | [basics.md](references/basics.md) |
| `RegexOptions`, timeouts, culture | [options.md](references/options.md) |
| Where to declare regex, file organisation | [patterns.md](references/patterns.md) |
| Common mistakes and compiler errors | [pitfalls.md](references/pitfalls.md) |

## Minimal examples

**Method (≥ .NET 7):**
```csharp
public static partial class Patterns
{
    [GeneratedRegex(@"\d+")]
    public static partial Regex Digits();
}
```

**Property (≥ .NET 9, C# 13):**
```csharp
public static partial class Patterns
{
    [GeneratedRegex(@"\d+")]
    public static partial Regex Digits { get; }
}
```

## Essential rules at a glance

- The **containing class** must be `partial`.
- Method form: must be `static`, `partial`, parameterless, non-generic, returning `Regex`.
- Property form: must be `static`, `partial`, getter-only, returning `Regex` (C# 13 / .NET 9+).
- Do **not** add `RegexOptions.Compiled` — it is ignored by the generator.
- The generator handles caching; no `static readonly` field is needed.

## What to read next

- First time using `[GeneratedRegex]` → [basics.md](references/basics.md)
- Need `IgnoreCase`, timeouts, or culture → [options.md](references/options.md)
- Deciding where to put the declaration → [patterns.md](references/patterns.md)
- Getting a compiler error → [pitfalls.md](references/pitfalls.md)
