# Common Pitfalls

## 1. Inline lambda with `Results<T1,T2>` union return type

**Problem:** The C# compiler cannot infer the union return type for inline lambdas.

```csharp
// Fails to compile — compiler can't resolve Results<Ok<ProductDto>, NotFound>
group.MapGet("/{id:guid}", async ([FromServices] IDocumentSession session, Guid id) =>
{
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
});
```

**Fix:** Extract to a named `static` method with an explicit return type.

```csharp
group.MapGet("/{id:guid}", GetProduct).WithName("GetProduct");

static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
    Guid id,
    [FromServices] IDocumentSession session,
    CancellationToken ct)
{
    var product = await session.LoadAsync<ProductDto>(id, ct);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
}
```

## 2. Complex type bound from query string instead of body

**Problem:** A `record` or `class` with many properties intended as a JSON body is accidentally bound from query string when not decorated.

```csharp
// If the Content-Type is not application/json, or if the type is too simple,
// binding may fail or produce nulls
static Task<Created<ProductDto>> CreateProduct(CreateProductRequest request, ...)
```

**Fix:** Be explicit with `[FromBody]` when you want JSON body binding, to prevent ambiguity.

```csharp
static Task<Created<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, ...)
```

## 3. DI service injected without `[FromServices]`

**Problem:** Implicit DI binding works only for types registered in the container. If a type is not registered, it becomes a query string parameter and the binding silently fails (null or 400).

```csharp
// IProductService may be treated as a query param if implicit binding guesses wrong
static Task<Ok<ProductDto[]>> GetProducts(IProductService service, ...)
```

**Fix:** Use `[FromServices]` on every DI parameter.

```csharp
static Task<Ok<ProductDto[]>> GetProducts([FromServices] IProductService service, ...)
```

## 4. `WithTags` inside the feature's extension method

**Problem:** Tags are applied inside the feature class, making them tightly coupled and hard to override.

```csharp
// Inside ProductEndpoints — don't do this
public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
{
    group.MapGet("/", GetProducts).WithTags("Products");  // here
    return group;
}
```

**Fix:** Apply `WithTags` at the call site (in `Program.cs` or the hub mapping extension).

```csharp
app.MapGroup("/api/products")
   .WithTags("Products")           // here — single place to change
   .MapProductEndpoints();
```

## 5. Route group prefix duplication

**Problem:** Prefix is specified both in `MapGroup` and inside the feature's handler routes.

```csharp
app.MapGroup("/api/products").MapProductEndpoints();

// Inside the extension method
group.MapGet("/api/products/{id}", GetProduct);  // results in /api/products/api/products/{id}
```

**Fix:** Relative paths only inside the extension method.

```csharp
group.MapGet("/{id:guid}", GetProduct);
```

## 6. Missing `WithName` causes `Results.Created` to throw

**Problem:** `TypedResults.CreatedAtRoute("GetProduct", ...)` requires the route to be named. If `WithName` is omitted, a `RouteNotFoundException` is thrown at runtime.

**Fix:** Always add `WithName` to any GET endpoint used as a `CreatedAtRoute` target.

```csharp
group.MapGet("/{id:guid}", GetProduct)
     .WithName("GetProduct");   // required

// Then in POST handler:
return TypedResults.CreatedAtRoute(product, "GetProduct", new { id = product.Id });
```

## 7. Auth group with an unauthenticated endpoint — forgetting `AllowAnonymous`

**Problem:** A group has `RequireAuthorization()`, but one endpoint should be public. It returns 401.

**Fix:** Explicitly opt out with `.AllowAnonymous()` on the specific endpoint.

```csharp
app.MapGroup("/api/products")
   .RequireAuthorization()
   .MapProductEndpoints();

// Inside MapProductEndpoints:
group.MapGet("/featured", GetFeatured)   // 401 without AllowAnonymous
     .AllowAnonymous();                   // override group auth
```

## 8. `[AsParameters]` record with body property

**Problem:** Mixing `[FromBody]` inside an `[AsParameters]` record with query/route properties can confuse the binder. There can only be one body per request.

```csharp
// Risky — only one [FromBody] is allowed; mixing body and query in one record is unusual
public record CreateRequest(
    [FromQuery] string TenantId,
    [FromBody] CreateProductBody Body);   // body in [AsParameters] record
```

**Fix:** Keep `[AsParameters]` records for query/route/header params only. Accept the body as a separate `[FromBody]` parameter.

```csharp
static Task<Created<ProductDto>> CreateProduct(
    [AsParameters] TenantContext tenant,   // query/header params
    [FromBody] CreateProductBody body,     // body separately
    ...)
```

## 9. Form file upload without `DisableAntiforgery`

**Problem:** File upload endpoints return 400/antiforgery validation error even with correct multipart form.

**Fix:** Call `.DisableAntiforgery()` on file upload endpoints (antiforgery tokens are browser-only; APIs use token auth).

```csharp
group.MapPost("/upload", UploadFile)
     .Accepts<IFormFile>("multipart/form-data")
     .DisableAntiforgery();
```

## 10. Forgetting `CancellationToken` in async handlers

**Problem:** Long-running database or I/O operations continue running after the client disconnects.

**Fix:** Always accept `CancellationToken ct` and pass it to all async calls.

```csharp
static async Task<Ok<ProductDto[]>> GetProducts(
    [FromServices] IDocumentSession session,
    CancellationToken ct)   // bound automatically from HttpContext.RequestAborted
=>
    TypedResults.Ok(await session.Query<ProductDto>().ToArrayAsync(ct));
```
