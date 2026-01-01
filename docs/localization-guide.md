# Localization Guide

This guide explains how to configure and use localization in the BookStore API.

## Overview

The BookStore API supports multiple languages for localized content (category names, book descriptions, author biographies).

**Architecture Strategy:**
The localization strategy uses **Write-Time Localization** via **Marten's Conjoined Tenancy**.
- **Events** contain all translations.
- **Projections** are multi-tenanted, with one document stored per supported language (tenant).
- **APIs** simply query the tenant corresponding to the user's preferred language.

This approach ensures high performance by eliminating complex runtime fallback logic during data retrieval.

## Configuration

### Supported Languages

The API is configured using **ISO 639-1 language codes**. You can configure either generic codes (e.g., `en`, `pt`) or specific regional cultures (e.g., `en-US`, `pt-PT`).

**Standard Configuration (Generic)**:
Suitable for applications where a single translation per language works for all regions.
```json
{
  "Localization": {
    "DefaultCulture": "en",
    "SupportedCultures": ["en", "pt", "fr", "de", "es"]
  }
}
```

**Regional Configuration (Specific)**:
Suitable when you need different content for specific regions (e.g., "Color" vs "Colour" in English, or regional differences between pt-PT, pt-BR, etc.).
```json
{
  "Localization": {
    "DefaultCulture": "en-US",
    "SupportedCultures": ["en-US", "en-GB", "pt-PT", "pt-BR"]
  }
}
```

### Marten Configuration

Projections are configured as multi-tenanted to support separate documents for each culture. This is defined in `MartenConfigurationExtensions.cs`.

```csharp
options.Schema.For<BookSearchProjection>().MultiTenanted();
options.Schema.For<AuthorProjection>().MultiTenanted();
options.Schema.For<CategoryProjection>().MultiTenanted();
```

The system iterates through all configured `SupportedCultures` to generate a projection document for each one.

### Cache Configuration

**Critical**: All localized endpoints must verify the cache by the `Accept-Language` header to ensure users receive the correct language version.

```csharp
.CacheOutput(policy => policy
    .Expire(TimeSpan.FromMinutes(5))
    .SetVaryByHeader("Accept-Language"))
```

## Translation Storage

Translations are captured at the source in Domain Events using a dictionary.

**Mixed Storage Example**:
You can store both generic and specific keys.
```json
{
  "pt": "Desporto",
  "pt-BR": "Esporte",
  "en": "Sports"
}
```

## Fallback Strategy (Write-Time)

The API applies fallback logic **during projection generation** to ensure every supported culture has content.

**Logic sequence for a target culture (e.g., `pt-PT`):**

1. **Exact Match**: Look for a translation with key `"pt-PT"`.
2. **Parent Culture**: Look for a translation with key `"pt"`.
3. **Default Culture**: Look for a translation with the key of the `DefaultCulture`.
4. **Any**: Use the first available translation.
5. **Empty**: Fallback to an empty string.

This robust fallback ensures that even if a specific translation is missing, the system provides the most relevant available content.

## Usage

### Making Requests

Clients request a specific language using the `Accept-Language` header.

```http
GET /api/books HTTP/1.1
Accept-Language: pt-PT
```

If the requested culture is not supported (e.g., `ja-JP`), the API will automatically fall back to the configured `DefaultCulture`.

### Endpoint Implementation

Endpoints are simplified to purely read operations. They resolve the current culture (handled by ASP.NET Core middleware) and query the corresponding database tenant.

```csharp
// 1. Resolve culture (e.g., "pt-PT")
var culture = CultureInfo.CurrentCulture.Name;

// 2. Open a session specific to that culture
await using var session = store.QuerySession(culture);

// 3. Query normally - Marten automatically filters by the tenant
var books = await session.Query<BookSearchProjection>().ToListAsync();
```

### Projection Implementation

Projections are responsible for "fanning out" changes to all supported cultures. When an event (like `BookAdded`) occurs, the projection updates the documents for **all** configured tenants.

```csharp
foreach (var culture in _localization.SupportedCultures)
{
    using var tenantSession = session.ForTenant(culture);
    
    var projection = new BookSearchProjection
    {
        // ... map fields ...
        Description = GetLocalizedDescription(@event.Data.Translations, culture)
    };
    
    tenantSession.Store(projection);
}
```
