# GeneratedRegex Basics

Source: https://learn.microsoft.com/dotnet/standard/base-types/regular-expression-source-generators
API ref: https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.generatedregexattribute?view=net-10.0

## Requirements

| Requirement | Detail |
|-------------|--------|
| Runtime | .NET 7+ (method); .NET 9+ for property form |
| Language | C# 11+ (method); C# 13+ (property) |
| Namespace | `System.Text.RegularExpressions` |
| NuGet | Built into `System.Text.RegularExpressions.dll` — no extra package |

## How it works

Applying `[GeneratedRegex]` to a `partial` method or property tells the Roslyn source generator (`System.Text.RegularExpressions.Generator`) to emit a complete `Regex`-derived class at compile time. The generated class:

- Embeds all match logic as readable C# (no `Reflection.Emit`)
- Caches a singleton instance so every call to the method / property returns the same object
- Requires no explicit `static readonly` field — the caching is inside the generated implementation

## Attribute constructors

```csharp
// Pattern only
[GeneratedRegex(string pattern)]

// Pattern + options
[GeneratedRegex(string pattern, RegexOptions options)]

// Pattern + options + culture
[GeneratedRegex(string pattern, RegexOptions options, string cultureName)]

// Pattern + options + timeout (ms)
[GeneratedRegex(string pattern, RegexOptions options, int matchTimeoutMilliseconds)]

// All four
[GeneratedRegex(string pattern, RegexOptions options, int matchTimeoutMilliseconds, string cultureName)]
```

## Method form (≥ .NET 7)

```csharp
using System.Text.RegularExpressions;

public static partial class Validators
{
    // basic
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    public static partial Regex IsoDateFormat();

    // with options
    [GeneratedRegex(@"^[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}$",
        RegexOptions.IgnoreCase, "en-US")]
    public static partial Regex Email();
}
```

Usage:
```csharp
bool isValid = Validators.IsoDateFormat().IsMatch("2024-01-15"); // true
```

Note: each call to `IsoDateFormat()` returns the **same cached** `Regex` instance — the call is cheap.

## Property form (≥ .NET 9, C# 13)

```csharp
public static partial class Validators
{
    [GeneratedRegex(@"^[A-Z]{2}\d{6}$")]
    public static partial Regex PassportNumber { get; }
}
```

Usage:
```csharp
bool ok = Validators.PassportNumber.IsMatch(input);
```

Prefer the property form if you use C# 13+ — it reads at the call site like a constant rather than a factory call.

## Instance method

When the regex is relevant only inside one class, declare it there directly:

```csharp
public partial class OrderValidator
{
    [GeneratedRegex(@"^\d{5}(-\d{4})?$")]
    private static partial Regex ZipCode();

    public bool IsValidZip(string zip) => ZipCode().IsMatch(zip);
}
```

The class must still be `partial`.
