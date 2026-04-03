---
name: aspnet-openapi
description: Use Microsoft.AspNetCore.OpenApi (built-in, no Swashbuckle) to add accurate, rich OpenAPI documentation to ASP.NET Core Minimal APIs. Covers setup, TypedResults for auto-inferred response schemas, Results<T1,T2> union types, endpoint metadata (WithSummary/WithDescription/WithTags/WithName), Produces for non-typed handlers, ExcludeFromDescription, and document/operation/schema transformers. Trigger whenever the user writes, reviews, or asks about OpenAPI, Swagger, API docs, endpoint metadata, TypedResults, Results<>, response types, AddOpenApi, MapOpenApi, or API documentation in .NET — even if they don't mention "OpenAPI" by name. Always prefer this skill over guessing, as .NET 9/10 introduced breaking changes to the transformer API.
---

# C# OpenAPI Skill

`Microsoft.AspNetCore.OpenApi` is the built-in package for generating OpenAPI documents in ASP.NET Core. It replaces Swashbuckle for greenfield projects on .NET 9+ and integrates directly with Minimal API endpoint metadata.

## Why this matters

The OpenAPI document is only as accurate as the metadata on your endpoints. Returning `IResult` without concrete type information produces an empty schema; returning `TypedResults.Ok(value)` with a concrete return type gives you a fully-typed schema for free. Getting this right saves hours of hand-maintaining documentation and makes client generation (Refit, NSwag, Kiota) reliable.

## Quick reference

| Topic | See |
|-------|-----|
| Package setup, AddOpenApi, MapOpenApi, Scalar UI | [setup.md](references/setup.md) |
| TypedResults, Results<>, WithSummary/Description/Tags/Name, Produces | [endpoints.md](references/endpoints.md) |
| Document, operation, and schema transformers | [transformers.md](references/transformers.md) |
| Common mistakes and when things silently break | [pitfalls.md](references/pitfalls.md) |

## Minimal examples

**Setup (Program.cs):**
```csharp
builder.Services.AddOpenApi();          // generates /openapi/v1.json

var app = builder.Build();
app.MapOpenApi();                        // exposes the JSON endpoint
```

**Endpoint with full inference (preferred):**
```csharp
// Return type drives the OpenAPI schema — no extra attributes needed
static async Task<Ok<BookDto>> GetBook(Guid id, IDocumentSession session) =>
    TypedResults.Ok(await session.LoadAsync<BookDto>(id));
```

**Multiple response types:**
```csharp
static async Task<Results<Ok<BookDto>, NotFound>> GetBook(Guid id, IDocumentSession session)
{
    var book = await session.LoadAsync<BookDto>(id);
    return book is null ? TypedResults.NotFound() : TypedResults.Ok(book);
}
```

**Endpoint metadata:**
```csharp
group.MapGet("/{id:guid}", GetBook)
    .WithName("GetBook")
    .WithSummary("Get a book by ID")
    .WithDescription("Returns 404 when the book does not exist.")
    .WithTags("Books");
```

## Essential rules at a glance

- **Always use `TypedResults.*`** (not `Results.*`) — concrete return types feed the schema generator automatically.
- **Declare `Results<T1, T2, ...>`** as the handler return type when you return multiple status codes.
- **Prefer `IResult` only** when you must; add `.Produces<T>(200)` to compensate for the lost type information.
- **`AddSchemaTransformer` / `AddOperationTransformer`** run async; the transformer API changed in .NET 9 — see [transformers.md](references/transformers.md).
- **`ExcludeFromDescription()`** hides an endpoint from the generated document entirely.

## What to read next

- First time setting up → [setup.md](references/setup.md)
- Annotating endpoints → [endpoints.md](references/endpoints.md)
- Customising the document → [transformers.md](references/transformers.md)
- Getting unexpected output or empty schemas → [pitfalls.md](references/pitfalls.md)
