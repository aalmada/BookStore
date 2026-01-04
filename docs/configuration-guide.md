# Configuration Guide

This guide covers configuration management in the BookStore application, including the Options pattern, validation, and best practices.

## Overview

The BookStore application uses the **Options pattern** for strongly-typed configuration with:
- **Type-safe access** to configuration values
- **Validation on startup** to catch configuration errors early
- **Data annotations** for declarative validation
- **Custom validation** for complex business rules

## Configuration Files

### appsettings.json

Main configuration file for the application:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Marten": "Information",
      "Wolverine": "Information",
      "BookStore": "Information"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ",
        "UseUtcTimestamp": true,
        "JsonWriterOptions": {
          "Indented": false
        }
      }
    }
  },
  "AllowedHosts": "*",
  "Pagination": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  },
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["pt", "en", "fr", "de", "es"]
  }
}
```

### appsettings.Development.json

Development-specific overrides:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

### Environment-Specific Configuration

Configuration files are loaded in order:
1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific)
3. User secrets (Development only)
4. Environment variables
5. Command-line arguments

---

## Options Pattern

### Creating an Options Class

Options classes are strongly-typed representations of configuration sections.

**Example: PaginationOptions**

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Models;

/// <summary>
/// Configuration options for pagination
/// </summary>
public sealed record PaginationOptions : IValidatableObject
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Pagination";

    /// <summary>
    /// Default value for page size when not specified
    /// </summary>
    public const int DefaultPageSizeValue = 20;

    /// <summary>
    /// Default value for maximum number of items allowed per page
    /// </summary>
    public const int MaxPageSizeValue = 100;

    /// <summary>
    /// Default page size when not specified
    /// </summary>
    [Range(1, 1000, ErrorMessage = "DefaultPageSize must be between 1 and 1000")]
    public int DefaultPageSize { get; init; } = DefaultPageSizeValue;

    /// <summary>
    /// Maximum number of items allowed per page
    /// </summary>
    [Range(1, 1000, ErrorMessage = "MaxPageSize must be between 1 and 1000")]
    public int MaxPageSize { get; init; } = MaxPageSizeValue;

    /// <summary>
    /// Validates that DefaultPageSize is less than or equal to MaxPageSize
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DefaultPageSize > MaxPageSize)
        {
            yield return new ValidationResult(
                $"DefaultPageSize ({DefaultPageSize}) cannot be greater than MaxPageSize ({MaxPageSize})",
                [nameof(DefaultPageSize), nameof(MaxPageSize)]);
        }
    }
}
```

**Key Features**:
- **Sealed record** - Immutable with value-based equality
- **SectionName constant** - References the configuration section
- **Default values** - Provides fallbacks
- **Data annotations** - Declarative validation (`[Range]`, `[Required]`, etc.)
- **IValidatableObject** - Complex cross-property validation

