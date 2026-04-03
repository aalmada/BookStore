# Organisation Patterns

Source: https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators

## Where to declare generated regex

The right placement depends on scope and reuse:

### Dedicated static class (recommended for shared patterns)

Centralise patterns that are used across multiple files or feature areas:

```csharp
// File: Patterns.cs (or RegexPatterns.cs)
using System.Text.RegularExpressions;

public static partial class Patterns
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    public static partial Regex IsoDate();

    [GeneratedRegex(@"^[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}$",
        RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex Email();

    [GeneratedRegex(@"^\+?[1-9]\d{6,14}$")]
    public static partial Regex PhoneE164();
}
```

Usage anywhere in the assembly:
```csharp
if (!Patterns.Email().IsMatch(input)) { ... }
```

### Declaring on the consuming class (private scope)

When only one class needs the pattern, declare it there to keep the scope narrow:

```csharp
public partial class InvoiceParser
{
    [GeneratedRegex(@"INV-\d{6}", RegexOptions.IgnoreCase)]
    private static partial Regex InvoiceNumber();

    public string? ExtractInvoiceNumber(string text) =>
        InvoiceNumber().Match(text).Value;
}
```

The class must be `partial` — if it isn't already, make it so.

### Feature-area file splitting

For large modules with many patterns, split a single `partial` class across multiple files by feature area:

```
Patterns/
  Patterns.cs          ← partial class declaration (no members)
  Patterns.Dates.cs    ← date-related patterns
  Patterns.Email.cs    ← email/address patterns
  Patterns.Finance.cs  ← currency, IBAN, card numbers
```

Each file:
```csharp
// Patterns.Dates.cs
public static partial class Patterns
{
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    public static partial Regex IsoDate();
}
```

### Co-location with a validator

When a pattern is the heart of a validation class, keep it co-located:

```csharp
public static partial class PostalCodeValidator
{
    [GeneratedRegex(@"^\d{5}(-\d{4})?$")]
    private static partial Regex UsZip();

    [GeneratedRegex(@"^[A-Z]{1,2}\d[A-Z\d]? ?\d[A-Z]{2}$",
        RegexOptions.IgnoreCase)]
    private static partial Regex UkPostcode();

    public static bool Validate(string code, string country) =>
        country switch
        {
            "US" => UsZip().IsMatch(code),
            "UK" => UkPostcode().IsMatch(code),
            _ => throw new ArgumentOutOfRangeException(nameof(country))
        };
}
```

## Naming conventions

| Pattern | Recommended name | Rationale |
|---------|-----------------|-----------|
| Method form | Noun or noun phrase: `Email()`, `IsoDate()`, `PhoneNumber()` | The `()` implies a call; keep the name a descriptor of what it matches |
| Property form | Same noun: `Email`, `IsoDate` | No parens, reads like a static constant |

Avoid prefixes like `Get`, `Create`, or `Match` — the attribute already communicates the nature of the member.

## Viewing the generated code

In Visual Studio: right-click the method or property declaration → **Go To Definition**.
Or expand **Dependencies → Analyzers → System.Text.RegularExpressions.Generator → RegexGenerator.g.cs** in Solution Explorer.

The generated code is fully readable C# and can be stepped through in the debugger.
