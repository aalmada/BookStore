# Localization Guide

This guide explains how to configure and use localization in the BookStore API.

## Overview

The BookStore API supports multiple languages for localized content (e.g., category names). The API automatically detects the client's preferred language from the `Accept-Language` HTTP header and returns localized content accordingly.

## Configuration

Localization is configured in `appsettings.json` under the `Localization` section:

```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US"]
  }
}
```

### Configuration Options

#### `DefaultCulture`
- **Type**: `string`
- **Default**: `"en-US"`
- **Description**: The default culture to use when the client's preferred language is not supported
- **Valid Values**: Any valid culture identifier (e.g., `"en-US"`, `"pt-PT"`, `"es-ES"`, `"fr-FR"`, `"de-DE"`)

#### `SupportedCultures`
- **Type**: `string[]`
- **Default**: `["en-US"]`
- **Description**: Array of culture identifiers that the API can respond in
- **Valid Values**: Array of valid culture identifiers

### Example Configurations

**English only (default)**:
```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US"]
  }
}
```

**Multiple languages**:
```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US", "pt-PT", "es-ES", "fr-FR", "de-DE"]
  }
}
```

**Portuguese as default**:
```json
{
  "Localization": {
    "DefaultCulture": "pt-PT",
    "SupportedCultures": ["pt-PT", "en-US", "es-ES"]
  }
}
```

## How It Works

### 1. Client Request
The client sends an HTTP request with the `Accept-Language` header:

```http
GET /api/books HTTP/1.1
Accept-Language: pt-PT,pt;q=0.9,en-US;q=0.8,en;q=0.7
```

### 2. Language Selection
ASP.NET Core's `RequestLocalizationMiddleware` automatically:
1. Parses the `Accept-Language` header
2. Matches against `SupportedCultures`
3. Sets `CultureInfo.CurrentCulture` to the best match
4. Falls back to `DefaultCulture` if no match is found

### 3. Localized Response
The API returns localized content based on the selected culture:

```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "title": "Clean Code",
  "categories": [
    {
      "id": "456e7890-e89b-12d3-a456-426614174001",
      "name": "Programação"  // Localized to Portuguese
    }
  ]
}
```

## Localized Content

### Categories
Category names are stored with translations in multiple languages. The API automatically returns the category name in the client's preferred language.

**Category Data Structure**:
```csharp
public class CategoryProjection
{
    public Guid Id { get; set; }
    public Dictionary<string, CategoryTranslation> Translations { get; set; }
    public DateTimeOffset LastModified { get; set; }
}

public record CategoryTranslation(string Name, string? Description);
```

**Translation Dictionary Example**:
```json
{
  "en": { "name": "Programming", "description": null },
  "pt": { "name": "Programação", "description": null },
  "es": { "name": "Programación", "description": null },
  "fr": { "name": "Programmation", "description": null },
  "de": { "name": "Programmierung", "description": null }
}
```

### Fallback Strategy

The API uses a **four-tier fallback strategy** to find the best translation:

1. **Full culture code**: First tries the complete culture identifier (e.g., `"pt-PT"`)
2. **Two-letter ISO language code**: If not found, uses `CultureInfo.TwoLetterISOLanguageName` to extract the language code (e.g., `"pt"` from `"pt-PT"`)
3. **English fallback**: If still not found, tries to use the English (`"en"`) translation
4. **First available**: As a last resort, uses the first available translation in the dictionary

**Example**:
- Client requests `Accept-Language: pt-PT`
- Category has translations for `"en"` and `"pt"` (but not `"pt-PT"`)
- API tries `"pt-PT"` → not found
- API creates `CultureInfo("pt-PT")` and gets `TwoLetterISOLanguageName` → `"pt"` → **found!** Returns Portuguese translation
- If `"pt"` wasn't found → tries `"en"` → returns English translation
- If `"en"` wasn't found → returns first available translation

This ensures maximum compatibility even when exact culture matches aren't available, and handles edge cases correctly using .NET's built-in culture handling.

### Fallback Behavior
If a translation is not available for the requested language:
1. The API falls back through the strategy above (full code → two-letter → English → first available)
2. No error is thrown
3. The response is still valid

## Environment-Specific Configuration

You can configure different languages for different environments:

**`appsettings.Development.json`** (local development):
```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US", "pt-PT"]
  }
}
```

**`appsettings.Production.json`** (production):
```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US", "pt-PT", "es-ES", "fr-FR", "de-DE"]
  }
}
```

## Testing Localization

### Using curl
```bash
# Request in Portuguese
curl -H "Accept-Language: pt-PT" https://localhost:5001/api/books

# Request in Spanish
curl -H "Accept-Language: es-ES" https://localhost:5001/api/books

# Request with quality values
curl -H "Accept-Language: fr-FR,fr;q=0.9,en-US;q=0.8" https://localhost:5001/api/books
```

### Using Browser DevTools
1. Open browser DevTools (F12)
2. Go to Network tab
3. Find the API request
4. Check the `Accept-Language` header in Request Headers
5. Modify the header using browser extensions or DevTools

### Using Postman
1. Create a new request
2. Go to Headers tab
3. Add `Accept-Language` header with desired value (e.g., `pt-PT`)
4. Send the request

## Implementation Details

The localization system uses:
- **ASP.NET Core's `RequestLocalizationMiddleware`**: Handles `Accept-Language` parsing and culture selection
- **`CultureInfo.CurrentCulture`**: Provides the current culture throughout the request pipeline
- **`LocalizationOptions`**: Strongly-typed configuration class
- **Query-time localization**: Categories are localized when mapping to DTOs, not in the database

## Best Practices

1. **Always include a default culture**: Ensure `DefaultCulture` is set to a language you fully support
2. **Use standard culture codes**: Use ISO culture identifiers (e.g., `en-US`, not `english`)
3. **Test fallback behavior**: Verify the API works when translations are missing
4. **Document supported languages**: Keep this guide updated when adding new languages
5. **Consider regional variants**: Use specific cultures (`en-US`, `en-GB`) rather than generic ones (`en`)

## Adding a New Language

To add support for a new language:

1. **Update configuration** in `appsettings.json`:
   ```json
   {
     "Localization": {
       "SupportedCultures": ["en-US", "pt-PT", "ja-JP"]  // Added Japanese
     }
   }
   ```

2. **Add translations** to category data (via admin API or database):
   ```csharp
   var translations = new Dictionary<string, CategoryTranslation>
   {
       ["en"] = new("Programming", null),
       ["pt"] = new("Programação", null),
       ["ja"] = new("プログラミング", null)  // Japanese translation
   };
   ```

3. **Test** with the new language:
   ```bash
   curl -H "Accept-Language: ja-JP" https://localhost:5001/api/books
   ```

## Related Documentation

- [Architecture Guide](architecture.md) - Overall system architecture
- [Marten Guide](marten-guide.md) - Document database and projections
- [Getting Started](getting-started.md) - Initial setup and configuration