> [!NOTE]
> **Record vs Class for Configuration**
> 
> The BookStore project uses `sealed record` with **explicit properties** for configuration options, consistent with the use of records for DTOs, commands, and events throughout the codebase.
> 
> **Current approach (explicit properties):**
> 
> ```csharp
> public sealed record PaginationOptions : IValidatableObject
> {
>     [Range(1, 1000)]
>     public int DefaultPageSize { get; init; } = 20;
> 
>     [Range(1, 1000)]
>     public int MaxPageSize { get; init; } = 100;
> 
>     public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
>     {
>         if (DefaultPageSize > MaxPageSize)
>         {
>             yield return new ValidationResult(
>                 $"DefaultPageSize cannot be greater than MaxPageSize",
>                 [nameof(DefaultPageSize), nameof(MaxPageSize)]);
>         }
>     }
> }
> ```
> 
> **Alternative approach (primary constructor with attributes):**
> 
> You **can** add attributes to primary constructor parameters (since C# 11):
> 
> ```csharp
> public sealed record PaginationOptions(
>     [property: Range(1, 1000)] int DefaultPageSize = 20,
>     [property: Range(1, 1000)] int MaxPageSize = 100)
> {
>     // But IValidatableObject is harder to implement cleanly
> }
> ```
> 
> **Why the project uses explicit properties:**
> 
> 1. **Readability** - Clearer and more familiar syntax
> 2. **IValidatableObject** - Easier to implement complex validation
> 3. **XML documentation** - Can add `<summary>` tags to each property
> 4. **Consistency** - Matches the pattern used in DTOs (which also use explicit properties)
> 5. **Flexibility** - Easier to add computed properties or additional logic
> 
> **Trade-offs:**
> 
> | Aspect | Explicit Properties | Primary Constructor |
> |--------|-------------------|---------------------|
> | Conciseness | More verbose | Very concise |
> | Attributes | Natural syntax | Requires `[property:]` target |
> | XML docs | Easy (`<summary>` per property) | Harder (on parameters) |
> | IValidatableObject | Easy to implement | Awkward to implement |
> | Computed properties | Natural | Requires separate declaration |
> 
> Both approaches work perfectly with the Options pattern and `IConfiguration.Bind()`. The choice is stylistic and based on your team's preferences.




### Registering Options

Register options in `Program.cs` or extension methods:

```csharp
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**Explanation**:
- **`AddOptions<T>()`** - Registers the options type
- **`Bind()`** - Binds configuration section to the options class
- **`ValidateDataAnnotations()`** - Enables data annotation validation
- **`ValidateOnStart()`** - Validates configuration at application startup (fails fast)

---

## Validation

### Data Annotation Validation

Use standard validation attributes for simple validation:

```csharp
public sealed record LocalizationOptions : IValidatableObject
{
    [Required(ErrorMessage = "DefaultCulture is required")]
    [MinLength(2, ErrorMessage = "DefaultCulture must be at least 2 characters")]
    [ValidCulture]
    public string DefaultCulture { get; init; } = "en-US";

    [Required(ErrorMessage = "SupportedCultures is required")]
    [MinLength(1, ErrorMessage = "At least one supported culture must be specified")]
    [ValidCulture]
    public string[] SupportedCultures { get; init; } = ["en-US"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SupportedCultures.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"DefaultCulture '{DefaultCulture}' must be included in SupportedCultures",
                [nameof(DefaultCulture), nameof(SupportedCultures)]);
        }
    }
}
```

**Common Validation Attributes**:
- `[Required]` - Value must be provided
- `[Range(min, max)]` - Numeric range validation
- `[MinLength(n)]` / `[MaxLength(n)]` - String/array length validation
- `[RegularExpression(pattern)]` - Pattern matching
- `[EmailAddress]`, `[Url]`, `[Phone]` - Format validation

### Custom Validation Attributes

Create custom validation attributes for reusable validation logic:

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Infrastructure;

/// <summary>
/// Validates that a string is a valid culture identifier
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidCultureAttribute : ValidationAttribute
{
    public ValidCultureAttribute()
        : base("The value '{0}' is not a valid culture identifier")
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is string culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return new ValidationResult(
                    "Culture identifier cannot be empty",
                    [validationContext.MemberName!]);
            }

            if (!CultureCache.IsValidCultureName(culture))
            {
                return new ValidationResult(
                    FormatErrorMessage(validationContext.DisplayName),
                    [validationContext.MemberName!]);
            }

            return ValidationResult.Success;
        }

        if (value is IEnumerable<string> cultures)
        {
            var invalidCodes = CultureCache.GetInvalidCodes(cultures);

            if (invalidCodes.Count > 0)
            {
                return new ValidationResult(
                    $"The following culture identifiers are invalid: {string.Join(", ", invalidCodes)}",
                    [validationContext.MemberName!]);
            }

            return ValidationResult.Success;
        }

        return new ValidationResult(
            $"The value must be a string or IEnumerable<string>, but was {value.GetType().Name}",
            [validationContext.MemberName!]);
    }
}
```

**Usage**:
```csharp
[ValidCulture]
public string DefaultCulture { get; set; } = "en-US";

[ValidCulture]
public string[] SupportedCultures { get; set; } = ["en-US"];
```

### IValidatableObject

