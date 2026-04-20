# ETag Support in Book Store API

## Overview

The Book Store API implements **ETags (Entity Tags)** for:

1. **Optimistic Concurrency Control** — mandatory `If-Match` header for all write operations
2. **HTTP Caching** — `If-None-Match` / `304 Not Modified` for conditional GET requests
3. **DTO versioning** — every resource DTO carries an `ETag` field so clients always have the current version

ETags are generated from Marten's event-stream versions, so they directly reflect the number of events applied to that aggregate.

> [!IMPORTANT]
> **ETags are mandatory** for admin state-changing operations (PUT, DELETE, POST restore).
> A missing `If-Match` header returns `428 Precondition Required`.
> Public operations like book sales support optional ETags but are not enforced by middleware.

---

## Architecture

The ETag implementation spans four components:

| Component | Location | Purpose |
|---|---|---|
| `ETagValidationMiddleware` | `src/BookStore.ApiService/Infrastructure/ETagValidationMiddleware.cs` | Enforces `If-Match` presence on writes; returns 428 when missing |
| `WolverineETagMiddleware` | `src/BookStore.ApiService/Infrastructure/WolverineETagMiddleware.cs` | Propagates `If-Match` header value into Wolverine commands implementing `IHaveETag` |
| `ETagHelper` (server) | `src/BookStore.ApiService/Infrastructure/ETagHelper.cs` | Generates, parses, and validates ETags; `WithETag()` extension for responses |
| `ETagHelper` (client) | `src/BookStore.Client/ETagHelper.cs` | Same generation / parsing utilities for use in the Refit client layer |

### ETag Format

```
ETag = "<stream_version>"
Example: "5"  (Marten event stream version 5)
```

`"*"` is accepted in `If-Match` as a wildcard (matches any version).

### Middleware Pipeline

```
Request → ETagValidationMiddleware → (enforce If-Match present)
        → WolverineETagMiddleware  → (copy If-Match → command.ETag)
        → Handler                  → (compare command.ETag with aggregate.Version)
```

1. **`ETagValidationMiddleware`** rejects write requests that lack `If-Match` with `428 Precondition Required`.
2. **`WolverineETagMiddleware`** reads `If-Match` and sets it on any command that implements `IHaveETag`.
3. **Handlers** call `ETagHelper.ParseETag(command.ETag)` and compare with the loaded aggregate's version; a mismatch returns `ETagHelper.PreconditionFailed()` (412).

### `IHaveETag` Interface

Any command that requires ETag protection implements:

```csharp
public interface IHaveETag
{
    string? ETag { get; set; }
}
```

`WolverineETagMiddleware` automatically populates `ETag` on these commands from the `If-Match` request header.

### `WithETag()` Response Extension

Single-resource GET endpoints attach the ETag as a response header using:

```csharp
return TypedResults.Ok(dto).WithETag(dto.ETag!);
```

This is defined in `ETagResultExtensions` and sets `response.Headers.ETag`.

---

## ETag Coverage by Resource

| Resource | GET list includes ETag | GET single sets ETag header | GET single supports 304 | Writes require If-Match |
|---|---|---|---|---|
| **Books** | ✅ (in DTO) | ✅ (`If-None-Match` / 304) | ✅ (checks stream state) | ✅ |
| **Authors** | ✅ (in DTO) | ✅ (`WithETag`) | ❌ | ✅ |
| **Publishers** | ✅ (in DTO) | ✅ (`WithETag`) | ❌ | ✅ |
| **Categories** | ✅ (in DTO) | ✅ (`WithETag`) | ❌ | ✅ |

### Middleware Exclusions

The following endpoints are **excluded** from the `If-Match` requirement:

- `POST /api/books/{id}/rating` — high-concurrency rating endpoint
- `POST /api/books/{id}/favorites` — add to favorites
- `DELETE /api/books/{id}/favorites` — remove from favorites
- `POST|DELETE /api/cart/**` — shopping cart operations

### ETag Enforcement by Operation

| Operation | Endpoint | If-Match Required | Handler ETag Validation |
|---|---|---|---|
| Update book | `PUT /api/admin/books/{id}` | ✅ Yes (428 if missing) | ✅ Returns 412 on mismatch |
| Soft delete book | `DELETE /api/admin/books/{id}` | ✅ Yes (428 if missing) | ✅ Returns 412 on mismatch |
| Restore book | `POST /api/admin/books/{id}/restore` | ✅ Yes (428 if missing) | ✅ Returns 412 on mismatch |
| Schedule sale | `POST /api/books/{id}/sales` | ❌ Optional | ✅ Validates if provided |
| Cancel sale | `DELETE /api/books/{id}/sales` | ❌ Optional | ✅ Validates if provided |
| Rate / Favorite | `POST /api/books/{id}/rating|favorites` | ❌ No | N/A |
| Cart operations | `POST|DELETE /api/cart/**` | ❌ No | N/A |

