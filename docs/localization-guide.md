# Localization Guide

This guide explains how to configure and use localization in the BookStore API.

## Overview

The BookStore API supports multiple languages for localized content (category names, book descriptions, author biographies).

**Architecture Strategy:**
The localization strategy uses **dictionary-based storage** with **SingleStreamProjection**.
- **Events** contain all translations as `Dictionary<string, XTranslation>`.
- **Projections** store translations in `Dictionary<string, string>` properties (one projection per entity).
- **Endpoints** use `LocalizationHelper` to extract the correct translation based on the `Accept-Language` header.

This approach ensures:
- ✅ **Simple architecture** - no multi-tenancy complexity
- ✅ **High performance** - single document read, no JOINs
- ✅ **Flexible fallback** - comprehensive 5-step fallback strategy
- ✅ **Easy to maintain** - all translations in one place

## Configuration

### Supported Languages

The API is configured using **ISO 639-1 language codes**. You can configure either generic codes (e.g., `en`, `pt`) or specific regional cultures (e.g., `en-US`, `pt-PT`).

**Standard Configuration (Generic)**:
```json
{
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["en", "pt", "fr", "de", "es"]
  }
}
```

**Regional Configuration (Specific)**:
```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US", "en-GB", "pt-PT", "pt-BR"]
  }
}
```

### Cache Configuration

**Critical**: All localized endpoints must vary the cache by the `Accept-Language` header to ensure users receive the correct language version.

```csharp
.CacheOutput(policy => policy
    .Expire(TimeSpan.FromMinutes(5))
    .SetVaryByHeader("Accept-Language"))
```

## Translation Storage

### Event Structure

Translations are stored in events using translation record types:

```csharp
// Category event with translations
public record CategoryAdded(
    Guid Id,
    Dictionary<string, CategoryTranslation> Translations,
    DateTimeOffset Timestamp);

public record CategoryTranslation(string Name, string? Description);
```

**Example event data**:
```json
{
  "id": "...",
  "translations": {
    "en": { "name": "Sports", "description": null },
    "pt": { "name": "Desporto", "description": null },
    "pt-BR": { "name": "Esporte", "description": null }
  }
}
```

### Projection Structure

Projections extract specific fields into dictionaries:

```csharp
public class CategoryProjection
{
    public Guid Id { get; set; }
    public Dictionary<string, string> Names { get; set; } = [];
    
    public static CategoryProjection Create(CategoryAdded @event)
    {
        return new CategoryProjection
        {
            Id = @event.Id,
            Names = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name) 
                ?? []
        };
    }
}
```

## Fallback Strategy

The `LocalizationHelper.GetLocalizedValue()` method implements a **5-step fallback** at read time:

1. **Exact culture match** - e.g., "pt-PT"
2. **Two-letter user culture** - e.g., "pt" from "pt-PT"
3. **Default culture** - configured in `LocalizationOptions`
4. **Two-letter default culture** - e.g., "en" from "en-US"
5. **Fallback value** - empty string or "Unknown"

**Example**:
```csharp
var localizedName = LocalizationHelper.GetLocalizedValue(
    category.Names,           // Dictionary<string, string>
    "pt-PT",                  // Requested culture
    "en-US",                  // Default culture
    "Unknown"                 // Fallback
);
```

**Fallback flow for `pt-PT` request**:
```
Request: pt-PT
  ↓
1. Check dictionary["pt-PT"] → Found? Return it
  ↓ (not found)
2. Check dictionary["pt"] → Found? Return it
  ↓ (not found)
3. Check dictionary["en-US"] → Found? Return it
  ↓ (not found)
4. Check dictionary["en"] → Found? Return it
  ↓ (not found)
5. Return "Unknown"
```

## Usage

### Making Requests

Clients request a specific language using the `Accept-Language` header.

```http
GET /api/categories HTTP/1.1
Accept-Language: pt-PT
```

If the requested culture is not in the dictionary, the fallback strategy automatically finds the best available translation.

### Endpoint Implementation

Endpoints use `LocalizationHelper` to extract localized values:

```csharp
static async Task<Ok<PagedListDto<CategoryDto>>> GetCategories(
    [FromServices] IDocumentStore store,
    [FromServices] IOptions<LocalizationOptions> localizationOptions,
    HttpContext context)
{
    var culture = CultureInfo.CurrentCulture.Name;
    var defaultCulture = localizationOptions.Value.DefaultCulture;
    await using var session = store.QuerySession();
    
    var categories = await session.Query<CategoryProjection>().ToListAsync();
    
    // Extract localized names using LocalizationHelper
    var items = categories.Select(c => new CategoryDto(
        c.Id,
        LocalizationHelper.GetLocalizedValue(c.Names, culture, defaultCulture, "Unknown")
    )).ToList();
    
    return TypedResults.Ok(items);
}
```

### Projection Implementation

Projections use `SingleStreamProjection` with `Create()` and `Apply()` methods:

```csharp
public class CategoryProjection
{
    public Dictionary<string, string> Names { get; set; } = [];
    
    public static CategoryProjection Create(CategoryAdded @event)
    {
        return new CategoryProjection
        {
            Id = @event.Id,
            Names = @event.Translations?
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name) 
                ?? []
        };
    }
    
    public void Apply(CategoryUpdated @event)
    {
        Names = @event.Translations?
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name) 
            ?? [];
    }
}
```

## Testing

### Test Different Languages

```bash
# English
curl -H "Accept-Language: en" http://localhost:5179/categories

# Portuguese (Portugal)
curl -H "Accept-Language: pt-PT" http://localhost:5179/categories

# Portuguese (generic - should fallback to pt-PT if available)
curl -H "Accept-Language: pt" http://localhost:5179/categories

# Unsupported language (should fallback to default)
curl -H "Accept-Language: ja" http://localhost:5179/categories
```

### Verify Fallback Logic

Test the fallback chain by checking responses for:
- Exact culture match (e.g., "en-US")
- Two-letter culture match (e.g., "en" when "en-US" not available)
- Default culture fallback
- Fallback value for completely missing translations

## Best Practices

1. **Always provide default culture translations** - ensures fallback always succeeds
2. **Use generic codes when possible** - reduces duplication (e.g., "en" instead of "en-US", "en-GB")
3. **Use specific codes for regional differences** - when content truly differs by region
4. **Cache by Accept-Language** - critical for performance
5. **Test fallback scenarios** - ensure graceful degradation

## Architecture Benefits

✅ **Simple** - No multi-tenancy, no separate translation tables  
✅ **Fast** - Single document read, dictionary lookup is O(1)  
✅ **Flexible** - Easy to add/remove languages  
✅ **Type-safe** - LINQ queries work normally  
✅ **Comprehensive fallback** - 5-step strategy ensures users always see content  
✅ **Idiomatic Marten** - Uses SingleStreamProjection pattern
