---
name: aspnet-typed-results
description: Use TypedResults (not Results) to write ASP.NET Core Minimal API handlers with strongly-typed return types that self-document for OpenAPI, enable unit testing without HTTP infrastructure, and give compile-time safety through Results<T1,T2> union types. Trigger whenever the user writes, reviews, or asks about Minimal API handlers, TypedResults, Results, IResult, Ok<T>, NotFound, Results<T1,T2>, response types, handler return types, unit testing handlers, or API endpoint return values in ASP.NET Core — even if they don't mention "TypedResults" by name. Always prefer this skill over guessing, as TypedResults is the idiomatic choice for all modern ASP.NET Core Minimal API work.
---

# ASP.NET Core TypedResults Skill

`TypedResults` is the strongly-typed counterpart to `Results` in ASP.NET Core Minimal APIs. Its return types carry type information at compile time, which unlocks three concrete benefits that `Results` cannot provide.

## Why TypedResults — the three wins

**1. OpenAPI for free.** When a handler returns `TypedResults.Ok(value)` with a concrete declared return type, the framework reads that type information at build time and generates the OpenAPI response schema automatically. With `Results`, you must add `.Produces<T>()` manually and keep it in sync.

**2. Unit-testable without HTTP plumbing.** Because each `TypedResults.*` method returns a concrete type (e.g. `Ok<ProductDto>`, `NotFound`), you can call a handler method directly in a test and inspect the result with a type assertion — no mocked `HttpContext` needed.

**3. Compile-time response contract.** `Results<T1, T2, ...>` union types make the compiler enforce that only the declared response types can be returned. Adding an unlisted type is a compile error, not a runtime surprise.

## Quick reference

| Topic | See |
|-------|-----|
| All built-in `TypedResults.*` methods and their HTTP status codes | [built-in-types.md](references/built-in-types.md) |
| `Results<T1, T2, ...>` union types — declaration, implicit cast, testing | [union-types.md](references/union-types.md) |
| Unit testing handlers by type assertion | [testing.md](references/testing.md) |
| Common mistakes and when things silently break | [pitfalls.md](references/pitfalls.md) |

## Minimal examples

**Single return type — simplest form:**
```csharp
using Microsoft.AspNetCore.Http.HttpResults;

// Declare the concrete return type; TypedResults.Ok infers the generic parameter
static async Task<Ok<ProductDto>> GetProduct(Guid id, IDocumentSession session) =>
    TypedResults.Ok(await session.LoadAsync<ProductDto>(id));
```

**Multiple return types:**
```csharp
static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
    Guid id, IDocumentSession session)
{
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(product);
}
```

**Unit test (TUnit style):**
```csharp
[Test]
public async Task GetProduct_WhenNotFound_ReturnsNotFound()
{
    var result = await ProductHandlers.GetProduct(Guid.CreateVersion7(), session);
    _ = await Assert.That(result.Result).IsAssignableTo<NotFound>();
}
```

**Register with metadata:**
```csharp
group.MapGet("/{id:guid}", GetProduct)
    .WithName("GetProduct")
    .WithSummary("Get a product by ID");
// No .Produces<ProductDto>() needed — the declared return type covers it
```

## Essential rules at a glance

- **Use `TypedResults.*`**, never `Results.*` — the concrete return type is the contract; `Results` erases it.
- **Always extract handler methods** from lambdas — you cannot call an inline lambda in a unit test.
- **Declare the full return type** on the method signature — the framework reads it at build time, not runtime.
- **Use `Results<T1, T2>`** whenever a handler can return more than one status code; `IResult` is a fallback that loses all type benefits.
- **Import `Microsoft.AspNetCore.Http.HttpResults`** for the concrete type names (`Ok<T>`, `NotFound`, etc.) in test files.

## What to read next

- First encounter with TypedResults → start here and read the examples above
- Need to return multiple status codes → [union-types.md](references/union-types.md)
- Writing unit tests → [testing.md](references/testing.md)
- Reference for all available result methods → [built-in-types.md](references/built-in-types.md)
- Seeing empty OpenAPI schemas or unexpected behaviour → [pitfalls.md](references/pitfalls.md)