---

## ETag in DTOs

Every resource DTO carries an `ETag` field alongside the data:

```csharp
record BookDto(..., string? ETag = null);
record AuthorDto(..., string? ETag = null);
record PublisherDto(..., string? ETag = null);
record CategoryDto(..., string? ETag = null);
record AdminBookDto(..., string? ETag = null);
```

This means clients can read the current version from the deserialized object body rather than having to parse the response header, but the header is also set for single-resource GETs.

---

## Read Operations (GET)

### GET single book — with `If-None-Match` / 304 support

`GET /api/books/{id}` performs an explicit stream-state check **before** loading from cache, enabling proper 304 responses:

**First request:**
```http
GET /api/books/{id}
→ 200 OK
ETag: "3"
```

**Conditional request:**
```http
GET /api/books/{id}
If-None-Match: "3"
→ 304 Not Modified   (no body, saves bandwidth)
```

**After the book is updated:**
```http
GET /api/books/{id}
If-None-Match: "3"
→ 200 OK
ETag: "4"
{...full body...}
```

### GET single author / publisher / category

These endpoints return `ETag` in both the DTO body and as the `ETag` response header (via `WithETag`), but do **not** perform conditional-request checking — they always return 200 with the full body.

```http
GET /api/authors/{id}
→ 200 OK
ETag: "2"
{ "id": "...", "name": "...", "etag": "\"2\"" }
```

### GET list responses

List endpoints embed the ETag for each item inside the DTO. This allows the UI to display detail pages and immediately know the ETag for subsequent writes without an extra GET.

---

## Write Operations (PUT / DELETE / POST)

### General flow

```
1. Client reads a resource  →  saves the ETag value (e.g. "5")
2. Client submits mutation with If-Match: "5"
3. If aggregate.Version == 5  →  mutation applied, response ETag = "6"
4. If aggregate.Version != 5  →  412 Precondition Failed
5. If If-Match header missing →  428 Precondition Required
```

### Update

```http
PUT /api/admin/books/{id}
If-Match: "3"
Content-Type: application/json

{ "title": "Clean Code (Updated)", ... }

→ 204 No Content
```

### Soft Delete

```http
DELETE /api/admin/books/{id}
If-Match: "4"

→ 204 No Content
```

### Restore

```http
POST /api/admin/books/{id}/restore
If-Match: "5"

→ 204 No Content
```

The same pattern applies to authors (`/api/admin/authors/{id}`), publishers (`/api/admin/publishers/{id}`), and categories (`/api/admin/categories/{id}`).

### Optional ETag Operations

Some public operations accept `If-Match` but do not require it at middleware level. Sales endpoints are the primary example.

```http
POST /api/books/{id}/sales
If-Match: "5"
```

When provided, handlers validate the ETag and return `412 Precondition Failed` on mismatch. Without `If-Match`, these operations can still proceed if business rules allow.

---

## Error Responses

### 428 Precondition Required

`If-Match` header is missing from a write request.

```json
{
  "title": "Precondition Required",
  "status": 428,
  "detail": "The If-Match header is required for PUT /api/admin/books/{id}."
}
```

**Client action:** Fetch the current version and include `If-Match` with the value from `ETag`.

### 412 Precondition Failed

The `If-Match` ETag does not match the aggregate's current version (a concurrent modification occurred).

```json
{
  "title": "Precondition Failed",
  "status": 412,
  "detail": "The resource has been modified since you last retrieved it. Please refresh and try again."
}
```

**Client action:**
1. Notify the user that a conflict occurred.
2. Fetch the latest version.
3. Ask the user to review changes and resubmit.

---

## Refit Client Usage

The `BookStore.Client` Refit interfaces expose ETag headers as optional parameters.

### Reading ETags

```csharp
// Single resource — ETag in both DTO body and response header
IApiResponse<PublisherDto> response = await publishers.GetPublisherWithResponseAsync(id);
string etag = response.Content!.ETag!;   // from DTO body — always available
// or: response.Headers.ETag?.ToString()  — from HTTP header on single-resource GETs

// List response — each item has its own ETag
PagedListDto<AuthorDto> authors = await authorsClient.GetAuthorsAsync(page: 1, pageSize: 20);
string authorEtag = authors.Items[0].ETag!;
```

