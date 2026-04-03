# Marten Document Storage

Marten serializes .NET objects to JSON and stores them in PostgreSQL. Each document type gets its own `mt_doc_{type}` table.

## Basic CRUD

```csharp
// Store (insert or update)
session.Store(doc);

// Load by ID (returns null if not found)
var book = await session.LoadAsync<Book>(id);

// Load multiple by IDs
var books = await session.LoadManyAsync<Book>(id1, id2, id3);

// Delete by ID
session.Delete<Book>(id);
session.DeleteWhere<Book>(b => b.IsDeleted);

// Hard delete (removes from DB entirely)
session.HardDelete<Book>(id);
```

## LINQ Queries

```csharp
// Basic query
var books = await session.Query<BookSearchProjection>()
    .Where(b => !b.Deleted)
    .OrderBy(b => b.Title)
    .ToListAsync();

// Single item
var book = await session.Query<BookSearchProjection>()
    .FirstOrDefaultAsync(b => b.Id == id);

// Count
var count = await session.Query<BookSearchProjection>()
    .Where(b => !b.Deleted)
    .CountAsync();

// Any
var exists = await session.Query<BookSearchProjection>()
    .AnyAsync(b => b.Isbn == isbn);

// Pagination
var page = await session.Query<BookSearchProjection>()
    .Where(b => !b.Deleted)
    .OrderBy(b => b.Title)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// Projection to DTO (reduces data transferred)
var titles = await session.Query<BookSearchProjection>()
    .Where(b => !b.Deleted)
    .Select(b => new { b.Id, b.Title })
    .ToListAsync();
```

## Full-Text Search

Marten supports PostgreSQL full-text search with NGram (handles partial matches):

```csharp
// PlainTextSearch — word-based, no partial matching
var results = await session.Query<BookSearchProjection>()
    .Where(b => b.SearchText.PlainTextSearch("clean code"))
    .ToListAsync();

// NgramSearch — handles partial word matching (needs GIN index)
var results = await session.Query<BookSearchProjection>()
    .Where(b => b.SearchText.NgramSearch("clea"))
    .ToListAsync();

// WebStyleSearch — handles natural language queries
var results = await session.Query<BookSearchProjection>()
    .Where(b => b.SearchText.WebStyleSearch("clean OR agile"))
    .ToListAsync();
```

> Full-text search requires a GIN index on the field. Configure in `AddMarten()`:
>
> ```csharp
> options.Schema.For<BookSearchProjection>()
>     .Index(x => x.SearchText, idx => idx.Method = IndexMethod.GIN);
> ```
>
> For multilingual support (accented characters), use `UseNGramSearchWithUnaccent()`.

## CollectionContains and JSON Queries

```csharp
// Query inside collections stored as JSON
var books = await session.Query<BookSearchProjection>()
    .Where(b => b.AuthorIds.Contains(authorId))
    .ToListAsync();

// Query dictionary values
var booksInCategory = await session.Query<BookSearchProjection>()
    .Where(b => b.CategoryIds.Contains(categoryId))
    .ToListAsync();
```

## Include (JOIN-like Loading)

Load related documents in a single query instead of N+1 queries:

```csharp
var authorMap = new Dictionary<Guid, AuthorProjection>();
var books = await session.Query<BookSearchProjection>()
    .Include(b => b.AuthorIds, authorMap)
    .Where(b => !b.Deleted)
    .ToListAsync();

// authorMap is now populated with all referenced authors
```

## Natural Keys

Marten supports using a string property as the document identity (instead of `Guid`):

```csharp
// Configure a natural key (string identity)
options.Schema.For<Tenant>()
    .Identity(x => x.Slug);  // Use "slug" string as identity

// Then load by natural key like any other ID
var tenant = await session.LoadAsync<Tenant>("acme-corp");
```

> Natural keys are useful for configuration documents, lookup tables, tenant records, and any entity where a human-readable key is more meaningful than a GUID.

## Document Soft Delete

Marten has a built-in soft delete pattern using `ISoftDeleted`:

```csharp
public class Book : ISoftDeleted
{
    public Guid Id { get; set; }
    public bool Deleted { get; set; }     // ISoftDeleted requirement
    public DateTimeOffset? DeletedAt { get; set; }
    public string Title { get; set; } = string.Empty;
}
```

Soft-deleted documents are excluded from normal queries automatically. Use `MaybeDeletedSince()` or `IsDeleted()` to include them:

```csharp
// Include soft-deleted
var all = await session.Query<Book>()
    .Where(b => b.MaybeDeletedSince(DateTimeOffset.MinValue))
    .ToListAsync();
```

## Global (Non-Tenanted) Documents

When using multi-tenancy but some documents should be shared across all tenants, use `[DoNotPartition]`:

```csharp
[Marten.Schema.DoNotPartition]  // Not partitioned by tenant_id
public class Tenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
```

## Temporal Queries with Version 7 GUIDs

Since Version 7 GUIDs embed a timestamp, you can use them for time-range queries:

```csharp
// Find documents created in the last hour
var cutoff = Guid.CreateVersion7(DateTimeOffset.UtcNow.AddHours(-1));
var recent = await session.Query<BookSearchProjection>()
    .Where(b => b.Id.CompareTo(cutoff) > 0)
    .ToListAsync();
```

> This is why `Guid.CreateVersion7()` is required (not `Guid.NewGuid()`): it gives sequential inserts AND implicit time filtering.
