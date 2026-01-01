# API Client Update Workflow

Quick reference for updating the BookStore API client when the API changes.

## Workflow

### 1. Start the API

```bash
aspire run
```

### 2. Update OpenAPI Spec

```bash
./_tools/update-openapi.sh
```

This downloads the latest OpenAPI spec from the running API to `openapi.json`.

### 3. Update Client (Choose One)

#### Option A: NSwag Auto-Generation (Recommended)

```bash
./_tools/generate-client-nswag.sh
```

**Pros**:
- ✅ Automatic - generates entire interface
- ✅ Consistent - always matches OpenAPI spec
- ✅ Fast - one command

**Cons**:
- ❌ Requires NSwag CLI installed
- ❌ May need manual cleanup

#### Option B: Manual Update

Edit `src/Client/BookStore.Client/IBookStoreApi.cs` manually.

**Pros**:
- ✅ Full control over interface
- ✅ Clean, minimal code
- ✅ No tool dependencies

**Cons**:
- ❌ Manual work
- ❌ Can get out of sync

### 4. Build and Test

```bash
dotnet build
dotnet test
```

### 5. Commit Changes

```bash
git add openapi.json src/Client/BookStore.Client/IBookStoreApi.cs
git commit -m "Update API client: [description]"
```

## Installing NSwag CLI (Optional)

If you want to use auto-generation:

```bash
dotnet tool install --global NSwag.ConsoleCore
```

## Example: Adding a New Endpoint

### API Side

```csharp
// Add new endpoint in BookEndpoints.cs
app.MapGet("/api/books/{id}/reviews", GetBookReviews);
```

### Client Side (Manual)

```csharp
// Add to IBookStoreApi.cs
[Get("/api/books/{id}/reviews")]
Task<PagedListDto<ReviewDto>> GetBookReviews(
    Guid id,
    [Query] int page = 1,
    [Query] int pageSize = 20,
    CancellationToken cancellationToken = default);
```

### Client Side (NSwag)

```bash
# Just run the scripts
./_tools/update-openapi.sh
./_tools/generate-client-nswag.sh
```

## Tips

- **Commit `openapi.json`** - Track API changes in git
- **Review diffs** - Check what changed before committing
- **Test after updates** - Ensure client still works
- **Keep in sync** - Update client when API changes

## Current Approach

We use **manual updates** because:
- ✅ Clean, minimal interface
- ✅ Full control over method signatures
- ✅ No build-time dependencies
- ✅ Works in all environments

NSwag is available as an **optional tool** for quick regeneration when needed.