### Writing with If-Match

```csharp
// Update (PUT)
await publishers.UpdatePublisherAsync(id, request, etag: dto.ETag);

// Delete
await publishers.SoftDeletePublisherAsync(id, etag: dto.ETag);

// Restore (POST)
await books.RestoreBookAsync(id, etag: dto.ETag);
```

### Handling 412 in the client

```csharp
IApiResponse response = await publishers.UpdatePublisherWithResponseAsync(id, request, etag: dto.ETag);

if (response.StatusCode == HttpStatusCode.PreconditionFailed)
{
    // Concurrent modification: refresh and re-present to user
    var latest = await publishers.GetPublisherAsync(id);
    // ... show conflict resolution UI
}
```

### Client-side `ETagHelper`

`BookStore.Client.ETagHelper` provides the same generation and parsing utilities:

```csharp
// Generate (used internally by DTO mapping)
string etag = ETagHelper.GenerateETag(version);  // → "\"5\""

// Parse
long? version = ETagHelper.ParseETag(dto.ETag);

// Try-parse
if (ETagHelper.TryParseETag(dto.ETag, out long ver))
{
    // use ver
}
```

---

## Workflow Examples

### Example 1: Safe Update

```bash
# 1. Get current resource
GET /api/publishers/123
# Response: 200 OK, ETag: "5", body includes etag: "\"5\""

# 2. Submit update with ETag
PUT /api/admin/publishers/123
If-Match: "5"
{ "name": "Updated Name" }
# Response: 204 No Content
```

### Example 2: Concurrent Update Detection

```bash
# User A fetches publisher
GET /api/publishers/123   → ETag: "5"

# User B fetches publisher
GET /api/publishers/123   → ETag: "5"

# User B updates first
PUT /api/admin/publishers/123
If-Match: "5"            → 204 No Content (ETag now "6")

# User A tries to update with stale ETag
PUT /api/admin/publishers/123
If-Match: "5"            → 412 Precondition Failed

# User A refreshes and retries
GET /api/publishers/123   → ETag: "6"
PUT /api/admin/publishers/123
If-Match: "6"            → 204 No Content
```

### Example 3: Conditional GET (Books only)

```bash
# First request
GET /api/books/123        → 200 OK, ETag: "5"

# Subsequent request with cached ETag
GET /api/books/123
If-None-Match: "5"        → 304 Not Modified (no body)

# After book is updated
GET /api/books/123
If-None-Match: "5"        → 200 OK, ETag: "6"
```

---

## Testing ETags with curl

```bash
# GET book and inspect ETag
curl -i http://localhost:5000/api/books/{id}

# Conditional GET (304 if unchanged)
curl -i http://localhost:5000/api/books/{id} \
  -H 'If-None-Match: "5"'

# Update with correct ETag (204)
curl -X PUT http://localhost:5000/api/admin/books/{id} \
  -H 'If-Match: "5"' \
  -H 'Content-Type: application/json' \
  -d '{"title":"Updated",...}'

# Update with wrong ETag (412)
curl -X PUT http://localhost:5000/api/admin/books/{id} \
  -H 'If-Match: "999"' \
  -H 'Content-Type: application/json' \
  -d '{"title":"Updated",...}'

# Update without ETag (428)
curl -X PUT http://localhost:5000/api/admin/books/{id} \
  -H 'Content-Type: application/json' \
  -d '{"title":"Updated",...}'
```

---

## Summary

| Concern | Mechanism |
|---|---|
| **ETag value** | Quoted Marten stream version: `"5"` |
| **ETag on reads** | Embedded in DTO body; also set as `ETag` response header on single-resource GETs |
| **Conditional GET** | `If-None-Match` → `304 Not Modified` for `GET /api/books/{id}` only |
| **Write enforcement** | `ETagValidationMiddleware` → `428` if `If-Match` missing |
| **Write validation** | Handler compares `ETag` with `aggregate.Version` → `412` if mismatch |
| **Command propagation** | `WolverineETagMiddleware` copies `If-Match` → `command.ETag` for `IHaveETag` commands |
| **Refit client** | `[Header("If-Match")]` / `[Header("If-None-Match")]` parameters on write/read methods |
| **Exclusions** | `/rating`, `/favorites`, `/api/cart` do not require `If-Match` |
