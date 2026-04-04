# Interface Design

## HTTP verb attributes

Every interface method needs exactly one verb attribute. The path can contain `{param}` placeholders that match method parameter names.

```csharp
[Get("/api/users/{id}")]
[Post("/api/users")]
[Put("/api/users/{id}")]
[Patch("/api/users/{id}")]
[Delete("/api/users/{id}")]
[Head("/api/users/{id}")]
[Options("/api/users")]
```

## Parameter binding

Refit infers binding from the method signature:

| Source | Attribute | Notes |
|--------|-----------|-------|
| URL path segment | _(none — name match)_ | `{id}` in path → `Guid id` parameter |
| Query string | `[Query]` | Works on primitives and objects (flattened) |
| Request body | `[Body]` | JSON by default; one per method |
| Header (per call) | `[Header("Name")]` | Dynamic value per call |
| Auth header | `[Authorize("Bearer")]` | Shortcut for `Authorization: Bearer <value>` |
| URL alias | `[AliasAs("snake_name")]` | Rename path/query param |

```csharp
// Path param — automatic by name match
[Get("/api/orders/{orderId}")]
Task<OrderDto> GetOrderAsync(Guid orderId, CancellationToken ct = default);

// Query object — all public properties become query params
[Get("/api/orders")]
Task<PagedList<OrderDto>> SearchOrdersAsync([Query] OrderSearchRequest request, CancellationToken ct = default);

// Query collection — repeat key=value for each element
[Get("/api/products")]
Task<List<ProductDto>> GetByTagsAsync([Query(CollectionFormat.Multi)] IEnumerable<string> tags, CancellationToken ct = default);

// Body — serialized as JSON
[Post("/api/orders")]
Task<OrderDto> CreateOrderAsync([Body] CreateOrderRequest body, CancellationToken ct = default);

// Dynamic header per call
[Put("/api/orders/{id}")]
Task UpdateOrderAsync(Guid id, [Body] UpdateOrderRequest body, [Header("If-Match")] string? etag = null, CancellationToken ct = default);
```

## Return types

| Return type | Behavior on non-2xx |
|-------------|---------------------|
| `Task` | Throws `ApiException` |
| `Task<T>` | Throws `ApiException` |
| `Task<IApiResponse>` | Returns; check `IsSuccessful` |
| `Task<IApiResponse<T>>` | Returns; check `IsSuccessful` then use `.Content` |

Prefer `Task<IApiResponse<T>>` for mutating calls where you need to inspect status codes or handle errors without exceptions. Prefer `Task<T>` for read-only calls where the caller should propagate exceptions.

```csharp
public interface IOrdersClient
{
    // Throws on failure — fine for reads where caller handles exceptions
    [Get("/api/orders/{id}")]
    Task<OrderDto> GetOrderAsync(Guid id, CancellationToken ct = default);

    // Non-throwing — useful for writes where 4xx codes mean "business rule failed"
    [Post("/api/orders")]
    Task<IApiResponse<OrderDto>> CreateOrderAsync([Body] CreateOrderRequest body, CancellationToken ct = default);

    // Non-throwing with no body — for writes that return 204
    [Delete("/api/orders/{id}")]
    Task<IApiResponse> DeleteOrderAsync(Guid id, [Header("If-Match")] string? etag = null, CancellationToken ct = default);
}
```

## Static headers

Use `[Headers]` on the interface or method to add headers that never change. Method-level headers override interface-level ones.

```csharp
[Headers("Accept: application/json", "X-Api-Version: 2")]
public interface IGitHubApi
{
    [Get("/repos/{owner}/{repo}")]
    Task<Repository> GetRepositoryAsync(string owner, string repo, CancellationToken ct = default);

    // Override Accept for this method only
    [Headers("Accept: application/vnd.github.raw")]
    [Get("/repos/{owner}/{repo}/readme")]
    Task<string> GetReadmeAsync(string owner, string repo, CancellationToken ct = default);
}
```

## Multipart / file upload

```csharp
[Multipart]
[Post("/api/books/{id}/cover")]
Task<IApiResponse> UploadCoverAsync(Guid id, [AliasAs("file")] StreamPart cover, CancellationToken ct = default);

// Call site
using var stream = File.OpenRead("cover.jpg");
await client.UploadCoverAsync(bookId, new StreamPart(stream, "cover.jpg", "image/jpeg"));
```

## Interface inheritance

Share endpoints across derived interfaces:

```csharp
public interface IReadOnlyProductsClient
{
    [Get("/api/products/{id}")]
    Task<ProductDto> GetProductAsync(Guid id, CancellationToken ct = default);
}

public interface IProductsClient : IReadOnlyProductsClient
{
    [Post("/api/products")]
    Task<IApiResponse<ProductDto>> CreateProductAsync([Body] CreateProductRequest body, CancellationToken ct = default);
}
```

## Common mistakes

- **Missing path braces**: `[Get("/api/users/id")]` silently ignores the `id` parameter instead of throwing — always use `{id}`.
- **Multiple body parameters**: Refit only supports one `[Body]` per method; use a wrapper object.
- **Null query params**: `null` primitive query params are omitted from the URL automatically — no need to manually filter.
- **Object body without `[Body]`**: a complex type without `[Body]` becomes a query string, not a JSON body.
