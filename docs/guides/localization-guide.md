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
- ✅ **Architectural Symmetry** - mirrors the pattern used for [Multi-Currency Pricing](multi-currency-guide.md)

## Configuration

### Supported Languages

The API is configured using **ISO 639-1 language codes**. You can configure either generic codes (e.g., `en`, `pt`) or specific regional cultures (e.g., `pt-PT`).

**Standard Configuration** (`appsettings.json`, section `"Localization"`):
```json
{
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["en", "pt", "pt-PT", "es", "fr", "de"]
  }
}
```

The `LocalizationOptions` class (`src/BookStore.ApiService/Infrastructure/LocalizationOptions.cs`) binds this section. The default/fallback list used when no configuration is present is `["en", "pt", "pt-PT", "es", "fr", "de"]` with `"en"` as the default culture.

The supported cultures are exposed at runtime via:

```
GET /api/config/localization
```

This returns a `LocalizationConfigDto` containing `DefaultCulture` and `SupportedCultures[]`. The Web frontend calls this endpoint at startup to configure its own `RequestLocalizationOptions` (with `"en"` as the fallback if the backend is unavailable).

### Cache Configuration

**Critical**: Localized content is cached using `HybridCache` with the `GetOrCreateLocalizedAsync` extension method (`src/BookStore.ApiService/Infrastructure/Extensions/HybridCacheExtensions.cs`), which automatically appends `|{CultureInfo.CurrentUICulture.Name}` to the cache key.

In practice, endpoints also include the culture explicitly in the base key alongside tenant and query parameters:

```csharp
var culture = CultureInfo.CurrentUICulture.Name;
var cacheKey = $"categories:tenant={tenantContext.TenantId}:culture={culture}:search={request.Search}:page={paging.Page}:size={paging.PageSize}:sort={normalizedSortBy}:{normalizedSortOrder}";

var response = await cache.GetOrCreateLocalizedAsync(
    cacheKey,           // GetOrCreateLocalizedAsync appends "|{culture}" to this
    async cancel => { /* load from database */ },
    options: new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    },
    tags: [CacheTags.CategoryList],
    token: cancellationToken);
```

The final stored key is `categories:tenant=...:culture=en:...|en`. The culture appears twice (once in the explicit key for transparency, once appended by `GetOrCreateLocalizedAsync`), ensuring correct variation by tenant, culture, and query parameters.

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
    var culture = CultureInfo.CurrentUICulture.Name;
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

## Web Frontend Localization

### How Culture Flows from Web to API

The `BookStoreHeaderHandler` (`src/BookStore.Client/Infrastructure/BookStoreHeaderHandler.cs`) is a `DelegatingHandler` applied to all Refit clients. It automatically adds the `Accept-Language` header when absent:

```csharp
if (!request.Headers.Contains("Accept-Language"))
{
    var culture = CultureInfo.CurrentUICulture.Name;
    if (!string.IsNullOrEmpty(culture))
    {
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
    }
}
```

This means the Blazor circuit's current UI culture is propagated transparently to every API call.

### `RequestLocalizationOptions` in the Web

At startup, `BookStore.Web/Program.cs` calls `GET /api/config/localization` to fetch the supported cultures from the API and configures its own `RequestLocalizationOptions` accordingly:

```csharp
var localizationConfig = await configClient.GetLocalizationConfigAsync();
supportedCultures = [.. localizationConfig.SupportedCultures];
defaultCulture = localizationConfig.DefaultCulture;

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(defaultCulture)
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
```

If the backend is unreachable at startup, the Web falls back to `["en"]` with default culture `"en"`.

### `LanguageService`

`src/BookStore.Web/Services/LanguageService.cs` wraps the configuration endpoint with an in-memory cache:

- `GetSupportedLanguagesAsync()` — returns the supported culture codes (cached after first call, with fallback to `["en", "pt", "pt-PT", "es", "fr", "de"]` on error).
- `GetLanguagesWithDisplayNamesAsync()` — returns a `Dictionary<string, string>` mapping code → display name in the current UI language.
- `GetDefaultCultureAsync()` — returns the configured default culture (fallback: `"en"`).
- `GetDisplayName(string cultureCode)` — static helper converting a culture code to its .NET display name.
- `GetAllLanguages()` — enumerates all .NET cultures (used for selecting a book's primary written language, not the UI language).

### Language Selector Components

Two Blazor components provide language selection in the UI:

| Component | File | Purpose |
|---|---|---|
| `<LanguageSelector>` | `Components/Shared/LanguageSelector.razor` | Selects a UI/content language from the **supported** cultures returned by the API. Shows "(Default)" next to the configured default. |
| `<AllLanguageSelector>` | `Components/Shared/AllLanguageSelector.razor` | Selects from **all** .NET cultures. Used in admin dialogs to set a book's primary written language. |

### Error Message Localization (`IStringLocalizer`)

The Web project uses standard ASP.NET Core resource-based localization for UI error messages:

- `AddLocalization(options => options.ResourcesPath = "Resources")` is registered in `Program.cs`.
- `src/BookStore.Web/Services/ErrorLocalizationService.cs` injects `IStringLocalizer<ErrorLocalizationService>` and looks up error codes from resource files.
- Default (English) strings live in `src/BookStore.Web/Resources/Services/ErrorLocalizationService.resx`.
- To add a translated version, add `ErrorLocalizationService.{culture}.resx` (e.g., `ErrorLocalizationService.pt.resx`) in the same folder.

This is distinct from the dictionary-based content localization used by the API — `.resx` files only cover UI error messages in the Web project.

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
✅ **Symmetric with Pricing** - Mirrors the [Multi-Currency Pricing](multi-currency-guide.md) implementation
