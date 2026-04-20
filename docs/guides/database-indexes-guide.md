# Database Indexes Guide

> **Note**: This guide focuses on Marten index configuration for query performance. For multi-tenancy architecture and implementation details, see [Multi-Tenancy Guide](multi-tenancy-guide.md). For Marten setup and projections, see [Marten Guide](marten-guide.md).

## Overview

Indexes are declared in C# using the Marten `Schema.For<T>()` fluent API inside `ConfigureIndexes` in `MartenConfigurationExtensions.cs`. Marten generates and applies the corresponding PostgreSQL DDL automatically at startup (or during schema migrations), so there are **no hand-written SQL `CREATE INDEX` statements** in this project.

All document tables are multi-tenanted (via `options.Policies.AllDocumentsAreMultiTenanted()`), which means Marten adds a `tenant_id` column to every document table and includes it in the primary index automatically.

## Index Configuration Location

**File**: [src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs](../../src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs) — `ConfigureIndexes` method.

## Defined Indexes

### `BookSearchProjection`

```csharp
options.Schema.For<BookSearchProjection>()
    .Index(x => x.PublisherId!)     // B-tree: exact publisher lookups
    .Index(x => x.Title)            // B-tree: sorting and filtering by title
    .GinIndexJsonData()             // GIN: full JSON document — enables fast JSONB containment queries
    .NgramIndex(x => x.Title)       // NGram (trigram GIN): fuzzy/partial-match search on title
    .NgramIndex(x => x.AuthorNames) // NGram (trigram GIN): fuzzy/partial-match search on author names
    .Index(x => x.Deleted);         // B-tree: soft-delete filtering (WHERE Deleted = false)
```

| Index | Type | Purpose |
|-------|------|---------|
| `PublisherId` | B-tree | Filtering books by publisher |
| `Title` | B-tree | Sorting results and equality lookups |
| JSON data | GIN | Fast JSONB containment (`@>`) queries |
| `Title` (NGram) | Trigram GIN | Fuzzy/substring search on book title |
| `AuthorNames` (NGram) | Trigram GIN | Fuzzy/substring search on author names |
| `Deleted` | B-tree | Excluding soft-deleted books from results |

### `AuthorProjection`

```csharp
options.Schema.For<AuthorProjection>()
    .Index(x => x.Name)         // B-tree: sorting and filtering by name
    .NgramIndex(x => x.Name)    // NGram (trigram GIN): fuzzy/partial-match search on name
    .Index(x => x.Deleted);     // B-tree: soft-delete filtering
```

| Index | Type | Purpose |
|-------|------|---------|
| `Name` | B-tree | Sorting results and equality lookups |
| `Name` (NGram) | Trigram GIN | Fuzzy/substring search on author name |
| `Deleted` | B-tree | Excluding soft-deleted authors from results |

### `CategoryProjection`

```csharp
options.Schema.For<CategoryProjection>()
    .Index(x => x.Deleted);     // B-tree: soft-delete filtering
```

| Index | Type | Purpose |
|-------|------|---------|
| `Deleted` | B-tree | Excluding soft-deleted categories from results |

### `PublisherProjection`

```csharp
options.Schema.For<PublisherProjection>()
    .Index(x => x.Name)         // B-tree: sorting and filtering by name
    .NgramIndex(x => x.Name)    // NGram (trigram GIN): fuzzy/partial-match search on name
    .Index(x => x.Deleted);     // B-tree: soft-delete filtering
```

| Index | Type | Purpose |
|-------|------|---------|
| `Name` | B-tree | Sorting results and equality lookups |
| `Name` (NGram) | Trigram GIN | Fuzzy/substring search on publisher name |
| `Deleted` | B-tree | Excluding soft-deleted publishers from results |

### `ApplicationUser` (Identity)

```csharp
options.Schema.For<ApplicationUser>()
    .UniqueIndex(UniqueIndexType.Computed, x => x.NormalizedEmail!)       // Unique: enforce unique email
    .UniqueIndex(UniqueIndexType.Computed, x => x.NormalizedUserName!)    // Unique: enforce unique username
    .Index(x => x.NormalizedEmail)                                         // B-tree: login lookup by email
    .Index(x => x.NormalizedUserName)                                      // B-tree: login lookup by username
    .GinIndexJsonData()                                                    // GIN: full JSON document
    .NgramIndex(x => x.Email!)                                             // NGram: fuzzy email search (admin)
    .Index(x => x.CreatedAt)                                               // B-tree: sort/filter by creation date
    .Index(x => x.CreatedAt, idx =>
    {
        idx.Predicate = "data ->> 'EmailConfirmed' = 'false'";
        idx.Name = "idx_application_user_unverified_created_at";
    });                                                                    // Partial B-tree: unverified users sorted by creation date
```