Implement `IValidatableObject` for complex validation that involves multiple properties:

```csharp
public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
{
    // Cross-property validation
    if (DefaultPageSize > MaxPageSize)
    {
        yield return new ValidationResult(
            $"DefaultPageSize ({DefaultPageSize}) cannot be greater than MaxPageSize ({MaxPageSize})",
            [nameof(DefaultPageSize), nameof(MaxPageSize)]);
    }

    // Business rule validation
    if (MaxPageSize > 1000)
    {
        yield return new ValidationResult(
            "MaxPageSize cannot exceed 1000 for performance reasons",
            [nameof(MaxPageSize)]);
    }
}
```

### ValidateOnStart

**Rule**: Always use `ValidateOnStart()` for production applications.

```csharp
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();  // ✅ Validates at startup
```

**Benefits**:
- ✅ **Fail fast** - Catches configuration errors at startup, not at runtime
- ✅ **Clear error messages** - Shows exactly what's wrong with the configuration
- ✅ **Prevents deployment issues** - Invalid configuration prevents the app from starting

**Without ValidateOnStart**:
```csharp
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName))
    .ValidateDataAnnotations();
    // ❌ Validation only happens when options are first accessed
```

---

## Accessing Configuration

### Using IOptions<T>

Inject `IOptions<T>` to access configuration:

```csharp
public class BookEndpoints
{
    static async Task<Ok<PagedListDto<BookDto>>> SearchBooks(
        IQuerySession session,
        IOptions<PaginationOptions> paginationOptions,  // ✅ Inject IOptions<T>
        PagedRequest request)
    {
        var paging = request.Normalize(paginationOptions.Value);
        // Use paginationOptions.Value to access the configuration
        ...
    }
}
```

### IOptions vs IOptionsSnapshot vs IOptionsMonitor

| Type | Lifetime | Reloads Config | Use Case |
|------|----------|----------------|----------|
| `IOptions<T>` | Singleton | No | Static configuration that doesn't change |
| `IOptionsSnapshot<T>` | Scoped | Yes (per request) | Configuration that may change between requests |
| `IOptionsMonitor<T>` | Singleton | Yes (real-time) | Configuration that changes during runtime |

**Recommendation**: Use `IOptions<T>` for most cases. The BookStore application uses static configuration that doesn't change at runtime.

---

## Configuration Best Practices

### 1. Use Strongly-Typed Options

✅ **Correct**:
```csharp
public sealed record PaginationOptions
{
    public int DefaultPageSize { get; init; } = 20;
    public int MaxPageSize { get; init; } = 100;
}

// Usage
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection("Pagination"));
```

❌ **Incorrect**:
```csharp
// Direct configuration access - not type-safe
var defaultPageSize = configuration.GetValue<int>("Pagination:DefaultPageSize");
```

### 2. Always Validate Configuration

✅ **Correct**:
```csharp
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();  // ✅ Validates at startup
```

❌ **Incorrect**:
```csharp
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName));
    // ❌ No validation - errors discovered at runtime
```

### 3. Provide Default Values

✅ **Correct**:
```csharp
public sealed record PaginationOptions
{
    public int DefaultPageSize { get; init; } = 20;  // ✅ Default value
    public int MaxPageSize { get; init; } = 100;     // ✅ Default value
}
```

❌ **Incorrect**:
```csharp
public sealed record PaginationOptions
{
    public int DefaultPageSize { get; init; }  // ❌ No default - could be 0
    public int MaxPageSize { get; init; }      // ❌ No default - could be 0
}
```

### 4. Use Constants for Section Names

✅ **Correct**:
```csharp
public sealed record PaginationOptions
{
    public const string SectionName = "Pagination";  // ✅ Constant
}

// Usage
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName));
```

❌ **Incorrect**:
```csharp
// ❌ Magic string - prone to typos
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection("Pagination"));
```

### 5. Document Configuration Options

