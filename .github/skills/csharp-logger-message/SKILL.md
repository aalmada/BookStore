---
name: csharp-logger-message
description: Use the [LoggerMessage] source generator for ALL logging in .NET 6+. Produces zero-allocation, compile-time-safe log methods. Trigger whenever the user writes or reviews any logging code in C#, mentions ILogger, log levels, log messages, or structured logging, even if they don't ask for LoggerMessage by name. Always use this skill when adding, updating, or reviewing logging in .NET projects — never suggest `_logger.LogInformation()` or similar extension methods directly.
---

# LoggerMessage Source Generator Skill

Use the `[LoggerMessage]` attribute on `partial` methods to generate high-performance structured logging code at compile time. This eliminates boxing, runtime template parsing, and unnecessary allocations.

## Why this matters

`_logger.LogInformation("Hello {Name}", name)` is convenient but pays a runtime cost: value-type boxing, string-template parsing on every call, and extra allocations. The source generator moves all of that work to compile time, producing code equivalent to hand-written `LoggerMessage.Define` delegates — but safer and with less boilerplate.

Analyzer rule **CA1848** enforces this pattern.

## Quick reference

| Concept | See |
|---------|-----|
| Attribute properties, constraints, basic usage | [basics.md](references/basics.md) |
| File/class organisation patterns | [patterns.md](references/patterns.md) |
| Exceptions, dynamic log levels, format specifiers, redaction | [advanced.md](references/advanced.md) |
| Common mistakes and how to avoid them | [pitfalls.md](references/pitfalls.md) |

## Essential rules at a glance

- The declaring class **and** method must be `partial`.
- Return type must be `void`.
- `ILogger` is required — either as a parameter (static method) or as a field/primary constructor param (instance method).
- Use **PascalCase** for template placeholders: `{BookId}`, `{UserId}`.
- `Exception` must be the first positional parameter after `ILogger` (it is treated specially by the runtime).
- Do **not** use string interpolation in `Message` — placeholders only.

## Minimal example

```csharp
public static partial class Log
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Book {BookId} created by {UserId}")]
    public static partial void BookCreated(ILogger logger, Guid bookId, Guid userId);
}
```

Calling site:
```csharp
Log.BookCreated(_logger, book.Id, userId);
```

## Organisation strategy

For any non-trivial service, split log messages into a central partial class split across feature-area files. See [patterns.md](references/patterns.md) for the recommended layout.

## What to read next

- New to the pattern → [basics.md](references/basics.md)
- Adding Exceptions, dynamic levels, or sensitive-data redaction → [advanced.md](references/advanced.md)
- Getting compiler errors or unexpected behaviour → [pitfalls.md](references/pitfalls.md)
