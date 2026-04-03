# RegexOptions, Timeouts, and Culture

Source: https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.regexoptions?view=net-10.0
Source: https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators

## RegexOptions reference

| Option | Notes |
|--------|-------|
| `IgnoreCase` | Case-insensitive match. Combine with `cultureName` for locale-aware casing. |
| `Multiline` | `^` and `$` match line boundaries rather than string start/end. |
| `Singleline` | `.` matches `\n` as well. |
| `IgnorePatternWhitespace` | Unescaped whitespace is ignored; allows inline comments with `#`. |
| `ExplicitCapture` | Only named groups capture; reduces allocations when you don't need unnamed groups. |
| `NonBacktracking` | Linear-time matching (no catastrophic backtracking). Not supported by the source generator — use with `new Regex(...)`. |
| `Compiled` | **Ignored by the source generator.** Do not include it. |
| `RightToLeft` | Match right-to-left. Supported by the source generator. |
| `CultureInvariant` | If combined with `IgnoreCase`, use invariant culture for case comparisons. |
| `ECMAScript` | ECMAScript-compatible behaviour. Cannot combine with most other options. |

Combine with bitwise OR:
```csharp
[GeneratedRegex(@"hello\s+world",
    RegexOptions.IgnoreCase | RegexOptions.Multiline)]
public static partial Regex HelloWorld();
```

## Culture for case-insensitive matching

When `IgnoreCase` is set, supply a BCP-47 culture name to pin the casing table to a specific locale. The source generator bakes the casing table at **compile time** (unlike `new Regex(...)`, which resolves it at runtime).

```csharp
// Turkish: 'I' casing differs from en-US
[GeneratedRegex(@"^[a-z]+$", RegexOptions.IgnoreCase, "tr-TR")]
public static partial Regex LowercaseTurkish();

// Invariant culture (portable, deterministic)
[GeneratedRegex(@"^[a-z]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
public static partial Regex LowercaseInvariant();
```

When no culture is specified and `IgnoreCase` is used, the generator uses the **invariant culture** at compile time. This differs from runtime `new Regex(...)` which uses the current culture. Always specify a culture when locale-sensitive matching matters.

## Timeouts

Pass a timeout (milliseconds) to guard against ReDoS on untrusted input. Use `Timeout.Infinite` (-1) only when the input is fully trusted and bounded.

```csharp
// 500 ms timeout — good for parsing untrusted user input
[GeneratedRegex(@"(\w+\s*)+", RegexOptions.None, matchTimeoutMilliseconds: 500)]
public static partial Regex WordSequence();

// All four parameters
[GeneratedRegex(@"^[a-z]+$",
    RegexOptions.IgnoreCase,
    matchTimeoutMilliseconds: 1000,
    cultureName: "en-US")]
public static partial Regex AlphaOnly();
```

> **Security:** Always set a timeout when the regex pattern or input comes from or is affected by user data. Polynomially-complex patterns (nested quantifiers, alternation with overlap) can cause runaway matching without a timeout.

## NonBacktracking and source generation

`RegexOptions.NonBacktracking` provides linear-time matching via a finite automaton engine. The source generator **does not support** this option — it will fall back to a runtime error or no-op. If you need linear-time guarantees, use `new Regex(pattern, RegexOptions.NonBacktracking)` and cache the instance in a `static readonly` field yourself.
