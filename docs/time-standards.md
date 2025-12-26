# Time and JSON Serialization Standards

## Overview

The BookStore API follows strict standards for time handling and JSON serialization to ensure consistency, interoperability, and maintainability.

## Time Standards

### Always Use UTC

**Rule**: All timestamps MUST use UTC timezone.

✅ **Correct**:
```csharp
var timestamp = DateTimeOffset.UtcNow;
var eventTime = DateTimeOffset.UtcNow;
```

❌ **Incorrect**:
```csharp
var timestamp = DateTime.Now;           // Local timezone - NEVER use
var eventTime = DateTimeOffset.Now;     // Local timezone - NEVER use
```

### Always Use DateTimeOffset

**Rule**: Use `DateTimeOffset` instead of `DateTime` for all timestamps.

✅ **Correct**:
```csharp
public DateTimeOffset Timestamp { get; set; }
public DateTimeOffset LastModified { get; set; }
```

❌ **Incorrect**:
```csharp
public DateTime Timestamp { get; set; }  // No timezone info - NEVER use
```

### ISO 8601 Format

All date/time values are automatically serialized in **ISO 8601** format:

```json
{
  "timestamp": "2025-12-26T17:16:09.123Z",
  "lastModified": "2025-12-26T17:16:09Z",
  "publicationDate": "2008-08-01"
}
```

**Format Details**:
- `DateTimeOffset`: `YYYY-MM-DDTHH:mm:ss.fffZ` (with milliseconds)
- `DateOnly`: `YYYY-MM-DD`
- Timezone: Always `Z` (UTC)

## JSON Serialization Standards

### Property Naming: camelCase

**Rule**: All JSON properties use camelCase.

```json
{
  "bookId": "018d5e4a-7b2c-7000-8000-123456789abc",
  "title": "Clean Code",
  "publicationDate": "2008-08-01",
  "lastModified": "2025-12-26T17:16:09Z"
}
```

### Enums: String Serialization

**Rule**: Enums are serialized as strings, not integers.

✅ **Correct** (String):
```json
{
  "status": "Active",
  "role": "Administrator",
  "orderStatus": "Shipped"
}
```

❌ **Incorrect** (Integer):
```json
{
  "status": 0,
  "role": 1,
  "orderStatus": 2
}
```

**Benefits**:
- **Readable**: `"Active"` is clearer than `0`
- **Evolvable**: Can reorder enum values without breaking API
- **Self-documenting**: No need to look up enum definitions
- **Debuggable**: Easier to understand logs and database queries

### Configuration

#### ASP.NET Core (API Responses)

Configured in `Program.cs`:
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
```

#### Marten (Database Storage)

Configured in `Program.cs`:
```csharp
options.UseDefaultSerialization(
    enumStorage: EnumStorage.AsString,
    casing: Casing.CamelCase);
```

## Common Patterns

### Creating Events

Always use `DateTimeOffset.UtcNow` for event timestamps:

```csharp
public static BookAdded Create(Guid id, string title, ...)
{
    return new BookAdded(
        id,
        title,
        ...,
        DateTimeOffset.UtcNow  // ✅ Always UTC
    );
}
```

### Storing Timestamps

Use `DateTimeOffset` for all timestamp properties:

```csharp
public record BookAdded(
    Guid Id,
    string Title,
    DateTimeOffset Timestamp  // ✅ DateTimeOffset with UTC
);
```

### Querying by Time

Use `DateTimeOffset.UtcNow` for time-based queries:

```csharp
var recentBooks = await session.Query<BookSearchProjection>()
    .Where(b => b.LastModified > DateTimeOffset.UtcNow.AddDays(-7))
    .ToListAsync();
```

## Benefits

### UTC Timezone
- ✅ No timezone conversion errors
- ✅ Consistent across all servers
- ✅ Works globally without confusion
- ✅ Simplifies distributed systems

### ISO 8601 Format
- ✅ Universal standard (RFC 3339)
- ✅ Sortable as strings
- ✅ Human-readable
- ✅ Supported by all platforms

### Enum Strings
- ✅ Self-documenting APIs
- ✅ Safe enum reordering
- ✅ Easier debugging
- ✅ Better database queries

### camelCase
- ✅ JavaScript/TypeScript convention
- ✅ Consistent with web standards
- ✅ Better readability in JSON

## Common Pitfalls

### ❌ Using DateTime.Now
```csharp
var timestamp = DateTime.Now;  // WRONG - uses local timezone
```

### ❌ Using DateTime instead of DateTimeOffset
```csharp
public DateTime Timestamp { get; set; }  // WRONG - no timezone info
```

### ❌ Manual date formatting
```csharp
var dateStr = date.ToString("yyyy-MM-dd");  // WRONG - use serialization
```

### ❌ Integer enum serialization
```csharp
// WRONG - will serialize as integer without configuration
public enum Status { Active, Inactive }
```

## Validation

### Unit Tests

Test JSON serialization format:

```csharp
[Fact]
public void DateTimeOffset_Should_Serialize_As_ISO8601()
{
    var obj = new { timestamp = DateTimeOffset.UtcNow };
    var json = JsonSerializer.Serialize(obj);
    
    // Should match ISO 8601 format: "2025-12-26T17:16:09.123Z"
    Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z", json);
}

[Fact]
public void Enum_Should_Serialize_As_String()
{
    var obj = new { status = Status.Active };
    var json = JsonSerializer.Serialize(obj);
    
    Assert.Contains("\"Active\"", json);
    Assert.DoesNotContain("0", json);
}
```

## Summary

**Golden Rules**:
1. ✅ Always use `DateTimeOffset.UtcNow` (never `DateTime.Now`)
2. ✅ Always use `DateTimeOffset` type (never `DateTime`)
3. ✅ ISO 8601 format is automatic (don't format manually)
4. ✅ Enums serialize as strings (configured globally)
5. ✅ JSON properties use camelCase (configured globally)

These standards ensure the API is:
- **Consistent**: Same format everywhere
- **Interoperable**: Works with all clients
- **Maintainable**: Easy to understand and debug
- **Scalable**: Works across timezones and regions