| Index | Type | Purpose |
|-------|------|---------|
| `NormalizedEmail` | Unique (Computed) | Enforce unique email constraint |
| `NormalizedUserName` | Unique (Computed) | Enforce unique username constraint |
| `NormalizedEmail` | B-tree | Fast login lookup by normalised email |
| `NormalizedUserName` | B-tree | Fast login lookup by normalised username |
| JSON data | GIN | Fast JSONB containment queries |
| `Email` (NGram) | Trigram GIN | Fuzzy/partial email search in admin UI |
| `CreatedAt` | B-tree | Sorting users by registration date |
| `CreatedAt` (partial, `EmailConfirmed = false`) | B-tree | Efficiently query unverified accounts for cleanup/alerting |

### Projections Without Explicit Indexes

The following projections are persisted but do not define explicit indexes in `ConfigureIndexes`. They are primarily accessed by document ID (primary key), so additional indexes are not currently required.

#### `UserProfile`
- Access pattern: direct lookup by `userId`
- Why no explicit index: shopping cart/favorites data is loaded by ID, not searched

#### `BookStatistics`
- Access pattern: direct lookup by `bookId`
- Why no explicit index: statistics are loaded per book detail view

#### `AuthorStatistics`
- Access pattern: direct lookup by `authorId`
- Why no explicit index: aggregate counters are loaded by ID only

#### `CategoryStatistics`
- Access pattern: direct lookup by `categoryId`
- Why no explicit index: aggregate counters are loaded by ID only

#### `PublisherStatistics`
- Access pattern: direct lookup by `publisherId`
- Why no explicit index: aggregate counters are loaded by ID only

## Index Types Used

| Type | Marten API | When to use |
|------|-----------|-------------|
| B-tree | `.Index(x => x.Field)` | Equality, range, and ORDER BY on scalar fields |
| Unique B-tree | `.UniqueIndex(UniqueIndexType.Computed, x => x.Field)` | Enforce uniqueness at DB level |
| GIN (full JSON) | `.GinIndexJsonData()` | JSONB containment (`@>`) across all document fields |
| Trigram GIN (NGram) | `.NgramIndex(x => x.Field)` | Substring / fuzzy search (`LIKE '%term%'`, `%` similarity) |
| Partial B-tree | `.Index(x => x.Field, idx => idx.Predicate = "...")` | Index a subset of rows to reduce size and improve selectivity |

## Multi-Tenancy and Indexes

Because `AllDocumentsAreMultiTenanted()` is active, Marten automatically includes `tenant_id` in the document tables. Marten's tenant-aware querying automatically scopes all queries with `WHERE mt_doc_xxx.tenant_id = :tenantId`, so Marten-managed indexes do **not** need to be manually made composite with `tenant_id` — PostgreSQL will combine the implicit tenant filter with the declared index efficiently.

The `Tenant` document type itself is decorated with `[DoNotPartition]` and is excluded from multi-tenancy.

## Adding New Indexes

1. Open `MartenConfigurationExtensions.cs` and find `ConfigureIndexes`.
2. Add index declarations on the appropriate `options.Schema.For<T>()` chain:

```csharp
// B-tree index on a scalar field
options.Schema.For<MyProjection>()
    .Index(x => x.SomeField);

// NGram (trigram) index for fuzzy search
options.Schema.For<MyProjection>()
    .NgramIndex(x => x.SomeTextField);

// Partial index — only index rows that match a condition
options.Schema.For<MyProjection>()
    .Index(x => x.SomeField, idx =>
    {
        idx.Predicate = "data ->> 'SomeFlag' = 'true'";
        idx.Name = "idx_my_projection_some_field_partial";
    });
```

3. Run the application in development (`AutoCreate.All`) to apply the schema change, or generate a migration script for production.

> **Tip**: In production `AutoCreate.CreateOnly` is used — new indexes are created but existing ones are never modified. For index changes on existing columns (e.g., renaming, adding a predicate), create an explicit database migration.

## Performance Monitoring

```sql
-- Check which indexes are being used
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan          AS index_scans,
    idx_tup_read      AS tuples_read,
    idx_tup_fetch     AS tuples_fetched
FROM pg_stat_user_indexes
WHERE tablename LIKE 'mt_doc_%'
ORDER BY idx_scan DESC;

-- Identify sequential scans (potential missing indexes)
SELECT
    schemaname,
    tablename,
    seq_scan,
    seq_tup_read,
    idx_scan,
    CASE WHEN seq_scan > 0 THEN seq_tup_read / seq_scan ELSE 0 END AS avg_seq_read
FROM pg_stat_user_tables
WHERE schemaname = 'public'
  AND tablename LIKE 'mt_doc_%'
  AND seq_scan > 0
ORDER BY seq_tup_read DESC;

-- Explain a specific query to verify index usage
EXPLAIN (ANALYZE, BUFFERS) SELECT ...;
```

## Maintenance

Marten-generated indexes follow standard PostgreSQL maintenance rules:

- **VACUUM ANALYZE**: Run regularly to keep planner statistics current. Configure `autovacuum` for high-write tables.
- **REINDEX CONCURRENTLY**: Use to rebuild bloated indexes without locking writes. Schedule during low-traffic windows if needed.
- **Bloat monitoring**: Use the `pgstattuple` extension or a bloat query to detect indexes that need rebuilding.
