# PostgreSQL Extensions and Text Search Optimization

## Required Extensions

Marten's NGram search relies on the `pg_trgm` PostgreSQL extension. It must exist in the database before Marten tries to create NGram indexes — otherwise schema migration fails silently or at runtime.

| Extension | Purpose | Required by |
|-----------|---------|------------|
| `pg_trgm` | Trigram-based fuzzy/partial-word matching | `NgramIndex`, `NgramSearch` |
| `unaccent` | Strips diacritics/accents from strings | `UseNGramSearchWithUnaccent` (optional) |

> Both extensions ship with standard PostgreSQL. No extra install is needed — just `CREATE EXTENSION`.

### Register extensions with Marten

Marten can create the extensions automatically via `Weasel.Postgresql.Extension`:

```csharp
using Weasel.Postgresql;

// In AddMarten() options setup:
options.Storage.ExtendedSchemaObjects.Add(new Extension("pg_trgm"));

// Optional: for accent-insensitive NGram search
options.Storage.ExtendedSchemaObjects.Add(new Extension("unaccent"));
```

With `AutoCreateSchemaObjects = AutoCreate.All` (development) Marten runs
`CREATE EXTENSION IF NOT EXISTS pg_trgm` on startup. In production (`AutoCreate.CreateOnly`) extensions are also created if missing.

### Aspire: provision the extension via a creation script

When running with Aspire using the PostgreSQL container, pass a SQL creation script through `WithCreationScript`:

```csharp
// AppHost.cs
var postgres = builder.AddPostgres(ResourceNames.Postgres)
    .WithCreationScript("sql/create-extensions.sql");
```

```sql
-- sql/create-extensions.sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent; -- only if using UseNGramSearchWithUnaccent
```

> The comment `// Add PostgreSQL with pg_trgm extension for ngram search` in `AppHost.cs` serves as a reminder that `pg_trgm` is a runtime dependency of the API service.

---

## Search Strategies

### 1. NGram search (`NgramIndex` + `NgramSearch`) — **preferred for partial-word matching**

Uses `pg_trgm` to index every 3-character sequence (trigram) of a string. A query term is also broken into trigrams and compared against the index.

**Why to use it:** Works for mid-word substrings ("clea" → "clean", "agil" → "agile"), typo tolerance, autocomplete. Does not require full words or specific word boundaries.

**Configuration:**
```csharp
// Register index when configuring Marten
options.Schema.For<BookSearchProjection>()
    .NgramIndex(x => x.Title)
    .NgramIndex(x => x.AuthorNames);
```

```csharp
// LINQ query
var results = await session.Query<BookSearchProjection>()
    .Where(b => b.Title.NgramSearch("clea"))
    .ToListAsync();
```

**Multi-field pattern — use a computed `SearchText` property:**

Instead of querying multiple NGram indexes, concatenate searchable fields into one property and put a single index on it. This keeps query code simple and index count low:

```csharp
// Projection property
public string SearchText { get; set; } = string.Empty;

// In projection logic
static void UpdateSearchText(BookSearchProjection p) =>
    p.SearchText = $"{p.Title} {p.Isbn ?? string.Empty} {p.PublisherName ?? string.Empty} {p.AuthorNames}".Trim();

// Single index covers all fields
options.Schema.For<BookSearchProjection>()
    .NgramIndex(x => x.SearchText);
```

**Accent-insensitive variant:**

When users may search with or without diacritics (e.g., "bjork" → "Björk"), enable unaccent:

```csharp
// Requires unaccent extension to be installed first
options.Advanced.UseNGramSearchWithUnaccent = true;
```

This wraps the indexed column and the query term in `unaccent()` so "uðmu" does not match "umut", but "bjork" does match "Björk".

---

### 2. Full-Text Search (`FullTextIndex` + `PlainTextSearch` / `PhraseSearch` / `WebStyleSearch`)

Uses PostgreSQL's native `tsvector`/`tsquery` full-text search. Lexemes (stemmed word roots), stop-word removal, and language-aware dictionaries. Does **not** support partial words — "clean" matches "cleaned", "cleaning", but not "clea".

