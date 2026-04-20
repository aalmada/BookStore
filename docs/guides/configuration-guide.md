# Configuration Guide

This guide covers configuration management in the BookStore application, including the Options pattern, validation, and best practices.

## Overview

The BookStore application uses the **Options pattern** for strongly-typed configuration with:
- **Type-safe access** to configuration values
- **Validation on startup** to catch configuration errors early
- **Data annotations** for declarative validation
- **Custom validation** for complex business rules

## Configuration Files

### BookStore.ApiService — appsettings.json

Main configuration file for the API service:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting": "Information",
      "Microsoft.AspNetCore.Routing": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "Marten": "Warning",
      "Npgsql": "Warning",
      "Wolverine": "Warning",
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
    "SupportedCultures": ["pt", "pt-PT", "en", "fr", "de", "es"]
  },
  "Currency": {
    "DefaultCurrency": "GBP",
    "SupportedCurrencies": ["USD", "EUR", "GBP"]
  },
  "Authentication": {
    "Passkey": {
      "ServerDomain": "localhost",
      "AllowedOrigins": ["https://localhost:7260"]
    }
  },
  "Email": {
    "DeliveryMethod": "Logging",
    "BaseUrl": "https://localhost:7260"
  },
  "Jwt": {
    "Algorithm": "HS256",
    "SecretKey": "",
    "Issuer": "BookStore.ApiService",
    "Audience": "BookStore.Web",
    "ExpirationMinutes": 15
  },
  "RateLimit": {
    "PermitLimit": 1000,
    "WindowInMinutes": 1,
    "QueueLimit": 100,
    "AuthPermitLimit": 20,
    "AuthWindowSeconds": 60,
    "AuthQueueLimit": 5,
    "NotificationSseTokenLimit": 20,
    "NotificationSseTokensPerPeriod": 2,
    "NotificationSseReplenishmentPeriodSeconds": 1,
    "NotificationSseQueueLimit": 0
  },
  "Account": {
    "Cleanup": {
      "UnverifiedAccountExpirationHours": 24,
      "CleanupIntervalHours": 1,
      "Enabled": true
    }
  }
}
```

### BookStore.ApiService — appsettings.Development.json

Development-specific overrides. Note that the console formatter switches to `"simple"` for human-readable local output:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.AspNetCore.Hosting": "Information",
      "Microsoft.AspNetCore.Routing": "Debug",
      "Microsoft.AspNetCore.Identity": "Debug",
      "System.Net.Http.HttpClient": "Information",
      "Marten": "Debug",
      "Wolverine": "Information",
      "BookStore": "Debug"
    },
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
        "IncludeScopes": true,
        "SingleLine": false,
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss ",
        "UseUtcTimestamp": true
      }
    }
  },
  "Authentication": {
    "Passkey": {
      "AllowedOrigins": ["https://localhost:7260"]
    }
  },
  "Jwt": {
    "Algorithm": "HS256",
    "SecretKey": "your-secret-key-must-be-at-least-32-characters-long-for-hs256"
  },
  "RateLimit": {
    "PermitLimit": 2000,
    "AuthPermitLimit": 200,
    "AuthWindowSeconds": 60,
    "AuthQueueLimit": 10,
    "NotificationSseTokenLimit": 60,
    "NotificationSseTokensPerPeriod": 5,
    "NotificationSseReplenishmentPeriodSeconds": 1,
    "NotificationSseQueueLimit": 0
  },
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["pt", "pt-PT", "en", "fr", "de", "es"]
  },
  "Currency": {
    "DefaultCurrency": "GBP",
    "SupportedCurrencies": ["USD", "EUR", "GBP"]
  }
}
```

### BookStore.Web — appsettings.json

The Web frontend has a minimal configuration. The API service URL and all infrastructure connection strings are injected by Aspire at runtime — there is nothing to configure manually here:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
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

## Aspire-Injected Configuration

