# Common Mistakes and Compiler Errors

Source: https://learn.microsoft.com/dotnet/api/system.text.regularexpressions.generatedregexattribute?view=net-10.0

## Compiler errors

### SYSLIB1040 / SYSLIB1043 — not partial / wrong return type

```csharp
// ❌ method is not partial
[GeneratedRegex(@"\d+")]
public static Regex Digits() => ...; // compiler error SYSLIB1043

// ✅ fix
[GeneratedRegex(@"\d+")]
public static partial Regex Digits();
```

```csharp
// ❌ return type is not Regex
[GeneratedRegex(@"\d+")]
public static partial bool IsDigits(); // compiler error

// ✅ must return Regex
[GeneratedRegex(@"\d+")]
public static partial Regex Digits();
```

### SYSLIB1044 — containing class is not partial

```csharp
// ❌ containing class missing partial
public class MyClass
{
    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits(); // SYSLIB1044
}

// ✅ fix
public partial class MyClass
{
    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
```

### SYSLIB1045 — diagnostic for upgrade opportunity

This is a **warning** (not an error). It fires on `new Regex(...)` calls with constant patterns that could be source-generated. Treat it as a prompt to migrate.

### Method has parameters

```csharp
// ❌ parameterless is required for method form
[GeneratedRegex(@"\d+")]
public static partial Regex Digits(string input); // compiler error

// ✅
[GeneratedRegex(@"\d+")]
public static partial Regex Digits();
```

### Non-static method without ILogger-style DI

Unlike `[LoggerMessage]`, `[GeneratedRegex]` does not support instance methods at all — only `static partial` methods/properties. Instance methods on a `partial` class must still delegate to a static member:

```csharp
public partial class Validator
{
    [GeneratedRegex(@"\d{3}-\d{2}-\d{4}")]
    private static partial Regex Ssn();

    public bool ValidateSsn(string s) => Ssn().IsMatch(s);
}
```

## Runtime mistakes

### Including RegexOptions.Compiled

```csharp
// ❌ Compiled is ignored; source generator handles this
[GeneratedRegex(@"\d+", RegexOptions.Compiled)]
public static partial Regex Digits();

// ✅ drop RegexOptions.Compiled entirely
[GeneratedRegex(@"\d+")]
public static partial Regex Digits();
```

The `Compiled` flag is silently ignored by the generator. It does not cause an error, but it misleads readers into thinking there's a meaningful difference.

### Caching a second time

```csharp
// ❌ unnecessary — generated implementation already caches the Regex
private static readonly Regex _digits = Digits();

// ✅ just call the method directly
bool isDigit = Digits().IsMatch(input);
```

### Using NonBacktracking with the source generator

`RegexOptions.NonBacktracking` is **not supported** by the source generator and will produce a build error. If you need linear-time matching, instantiate `Regex` manually:

```csharp
// For linear-time matching on untrusted input
private static readonly Regex _safePattern =
    new Regex(@"(a+)+b", RegexOptions.NonBacktracking);
```

### Culture casing mismatch

`IgnoreCase` without a `cultureName` bakes in the **invariant culture** at compile time. If the pattern was previously evaluated at runtime with the current culture, results may differ. Always pass an explicit culture name:

```csharp
// ❌ culture unspecified — uses invariant at compile time
[GeneratedRegex(@"[a-z]+", RegexOptions.IgnoreCase)]
public static partial Regex Alpha();

// ✅ culture is explicit and deterministic
[GeneratedRegex(@"[a-z]+", RegexOptions.IgnoreCase, "en-US")]
public static partial Regex Alpha();
```

### Forgetting a timeout on user-controlled input

Any regex consuming external/user-supplied text should have a timeout to prevent ReDoS:

```csharp
// ❌ no timeout on a potentially expensive pattern
[GeneratedRegex(@"(\w+\s*)+")]
public static partial Regex WordSeq();

// ✅ 500 ms is a reasonable default for interactive input
[GeneratedRegex(@"(\w+\s*)+", RegexOptions.None, matchTimeoutMilliseconds: 500)]
public static partial Regex WordSeq();
```