✅ **Correct**:
```csharp
/// <summary>
/// Configuration options for pagination
/// </summary>
/// <remarks>
/// Configure in appsettings.json:
/// <code>
/// {
///   "Pagination": {
///     "DefaultPageSize": 20,
///     "MaxPageSize": 100
///   }
/// }
/// </code>
/// </remarks>
public sealed class PaginationOptions
{
    /// <summary>
    /// Default page size when not specified
    /// </summary>
    [Range(1, 1000, ErrorMessage = "DefaultPageSize must be between 1 and 1000")]
    public int DefaultPageSize { get; init; } = 20;
}
```

### 6. Use Sealed Classes

✅ **Correct**:
```csharp
public sealed record PaginationOptions  // ✅ Sealed
{
    ...
}
```

❌ **Incorrect**:
```csharp
public record PaginationOptions  // ❌ Not sealed - can be inherited
{
    ...
}
```

**Reason**: Options should be sealed to prevent inheritance and ensure immutability.

### 7. Use Init-Only Properties

✅ **Correct**:
```csharp
public int DefaultPageSize { get; init; } = 20;  // ✅ Init-only
```

❌ **Incorrect**:
```csharp
public int DefaultPageSize { get; set; } = 20;  // ❌ Mutable
```

**Reason**: Configuration should be immutable after binding.

---

## Common Configuration Patterns

### Pagination Configuration

```json
{
  "Pagination": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}
```

```csharp
public sealed class PaginationOptions : IValidatableObject
{
    public const string SectionName = "Pagination";

    [Range(1, 1000)]
    public int DefaultPageSize { get; init; } = 20;

    [Range(1, 1000)]
    public int MaxPageSize { get; init; } = 100;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DefaultPageSize > MaxPageSize)
        {
            yield return new ValidationResult(
                $"DefaultPageSize cannot be greater than MaxPageSize",
                [nameof(DefaultPageSize), nameof(MaxPageSize)]);
        }
    }
}
```

### Localization Configuration

```json
{
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["pt", "en", "fr", "de", "es"]
  }
}
```

```csharp
public class LocalizationOptions : IValidatableObject
{
    public const string SectionName = "Localization";

    [Required]
    [MinLength(2)]
    [ValidCulture]
    public string DefaultCulture { get; set; } = "en-US";

    [Required]
    [MinLength(1)]
    [ValidCulture]
    public string[] SupportedCultures { get; set; } = ["en-US"];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SupportedCultures.Contains(DefaultCulture, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"DefaultCulture must be included in SupportedCultures",
                [nameof(DefaultCulture), nameof(SupportedCultures)]);
        }
    }
}
```

---

## Logging Configuration

### Structured Logging

The BookStore application uses structured JSON logging:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Marten": "Information",
      "Wolverine": "Information",
      "BookStore": "Information"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ",
        "UseUtcTimestamp": true,
        "JsonWriterOptions": {
          "Indented": false
        }
      }
    }
  }
}
```

**Key Settings**:
- **FormatterName**: `"json"` for structured logging
- **TimestampFormat**: ISO 8601 format with milliseconds
- **UseUtcTimestamp**: Always use UTC (consistent with time standards)
- **IncludeScopes**: Include logging scopes for correlation

---

## Authentication Configuration

The BookStore uses a **Token-based authentication model** with JWT and Passkeys.

### JWT Configuration

Required for token generation and validation.

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-must-be-at-least-32-characters-long-for-hs256",
    "Issuer": "BookStore.ApiService",
    "Audience": "BookStore.Web",
    "ExpirationMinutes": 60
  }
}
```

**Key Settings**:
- **SecretKey**: Strong cryptographic key (min 32 chars) for signing tokens. **Must be kept secret in production.**
- **Issuer**: The authority issuing the token (e.g., your API domain).
- **Audience**: The intended recipient of the token (e.g., your Web App).
- **ExpirationMinutes**: Lifetime of the Access Token.

### Passkey Configuration

Required for WebAuthn/FIDO2 operations.

```json
{
  "Authentication": {
    "Passkey": {
      "ServerDomain": "localhost"
    }
  }
}
```

**Key Settings**:
- **ServerDomain**: The domain where the passkey is valid (the Origin).
    - **Development**: Use `localhost`.
    - **Production**: **MUST** match your public domain (e.g., `bookstore.com`). Do not include protocol or port.