The AppHost (`src/BookStore.AppHost/AppHost.cs`) wires up infrastructure resources and injects their connection details as environment variables into consuming services. You do **not** configure these in `appsettings.json`; they are set automatically at runtime by Aspire.

| Resource | Injected into | Aspire resource name |
|----------|--------------|----------------------|
| PostgreSQL database | ApiService | `bookstoredb` |
| Redis cache | ApiService | `cache` |
| Azure Blob Storage | ApiService | `blobs` |
| ApiService HTTP endpoint | Web | `apiservice` |

In development, Blob Storage runs via the **Azurite** emulator container — no Azure account is needed.

### AppHost-Forwarded Environment Flags

The AppHost also conditionally reads certain flags from its own configuration and forwards them to the API service as environment variables. These are **not** stored in `appsettings.json` and are intended for use by the test runner or CI pipeline (e.g., via `aspire.config.json` passed to `aspire run`):

| AppHost configuration key | Forwarded to ApiService as | Purpose |
|--------------------------|---------------------------|---------|
| `RateLimit:Disabled` | `RateLimit__Disabled` | Disable rate limiting during integration tests |
| `Seeding:Enabled` | `Seeding__Enabled` | Enable or disable database seeding at startup |
| `Email:DeliveryMethod` | `Email__DeliveryMethod` | Override email delivery mode in test environments |

---

## Options Pattern

### Creating an Options Class

Options classes are strongly-typed representations of configuration sections.

**Example: PaginationOptions**

```csharp
using System.ComponentModel.DataAnnotations;

namespace BookStore.ApiService.Infrastructure;

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
  .BindConfiguration(PaginationOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**Explanation**:
- **`AddOptions<T>()`** - Registers the options type
- **`BindConfiguration(sectionName)`** - Binds `configuration.GetSection(sectionName)` to the options class
- **`ValidateDataAnnotations()`** - Enables data annotation validation
- **`ValidateOnStart()`** - Validates configuration at application startup (fails fast)

> [!NOTE]
> A few components (for example rate limiting) bind configuration manually in specialized extension methods instead of using `AddOptions<T>()`.

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
    public string DefaultCulture { get; init; } = "en";

    [Required(ErrorMessage = "SupportedCultures is required")]
    [MinLength(1, ErrorMessage = "At least one supported culture must be specified")]
    [ValidCulture]
    public required string[] SupportedCultures { get; init; }

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
public string DefaultCulture { get; init; } = "en";

[ValidCulture]
public required string[] SupportedCultures { get; init; }
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
public sealed record PaginationOptions : IValidatableObject
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
    "SupportedCultures": ["pt", "pt-PT", "en", "fr", "de", "es"]
  }
}
```

```csharp
public sealed record LocalizationOptions : IValidatableObject
{
    public const string SectionName = "Localization";

    [Required]
    [MinLength(2)]
    [ValidCulture]
    public string DefaultCulture { get; init; } = "en";

    [Required]
    [MinLength(1)]
    [ValidCulture]
    public required string[] SupportedCultures { get; init; }

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

### Currency Configuration

```json
{
  "Currency": {
    "DefaultCurrency": "GBP",
    "SupportedCurrencies": ["USD", "EUR", "GBP"]
  }
}
```

```csharp
public sealed record CurrencyOptions : IValidatableObject
{
    public const string SectionName = "Currency";

    [Required]
    [Length(3, 3, ErrorMessage = "DefaultCurrency must be a 3-character ISO 4217 code")]
    public string DefaultCurrency { get; init; } = "USD";

