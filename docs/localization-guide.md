# Localization Guide

This guide explains how to configure and use localization in the BookStore API.

## Overview

The BookStore API supports multiple languages for localized content (category names, book descriptions, author biographies). The API automatically detects the client's preferred language from the `Accept-Language` HTTP header and returns localized content accordingly.

## Configuration

### Two-Letter Language Codes

The API uses **two-letter ISO 639-1 language codes** for universal variant support:

```json
{
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["pt", "en", "fr", "de", "es"]
  }
}
```

**Why two-letter codes?**
- ✅ Any regional variant automatically maps to the base language (pt-BR, pt-PT, pt-AO → pt)
- ✅ Simpler configuration - no need to list every regional variant
- ✅ Easier maintenance - new regional variants work immediately

### Cache Configuration

**Critical**: All localized endpoints must vary cache by `Accept-Language`:

```csharp
.CacheOutput(policy => policy
    .Expire(TimeSpan.FromMinutes(5))
    .SetVaryByHeader("Accept-Language"))
```

Without this, cached responses ignore the language header.

## Translation Storage

Translations can be **culture-specific** or **culture-invariant**:

**Culture-Invariant** (recommended):
```json
{
  "pt": "Programação",
  "en": "Programming"
}
```

**Hybrid** (specific + invariant):
```json
{
  "pt-BR": "Programação (Brasil)",
  "pt": "Programação",
  "en": "Programming"
}
```

## Fallback Strategy

The API uses a 6-step fallback to find the best translation:

1. Exact preferred culture (e.g., `"pt-PT"`)
2. Two-letter preferred code (e.g., `"pt"`)
3. Exact default culture (e.g., `"en"`)
4. Two-letter default code (e.g., `"en"`)
5. First available translation
6. Default value (empty string)

**Example**: Request with `Accept-Language: pt-BR`
- Tries `"pt-BR"` → `"pt"` → `"en"` → first available → default
- With hybrid translations above, returns `"Programação (Brasil)"`

## Usage

### Making Requests

Include the `Accept-Language` header in your HTTP requests:

```http
GET /api/books HTTP/1.1
Accept-Language: pt-BR
```

The API returns localized content based on the header value.

### Testing Different Languages

**Using curl**:
```bash
curl -H "Accept-Language: pt" https://localhost:7001/api/categories
curl -H "Accept-Language: en" https://localhost:7001/api/categories
```

**Using Postman/Insomnia**:
1. Add header: `Accept-Language: pt`
2. Send request
3. Observe localized response

## Localized Endpoints

All public-facing endpoints return localized content:

- **Categories** (`/api/categories`): Category names
- **Books** (`/api/books`): Descriptions, category names, author biographies, language display names
- **Authors** (`/api/authors`): Author biographies

## Implementation Details

The localization system uses:
- **ASP.NET Core Middleware**: `RequestLocalizationMiddleware` determines culture from `Accept-Language` header
- **LocalizationHelper**: Centralized helper with reusable methods:
  - `GetLocalizedValue<T>()`: Generic method for translating any `Dictionary<string, T>`
  - `LocalizeLanguageName()`: Gets localized display names for language codes
  - `GetPreferredCulture()`: Retrieves user's preferred culture from middleware
- **Generic Translation Support**: Works with any translation type via selector functions
- **Null Safety**: Uses `[NotNullWhen(true)]` attribute for compile-time null safety

### Usage Example

```csharp
// Localize category name
var localizedName = LocalizationHelper.GetLocalizedValue(
    context,
    options,
    category.Translations,
    translation => translation.Name,
    defaultValue: "Unknown");

// Get localized language name
var languageName = LocalizationHelper.LocalizeLanguageName(
    "en",
    context,
    options); // Returns "English" or "Inglês" depending on user's language
```

## Best Practices

1. **Use two-letter codes**: Configure `SupportedCultures` with two-letter codes for universal variant support
2. **Use culture-invariant translations**: Store translations with two-letter keys unless region-specific content is needed
3. **Always vary cache by Accept-Language**: Ensure cached responses are language-specific
4. **Provide fallbacks**: Always include default culture translations
5. **Validate culture codes**: Use `CultureCache.IsValidCultureCode()` to validate codes before use

## Troubleshooting

### Translations not working
- ✅ Check `Accept-Language` header is being sent
- ✅ Verify culture code is in `SupportedCultures`
- ✅ Ensure cache varies by `Accept-Language`
- ✅ Check translation dictionary has the expected keys

### Always returning same language
- ✅ Verify cache configuration includes `.SetVaryByHeader("Accept-Language")`
- ✅ Clear cache and retry

### Regional variant not working
- ✅ Ensure `SupportedCultures` uses two-letter codes
- ✅ Check translation dictionary has two-letter key (e.g., `"pt"` not `"pt-PT"`)
