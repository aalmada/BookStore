---
name: scaffold-read
description: Adds a new read operation (query) to the Backend with Marten queries, Projections, Caching, and Pagination. Use this when implementing GET endpoints or query features.
---

Follow this guide to implement a read-only endpoint in the **Backend** (ApiService) using strict project standards.

1. **Define the DTO**
   - Create a `record` in `src/Shared/BookStore.Shared/Models/`.
   - **Properties**: `Guid Id`, `string Name`, etc.
   - **Localization (Text)**: Map localized text to a single `string` (e.g., `string Description`), NOT a Dictionary.
   - **Data Maps**: Use `IReadOnlyDictionary<string, T>` for non-text maps (e.g., `Prices`).

2. **Define the Projection**
   - Open/Create `src/BookStore.ApiService/Projections/{Resource}Projection.cs`.
   - **Template**: `templates/Projection.cs`
   - **Localization Storage**: Use `Dictionary<string, string>` for storing all translations (e.g., `Descriptions`, `Biographies`).
   - **Reference**: See [marten-guide](../../../docs/guides/marten-guide.md) for projection patterns.

3. **Expose the Endpoint**
   - Open `src/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`.
   - **Template**: `templates/Endpoint.cs`

4. **Implement Caching (HybridCache)**
   - Wrap logic in `cache.GetOrCreateLocalizedAsync` (auto-appends culture to cache key).
   - **Key**: Include page, size, sort, order (culture added automatically).
   - **Tags**: Use `CacheTags.{Resource}List` or `$\"{Resource.ToLower()}:{id}\"`.
   - **Options**: Set `Expiration` (L2/Redis) and `LocalCacheExpiration` (L1/memory).
   - **Reference**: See [caching-guide](../../../docs/guides/caching-guide.md) for HybridCache patterns.

5. **Implement Query (Marten)**
   - Use `await using var session = store.QuerySession();`.
   - **Query**: `session.Query<Projection>()`
   - **Filter**: `.Where(x => !x.Deleted)`
   - **Paginate**: `.ToPagedListAsync(page, pageSize, token)`

6. **Apply Localization**
   - Map from Projection to DTO.
   - **Text**: Use `LocalizationHelper.GetLocalizedValue(proj.Descriptions, culture, defaultCulture, "")`.
   - **Maps**: Copy directly (e.g., `Prices = proj.Prices`).

7. **Client Integration**
   - **Interface**: Create `src/Client/BookStore.Client/IGet{Resource}Endpoint.cs` manually.
   - **Registration**: Add to `BookStoreClientExtensions.cs`.

// turbo
8. **Verify**
   - Run `/verify-feature` to ensure build and tests pass.

## Related Skills

**Prerequisites**:
- `/scaffold-write` - Ensure write operations exist before adding reads
- `/scaffold-projection` - For detailed projection patterns and localization

**Next Steps**:
- `/scaffold-frontend-feature` - Create UI to consume the query
- `/scaffold-test` - Create integration tests for the endpoint
- `/verify-feature` - Complete verification

**Debugging**:
- `/debug-cache` - If caching issues occur

**See Also**:
- [scaffold-projection](../scaffold-projection/SKILL.md) - Advanced projection patterns
- [scaffold-write](../scaffold-write/SKILL.md) - Related write operations
- [caching-guide](../../../docs/guides/caching-guide.md) - HybridCache patterns
- [localization-guide](../../../docs/guides/localization-guide.md) - Localization patterns
- ApiService AGENTS.md - Caching and localization patterns