    [Required]
    [MinLength(1)]
    public required string[] SupportedCurrencies { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!SupportedCurrencies.Contains(DefaultCurrency, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"DefaultCurrency must be included in SupportedCurrencies",
                [nameof(DefaultCurrency), nameof(SupportedCurrencies)]);
        }
    }
}
```

### Rate Limit Configuration

```json
{
  "RateLimit": {
    "PermitLimit": 1000,
    "WindowInMinutes": 1,
    "QueueLimit": 100,
    "AuthPermitLimit": 20,
    "AuthWindowSeconds": 60,
    "AuthQueueLimit": 5,
    "NotificationSseTokenLimit": 20,
    "NotificationSseTokensPerPeriod": 2,
    "NotificationSseReplenishmentPeriodSeconds": 1,
    "NotificationSseQueueLimit": 0
  }
}
```

> **`RateLimit:Disabled`**: Setting `RateLimit:Disabled=true` bypasses all rate limiting. This flag is intended **only** for automated integration tests. If it is set to `true` in any non-development, non-test environment, a **Critical** log warning is emitted at startup. Never set this flag in production. This flag is **not** a property on `RateLimitOptions`; it is read directly from configuration by the rate-limiting setup code.

```csharp
public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    // General API rate limiting
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int WindowInMinutes { get; set; } = 1;

    public int QueueLimit { get; set; } = 0;

    // Auth endpoint rate limiting
    [Range(1, int.MaxValue)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int AuthWindowSeconds { get; set; } = 60;

    public int AuthQueueLimit { get; set; } = 2;

    // SSE notification endpoint rate limiting (token bucket)
    [Range(1, int.MaxValue)]
    public int NotificationSseTokenLimit { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int NotificationSseTokensPerPeriod { get; set; } = 2;

    [Range(1, int.MaxValue)]
    public int NotificationSseReplenishmentPeriodSeconds { get; set; } = 1;

    public int NotificationSseQueueLimit { get; set; } = 0;
}
```

### Account Cleanup Configuration

Controls the background job that removes unverified accounts after they expire:

```json
{
  "Account": {
    "Cleanup": {
      "UnverifiedAccountExpirationHours": 24,
      "CleanupIntervalHours": 1,
      "Enabled": true
    }
  }
}
```

```csharp
public sealed class AccountCleanupOptions
{
    public const string SectionName = "Account:Cleanup";

    /// <summary>
    /// Hours after which an unverified account is considered expired. Default: 24.
    /// </summary>
    [Range(1, 8760)] // 1 hour to 1 year
    public int UnverifiedAccountExpirationHours { get; set; } = 24;

    /// <summary>
    /// How often the cleanup job runs. Default: every 1 hour.
    /// </summary>
    [Range(1, 720)] // 1 hour to 30 days
    public int CleanupIntervalHours { get; set; } = 1;

    /// <summary>
    /// Whether the cleanup job is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
```

---

## Logging Configuration

### Structured Logging

The BookStore API service uses structured JSON logging in production and a human-readable simple formatter in development:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting": "Information",
      "Microsoft.AspNetCore.Routing": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "Marten": "Warning",
      "Npgsql": "Warning",
      "Wolverine": "Warning",
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
- **FormatterName**: `"json"` for structured logging (production); `"simple"` in Development for human-readable output
- **TimestampFormat**: ISO 8601 format with milliseconds
- **UseUtcTimestamp**: Always use UTC (consistent with time standards)
- **IncludeScopes**: Include logging scopes for correlation

---

## Authentication Configuration

The BookStore uses **custom JWT authentication** backed by **ASP.NET Core Identity** (with Marten as the user store). The API issues its own JWT access tokens; the Web frontend stores them in cookies and attaches them to API requests via a `DelegatingHandler`.

> [!NOTE]
> There is no Keycloak or external identity provider. Authentication is handled entirely by the BookStore API using `JwtTokenService` for token issuance and JWT Bearer authentication for validation.

### JWT Configuration

Required for token signing and validation.

```json
{
  "Jwt": {
    "Algorithm": "HS256",
    "SecretKey": "your-secret-key-must-be-at-least-32-characters-long-for-hs256",
    "Issuer": "BookStore.ApiService",
    "Audience": "BookStore.Web",
    "ExpirationMinutes": 15,
    "RS256": {
      "PrivateKeyPem": "",
      "PublicKeyPem": ""
    }
  }
}
```

**Key Settings**:
- **Algorithm**: `HS256` (default) or `RS256`.
- **SecretKey**: Strong cryptographic key (min 32 UTF-8 bytes) for HMAC signing. **Must be kept secret in production.** Only used when `Algorithm` is `HS256`.
- **Issuer**: The authority issuing the token — must match `BookStore.ApiService`.
- **Audience**: The intended recipient — must match `BookStore.Web`.
- **ExpirationMinutes**: Lifetime of the access token. Default is **15 minutes**.
- **RS256.PrivateKeyPem / RS256.PublicKeyPem**: PEM-encoded key pair used only when `Algorithm` is `RS256`.

> [!WARNING]
> For production deployments, prefer `RS256` over `HS256`.
> When `Jwt:Algorithm` is `HS256` outside Development, the secret key is validated for sufficient entropy (minimum distinct characters, no repeated-character patterns).
> When `Jwt:Algorithm` is `RS256`, both `RS256:PrivateKeyPem` and `RS256:PublicKeyPem` must be non-empty or the application will fail to start.

### Passkey Configuration

Required for WebAuthn/FIDO2 passkey operations.

```json
{
  "Authentication": {
    "Passkey": {
      "ServerDomain": "localhost",
      "AllowedOrigins": ["https://localhost:7260"]
    }
  }
}
```

**Key Settings**:
- **ServerDomain**: The relying party domain for passkey registration/authentication.
    - **Development**: Use `localhost`.
    - **Production**: **MUST** match your public domain (e.g., `bookstore.com`). Do not include protocol or port.
- **AllowedOrigins**: Origins permitted to make cross-origin passkey requests (typically the Web frontend origin).
  - **Development**: `http://localhost` is permitted.
  - **Production**: **MUST** be HTTPS-only origins. HTTP origins are rejected outside Development.
  - Outside Development, an empty list causes startup validation to fail.

> [!WARNING]
> **Production Criticality**
> 1. Setting `ServerDomain` incorrectly causes passkey registration and login to fail with "Domain mismatch" or "NotAllowed" errors.
> 2. `AllowedOrigins` must only contain HTTPS origins in production.

### Email Configuration

Required for email-based account verification.

```json
{
  "Email": {
    "DeliveryMethod": "Smtp",
    "BaseUrl": "https://bookstore.com",
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
  - `None`: Disables email sending — users are auto-verified. **Allowed only in Development.**
  - `Logging`: Logs email content to the console (Development / staging diagnostics).
  - `Smtp`: Sends actual emails via SMTP (Production).
- **BaseUrl**: Base URL of the frontend application, used to build verification links in emails.
- **FromEmail / FromName**: Sender details.
- **Smtp***: SMTP server credentials (required when `DeliveryMethod` is `Smtp`).

> [!WARNING]
> Startup validation fails outside Development when `Email:DeliveryMethod` is `None`.
> For Test, Staging, and Production environments, use `Logging` (non-delivery diagnostics) or `Smtp` (real delivery).

---

## Seeding Configuration

Database seeding is controlled by configuration values that are **not** in `appsettings.json`. They are injected by AppHost or set via user secrets / environment variables:

| Key | Type | Default | Purpose |
|-----|------|---------|---------|
| `Seeding:Enabled` | `bool` | `true` | Enable or disable the background seeding job at startup |
| `Seeding:AdminPassword` | `string` | _(none)_ | Password for the seeded admin user. Required outside Development/Test. |

> [!NOTE]
> In Development, if `Seeding:AdminPassword` is not set, the seeder uses a hardcoded fallback password. Outside Development/Test, an explicit password must be provided or the seeder will throw.

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

# Set the JWT signing key
dotnet user-secrets set "Jwt:SecretKey" "your-secret-key-must-be-at-least-32-characters-long-for-hs256"

# Set seeding admin password
dotnet user-secrets set "Seeding:AdminPassword" "Admin@123!"

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
    "DefaultPageSize": 20,
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
