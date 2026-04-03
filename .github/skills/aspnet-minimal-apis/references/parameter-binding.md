# Parameter Binding

## The seven binding sources

| Source | Attribute | Implicit? | Notes |
|--------|-----------|-----------|-------|
| Route segment | `[FromRoute]` | ✅ by name | `{id:guid}` in route template → `Guid id` param |
| Query string | `[FromQuery]` | ✅ by name | `?page=2` → `int page` param |
| JSON body | `[FromBody]` | ✅ if complex type | One body per request; must be `Content-Type: application/json` |
| Form field | `[FromForm]` | ❌ must be explicit | For `multipart/form-data` and `application/x-www-form-urlencoded` |
| Header | `[FromHeader]` | ❌ must be explicit | Header name can differ from param name using `Name` property |
| DI container | `[FromServices]` | ⚠️ see below | See "DI injection" section |
| Keyed DI | `[FromKeyedServices("key")]` | ❌ | For keyed service registrations |

> **DI implicit binding:** .NET 7+ tries DI for any parameter that is *registered in the DI container*, but this is ambiguous and error-prone. Use `[FromServices]` explicitly to make it clear.

## Implicit binding examples

```csharp
// Route: GET /products/{id:guid}?includeDeleted=true
static async Task<Ok<ProductDto>> GetProduct(
    Guid id,                     // <- [FromRoute] implied by matching route param name
    bool includeDeleted,         // <- [FromQuery] implied (simple type, not in route)
    [FromServices] IDocumentSession session,  // <- DI: explicit
    CancellationToken ct)        // <- special type, always implicit
```

The compiler/runtime resolves:
- `id` → route (matches route template `{id}`)
- `includeDeleted` → query string (not in route, not a complex type)
- `session` → DI
- `ct` → special type

## Explicit attribute binding

Use explicit attributes when the parameter name differs from the route/query key, or when the source is ambiguous:

```csharp
static Task<Ok<SearchResult[]>> Search(
    [FromQuery(Name = "q")] string searchTerm,         // ?q=...
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromHeader(Name = "X-Tenant-ID")] string tenantId,
    [FromServices] ISearchService search,
    CancellationToken ct)
```

## `[AsParameters]` — grouping parameters

When a handler has many query/route/header params (e.g. search, pagination, filtering), bundle them into a `record` and annotate with `[AsParameters]`. Each property is individually bound from the correct source.

```csharp
// The record — no attributes needed on the record itself
public record SearchRequest(
    [FromQuery(Name = "q")] string Query,
    [FromQuery] int Page = 1,
    [FromQuery] int PageSize = 20,
    [FromQuery] string? SortBy = null,
    [FromQuery] bool Descending = false);

// Handler becomes clean
static async Task<Ok<PagedList<ProductDto>>> SearchProducts(
    [AsParameters] SearchRequest request,
    [FromServices] IProductService service,
    CancellationToken ct)
```

`[AsParameters]` supports `record`, `class`, or `struct` with a parameterized constructor or settable properties.

## Special types — always implicit, never explicit

These types are bound directly from the HTTP context without any attribute:

| Type | What it provides |
|------|-----------------|
| `HttpContext` | Full request/response context |
| `HttpRequest` | Request only |
| `HttpResponse` | Response only |
| `CancellationToken` | Request abort token — always pass to async calls |
| `ClaimsPrincipal` | Authenticated user (`HttpContext.User`) |
| `IFormFileCollection` | All uploaded form files |

```csharp
static async Task<Ok<string>> Upload(
    IFormFile file,               // single uploaded file
    ClaimsPrincipal user,         // authenticated user
    CancellationToken ct)         // request cancellation
```

## Complex body binding

Any complex (non-primitive) type that isn't a service is assumed to be `[FromBody]` when JSON:

```csharp
// CreateProductRequest is deserialized from JSON body automatically
static async Task<Created<ProductDto>> CreateProduct(
    CreateProductRequest request,         // implicit [FromBody]
    [FromServices] IMessageBus bus,
    CancellationToken ct)
```

To accept form data instead of JSON:
```csharp
group.MapPost("/upload", UploadFile)
     .Accepts<UploadRequest>("multipart/form-data")
     .DisableAntiforgery();

static async Task<Ok> UploadFile(
    [FromForm] string name,
    IFormFile file,
    CancellationToken ct)
```

## Custom binding

### `TryParse` — for route, query, and header values

If your type implements `static bool TryParse(string s, out T result)`, it's automatically used for route/query/header binding:

```csharp
public readonly record struct ProductCode
{
    public string Value { get; }
    private ProductCode(string v) => Value = v;

    public static bool TryParse(string s, out ProductCode result)
    {
        result = new ProductCode(s);
        return s.StartsWith("SKU-");  // validation logic
    }
}

// Now usable directly in handler
static Task<Ok<ProductDto>> GetByCode(ProductCode code, ...)
```

### `BindAsync` — for complex types needing the full request

```csharp
public class PaginationOptions
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;

    public static ValueTask<PaginationOptions?> BindAsync(HttpContext ctx)
    {
        var page = int.TryParse(ctx.Request.Query["page"], out var p) ? p : 1;
        var size = int.TryParse(ctx.Request.Query["pageSize"], out var s) ? s : 20;
        return ValueTask.FromResult<PaginationOptions?>(new() { Page = page, PageSize = size });
    }
}
```

## Optional parameters and defaults

```csharp
// Query params with defaults — these are optional in the URL
static Task<Ok<ProductDto[]>> GetProducts(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? category = null,   // nullable = optional
    ...)
```

Route parameters are always required (they're part of the URL template). Query and header params are optional when they have a default value or are nullable.
