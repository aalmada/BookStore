# Common Pitfalls

## 1. Returning `IResult` produces an empty schema

The single most common mistake. `IResult` carries no type information, so the schema generator emits `{}`.

```csharp
// ❌ Produces empty schema in the document
static async Task<IResult> GetBook(Guid id, IDocumentSession session)
    => Results.Ok(await session.LoadAsync<BookDto>(id));

// ✅ Schema inferred from Ok<BookDto>
static async Task<Ok<BookDto>> GetBook(Guid id, IDocumentSession session)
    => TypedResults.Ok(await session.LoadAsync<BookDto>(id));
```

When you cannot change the return type (e.g. a handler dispatched through a bus), compensate with `.Produces<T>()`:

```csharp
group.MapPost("/", CreateBook)
    .Produces<BookDto>(201)
    .ProducesProblem(400);
```

## 2. Using `Results.*` instead of `TypedResults.*`

`Results.Ok(value)` returns `IResult`; `TypedResults.Ok(value)` returns `Ok<T>`. The names look similar but the types are very different for schema inference.

## 3. Forgetting to declare all status codes on multi-response handlers

Declare `Results<T1, T2>` as the return type — not just `IResult`. If you only declare `IResult`, none of the branches appear in the document.

```csharp
// ❌ Only one schema entry — the 404 is invisible
static async Task<IResult> GetBook(Guid id, ...) { ... }

// ✅ Both 200 and 404 appear in the document
static async Task<Results<Ok<BookDto>, NotFound>> GetBook(Guid id, ...) { ... }
```

## 4. Transformer API changes between .NET 8 and .NET 9

In .NET 8, transformers took `IOpenApiDocumentTransformer` from `Microsoft.AspNetCore.OpenApi`. In .NET 9+, the namespace and method signatures changed. If you copy examples from the internet, verify they target your SDK version. Signs you have the wrong version:

- `options.UseApiExplorer()` — this is Swashbuckle, not `Microsoft.AspNetCore.OpenApi`
- `IOperationFilter` / `ISchemaFilter` — Swashbuckle interfaces, not built-in
- `AddSwaggerGen` / `UseSwagger` — Swashbuckle, not the built-in package

## 5. `MapOpenApi()` called unconditionally in production

`/openapi/v1.json` exposes your full API surface. Wrap it in `IsDevelopment()` or protect it with `RequireAuthorization()`.

```csharp
// ❌ Exposed in production
app.MapOpenApi();

// ✅ Development only
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ✅ Production with auth
app.MapOpenApi().RequireAuthorization("Admin");
```

## 6. WithTags on a group not applying to all endpoints

`.WithTags()` on a `RouteGroupBuilder` applies to all endpoints registered via that builder *at the time the builder is used*. If endpoints are added through extension methods, ensure the extension method is called on the same builder that has `WithTags`:

```csharp
// ✅ Tag applies to all endpoints in the group
app.MapGroup("/books")
    .WithTags("Books")
    .MapBookEndpoints();   // inside MapBookEndpoints, group already has the tag

// ❌ Tag may not apply if MapBookEndpoints returns a different builder
```

## 7. Missing schemas for polymorphic types

The built-in generator does not automatically handle polymorphic (derived) types. If you return an abstract base type or an interface, the schema will be empty. Options:

- Return the concrete type in `TypedResults.Ok(concreteValue)`
- Use a schema transformer to add a `discriminator` and enumerate subtypes manually
- Consider Swashbuckle for complex polymorphic APIs

## 8. Accepts<T> not affecting the request body schema

`.Accepts<T>("application/json")` sets the content type annotation but does **not** replace the automatically inferred body schema from the method parameter. Use it only for non-JSON types (multipart, form data) where automatic inference doesn't work.

## 9. `GetOrCreateSchemaAsync` only available in .NET 10+

If you're on .NET 9, `context.GetOrCreateSchemaAsync` does not exist. Construct `OpenApiSchema` objects manually or upgrade to .NET 10.

## 10. Multiple `AddOpenApi` calls required for multiple documents

One `AddOpenApi()` call produces one document. For public + internal documents, call it twice with different names:

```csharp
builder.Services.AddOpenApi("public");
builder.Services.AddOpenApi("internal");
```

Each call accepts its own options lambda, so you can apply different transformers per document.
