# Endpoint Metadata and Response Types

## Why return types matter

`Microsoft.AspNetCore.OpenApi` infers request and response schemas directly from the handler's declared return type. Returning `IResult` loses all type information; returning a concrete `TypedResults.*` subtype gives you a complete schema with no extra attributes.

## TypedResults vs Results

Always prefer `TypedResults.*` over `Results.*`. The concrete types in `Microsoft.AspNetCore.Http.HttpResults` carry the response body type as a generic parameter, which the schema generator can inspect at build time:

```csharp
// ✅ Schema fully inferred — BookDto appears in the OpenAPI document
static async Task<Ok<BookDto>> GetBook(Guid id, IDocumentSession session)
    => TypedResults.Ok(await session.LoadAsync<BookDto>(id));

// ❌ No schema — IResult carries no type information
static async Task<IResult> GetBook(Guid id, IDocumentSession session)
    => Results.Ok(await session.LoadAsync<BookDto>(id));
```

## Multiple response types — Results<T1, T2, ...>

When a handler can return more than one status code, express that as a `Results<>` union type. The generator emits all possible responses automatically:

```csharp
static async Task<Results<Ok<BookDto>, NotFound>> GetBook(
    Guid id, IDocumentSession session)
{
    var book = await session.LoadAsync<BookDto>(id);
    return book is null ? TypedResults.NotFound() : TypedResults.Ok(book);
}
```

Up to eight type parameters are supported. Common combinations:

| Scenario | Return type |
|----------|-------------|
| Exists or not found | `Results<Ok<T>, NotFound>` |
| Created or conflict | `Results<Created<T>, Conflict>` |
| Accepted or bad request | `Results<Accepted, BadRequest<ProblemDetails>>` |
| Rich error with body | `Results<Ok<T>, NotFound, BadRequest<ProblemDetails>>` |

## Common TypedResults factory methods

| Method | Status | Notes |
|--------|--------|-------|
| `TypedResults.Ok(value)` | 200 | Body required |
| `TypedResults.Created(uri, value)` | 201 | Sets `Location` header |
| `TypedResults.Accepted(uri?, value?)` | 202 | — |
| `TypedResults.NoContent()` | 204 | No body |
| `TypedResults.NotFound()` | 404 | No body |
| `TypedResults.NotFound(value)` | 404 | Error body |
| `TypedResults.BadRequest(value)` | 400 | Use `ProblemDetails` as value |
| `TypedResults.UnprocessableEntity(value)` | 422 | Validation errors |
| `TypedResults.Conflict(value?)` | 409 | — |
| `TypedResults.Unauthorized()` | 401 | — |
| `TypedResults.Forbid()` | 403 | — |
| `TypedResults.ValidationProblem(errors)` | 400 | Produces RFC 9457 body |
| `TypedResults.Problem(detail, title, statusCode)` | varies | RFC 9457 |

## Endpoint metadata extension methods

Chain these onto the `RouteHandlerBuilder` returned by `MapGet/Post/Put/Delete`:

```csharp
group.MapGet("/{id:guid}", GetBook)
    .WithName("GetBook")                          // operationId in the spec
    .WithSummary("Get a book by ID")             // short one-liner shown in UI
    .WithDescription("Returns 404 when the book does not exist or has been deleted.")
    .WithTags("Books")                            // groups endpoint in the spec UI
    .WithGroupName("public");                     // routes to named OpenAPI document
```

### WithName

Sets the `operationId`. Use it when client generators (Refit, Kiota) need stable operation names. By convention, use `PascalCase`.

### WithSummary vs WithDescription

- **Summary** — one short sentence shown in collapsed view.
- **Description** — longer Markdown-supported text shown when expanded.

Both accept plain text strings. Descriptions support Markdown in most UIs.

### WithTags

Groups endpoints into sections in the UI. Tags on a `MapGroup` apply to all endpoints in the group — no need to repeat them per-endpoint.

```csharp
app.MapGroup("/books")
    .WithTags("Books")
    .MapBookEndpoints();
```

## Produces — fallback when TypedResults is not used

When you must return `IResult` (e.g. handler returns `IResult` from a mediator/bus invocation), add `.Produces<T>()` to declare the response schema manually:

```csharp
group.MapPost("/", CreateBook)
    .Produces<BookDto>(201)
    .ProducesProblem(400)
    .ProducesProblem(409);
```

`ProducesProblem(statusCode)` is shorthand for `.Produces<ProblemDetails>(statusCode, "application/problem+json")`.

## Accepts — request body content type

For file uploads or non-JSON bodies, declare the expected content type:

```csharp
group.MapPost("/{id:guid}/cover", UploadCover)
    .DisableAntiforgery()
    .Accepts<IFormFile>("multipart/form-data");
```

## Excluding endpoints from the document

```csharp
app.MapGet("/health", HealthCheck)
    .ExcludeFromDescription();
```

Use it for internal endpoints (health checks, metrics, OpenAPI itself) that you don't want consumers to see.

## API versioning integration

With `Asp.Versioning.Http`, assign an endpoint to a version set and the OpenAPI document is automatically filtered per version:

```csharp
var versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .HasApiVersion(new ApiVersion(2))
    .ReportApiVersions()
    .Build();

group.MapGet("/books", GetBooks)
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(1);

group.MapGet("/books", GetBooksV2)
    .WithApiVersionSet(versionSet)
    .MapToApiVersion(2);
```

Register a separate `AddOpenApi` per version if you want one document per version.