```csharp
// Index (GIN over tsvector)
options.Schema.For<BlogPost>()
    .FullTextIndex(d => d.Body)               // "english" language config by default
    .FullTextIndex(index => index.RegConfig = "portuguese", d => d.Body);

// Query variants
session.Query<BlogPost>().Where(x => x.Body.PlainTextSearch("software design"))   // plainto_tsquery
session.Query<BlogPost>().Where(x => x.Body.PhraseSearch("software design"))      // phraseto_tsquery
session.Query<BlogPost>().Where(x => x.Body.WebStyleSearch("software OR design")) // websearch_to_tsquery (PG11+)
session.Query<BlogPost>().Where(x => x.Body.Search("software & design"))          // to_tsquery (raw operators)
```

**When to prefer full-text over NGram:**
- Body text, descriptions, long-form content — documents where word semantics matter
- Multiple languages with language-specific stemming
- Users type full words, not partial terms

---

### 3. GIN Index on JSON Data (`GinIndexJsonData`)

Indexes the entire JSONB column with a GIN index. This accelerates ad-hoc queries on any JSON key, including nested paths, without needing individual indexes per field.

```csharp
options.Schema.For<BookSearchProjection>().GinIndexJsonData();
```

**When to use:** Useful for ad-hoc queries against many fields, or when fields queried are not predefined. Not needed if you have explicit computed indexes (`Index(x => x.Field)`) on every queried property — those are more selective.

---

## Index Strategy in This Project

The project's `ConfigureIndexes` method applies this strategy consistently across projections:

| Projection | B-tree (sorting/exact) | NGram (search) | GIN JSON |
|-----------|----------------------|---------------|---------|
| `BookSearchProjection` | `Title`, `PublisherId`, `Deleted` | `Title`, `AuthorNames` | ✓ |
| `AuthorProjection` | `Name`, `Deleted` | `Name` | — |
| `PublisherProjection` | `Name`, `Deleted` | `Name` | — |
| `ApplicationUser` | `NormalizedEmail`, `NormalizedUserName`, `CreatedAt` | `Email` | ✓ |

**Pattern:** Index the field twice — once with `Index()` for exact-match and sort operations, once with `NgramIndex()` for search. These are independent indexes and serve different queries.

```csharp
options.Schema.For<AuthorProjection>()
    .Index(x => x.Name)       // ORDER BY / exact match
    .NgramIndex(x => x.Name)  // WHERE Name.NgramSearch(...)
    .Index(x => x.Deleted);   // WHERE Deleted = false
```

**Partial index for filtered queries:** For columns always queried with a constant predicate (e.g., `Deleted = false`), a partial index reduces index size:

```csharp
options.Schema.For<ApplicationUser>()
    .Index(x => x.CreatedAt, idx =>
    {
        idx.Predicate = "data ->> 'EmailConfirmed' = 'false'";
        idx.Name = "idx_application_user_unverified_created_at";
    });
```

---

## Index Type Reference

| Index | SQL type | Best for | Requires |
|-------|---------|----------|---------|
| `NgramIndex` | GIN (pg_trgm) | Partial/fuzzy word matching | `pg_trgm` extension |
| `FullTextIndex` | GIN (tsvector) | Whole-word linguistic search | Built-in PG FTS |
| `GinIndexJsonData` | GIN (jsonb_path_ops) | Ad-hoc JSON queries | — |
| `Index` (default) | B-tree | Equality, range, sort | — |
| `Index(..., IndexMethod.GIN)` | GIN (custom) | Custom GIN expressions | depends |

---

## Common Mistakes

| Problem | Cause | Fix |
|---------|-------|-----|
| `NgramSearch` returns nothing | `pg_trgm` extension not installed | Add `new Extension("pg_trgm")` to `ExtendedSchemaObjects`, or use `WithCreationScript` in Aspire |
| Partial search misses accented names | `unaccent` not enabled | Set `options.Advanced.UseNGramSearchWithUnaccent = true` and install the `unaccent` extension |
| Slow search across many fields | Multiple `NgramIndex` hits | Consolidate into one computed `SearchText` field with a single `NgramIndex` |
| `FullTextIndex` doesn't match substrings | tsvector uses whole-word lexemes | Switch to `NgramIndex` for partial/autocomplete use cases |
| Too many GIN indexes slow writes | Separate NGram + GIN JSON indexes per projection | Remove `GinIndexJsonData` when explicit `Index()` columns cover all query paths |