> [!WARNING]
> **Production Criticality**
> Failing to set `ServerDomain` correctly in production will cause Passkey registration and login to fail with "Domain mismatch" or "NotAllowed" errors.

### Email Configuration

Required for email verification.

```json
{
  "Email": {
    "DeliveryMethod": "Smtp", // None, Logging, or Smtp
    "BaseUrl": "https://localhost:7260",
    "FromEmail": "noreply@bookstore.com",
    "FromName": "BookStore",
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpUsername": "username",
    "SmtpPassword": "password"
  }
}
```

**Key Settings**:
- **DeliveryMethod**:
    - `None`: Disables email sending. **Users are auto-verified.**
    - `Logging`: Logs email content to console (Development).
    - `Smtp`: Sends actual emails via SMTP (Production).
- **BaseUrl**: The base URL of the frontend application (used for verification links).
- **FromEmail/FromName**: Sender details.
- **Smtp***: SMTP server credentials (required if method is `Smtp`).

---

## Environment Variables

Override configuration using environment variables:

```bash
# Format: {SectionName}__{PropertyName}
export Pagination__DefaultPageSize=50
export Pagination__MaxPageSize=200
export Localization__DefaultCulture=pt

# Run the application
dotnet run
```

**Naming Convention**:
- Use double underscore (`__`) to separate section and property names
- Case-insensitive (but use PascalCase for consistency)

---

## User Secrets (Development)

Store sensitive configuration in user secrets during development:

```bash
# Initialize user secrets
dotnet user-secrets init --project src/BookStore.ApiService

# Set a secret
dotnet user-secrets set "ConnectionStrings:bookstore" "Host=localhost;Database=bookstore;Username=postgres;Password=secret"

# List secrets
dotnet user-secrets list --project src/BookStore.ApiService
```

> [!WARNING]
> User secrets are for **development only**. Use environment variables, Azure Key Vault, or other secure storage for production.

---

## Troubleshooting

### Configuration Not Loading

**Symptom**: Options have default values instead of configured values.

**Solution**: Check the section name matches exactly:

```csharp
// ✅ Correct - matches "Pagination" in appsettings.json
.Bind(configuration.GetSection("Pagination"))

// ❌ Incorrect - case mismatch
.Bind(configuration.GetSection("pagination"))
```

### Validation Errors at Startup

**Symptom**: Application fails to start with validation error.

**Solution**: Check the error message and fix the configuration:

```
System.ComponentModel.DataAnnotations.ValidationException: 
DefaultPageSize (150) cannot be greater than MaxPageSize (100)
```

Fix in `appsettings.json`:
```json
{
  "Pagination": {
    "DefaultPageSize": 20,  // ✅ Fixed - less than MaxPageSize
    "MaxPageSize": 100
  }
}
```

### Options Always Null

**Symptom**: `IOptions<T>.Value` is null or has default values.

**Solution**: Ensure options are registered:

```csharp
// ✅ Register options
services.AddOptions<PaginationOptions>()
    .Bind(configuration.GetSection(PaginationOptions.SectionName));
```

---

## Summary

**Configuration Best Practices**:
1. ✅ Use strongly-typed options classes
2. ✅ Always validate with `ValidateDataAnnotations()` and `ValidateOnStart()`
3. ✅ Provide sensible default values
4. ✅ Use constants for section names
5. ✅ Document configuration options with XML comments
6. ✅ Use sealed classes for options
7. ✅ Use init-only properties for immutability
8. ✅ Implement `IValidatableObject` for complex validation
9. ✅ Create custom validation attributes for reusable logic
10. ✅ Use environment variables for environment-specific overrides

**Configuration Loading Order**:
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (Development)
4. Environment variables
5. Command-line arguments

These practices ensure:
- **Type safety** - Compile-time checking of configuration access
- **Early error detection** - Validation at startup prevents runtime errors
- **Maintainability** - Clear, documented configuration structure
- **Testability** - Easy to mock and test with different configurations
