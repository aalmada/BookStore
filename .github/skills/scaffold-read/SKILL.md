---
name: Scaffold Read
description: Guide for adding a new read operation (query) to the Backend. Focuses on Marten queries, Projections, Caching, and Pagination.
---

Follow this guide to implement a read-only endpoint in the **Backend** (ApiService) using strict project standards.

1. **Define the DTO**
   - Create a `record` in `src/Shared/BookStore.Shared/Models/`.
   - **Properties**: `Guid Id`, `string Name`, etc.
   - **Localization (Text)**: Map localized text to a single `string` (e.g., `string Description`), NOT a Dictionary.
   - **Data Maps**: Use `IReadOnlyDictionary<string, T>` for non-text maps (e.g., `Prices`).

2. **Define the Projection**
   - Open/Create `src/ApiService/BookStore.ApiService/Projections/{Resource}Projection.cs`.
   - **Localization Storage**: Use `Dictionary<string, string>` for storing all translations (e.g., `Descriptions`, `Biographies`).

3. **Expose the Endpoint**
   - Open `src/ApiService/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`.
   - **Signature**:
     ```csharp
     static async Task<Ok<PagedListDto<Dto>>> GetItems(
         [FromServices] IDocumentStore store,
         [FromServices] HybridCache cache,
         [FromServices] IOptions<LocalizationOptions> locOptions,
         [AsParameters] OrderedPagedRequest request,
         CancellationToken cancellationToken)
     ```

4. **Implement Caching (HybridCache)**
   - Wrap logic in `cache.GetOrCreateLocalizedAsync`.
   - **Key**: Include page, size, sort, order, AND culture.
   - **Tags**: Use `CacheTags.{Resource}List`.

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

8. **Verify**
   - Run `/verify-feature` to ensure build and tests pass.
