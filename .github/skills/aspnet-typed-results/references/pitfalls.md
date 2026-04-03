# Common Pitfalls with TypedResults

## 1. Using `Results.*` instead of `TypedResults.*`

The two classes look identical in usage but `Results.*` methods return `IResult`, not a concrete type. The compiler infers `IResult` as the handler's return type, OpenAPI sees nothing, and tests cannot do type assertions without casting.

```csharp
// ❌ Returns IResult — OpenAPI schema is empty, not testable by type
app.MapGet("/products/{id}", (Guid id) => Results.Ok(new ProductDto(id)));

// ✅ Returns Ok<ProductDto> — schema inferred, testable
static Ok<ProductDto> GetProduct(Guid id) => TypedResults.Ok(new ProductDto(id));
app.MapGet("/products/{id}", GetProduct);
```

## 2. Returning `IResult` from a handler that uses `TypedResults`

Even if you call `TypedResults.Ok(...)` inside the method body, if the declared return type is `IResult` the framework sees only the interface. The concrete type must appear in the *signature*, not just inside the body.

```csharp
// ❌ Declared return type is IResult — type information is lost
static async Task<IResult> GetProduct(Guid id, IDocumentSession session) =>
    TypedResults.Ok(await session.LoadAsync<ProductDto>(id));

// ✅ Declared return type is Ok<ProductDto>
static async Task<Ok<ProductDto>> GetProduct(Guid id, IDocumentSession session) =>
    TypedResults.Ok(await session.LoadAsync<ProductDto>(id));
```

## 3. Using inline lambdas with multiple return types

Lambdas that return `Results<T1, T2>` require the compiler to infer the union type, which C# cannot do in a multi-statement lambda. This causes a compilation error or forces you to add an explicit cast everywhere.

```csharp
// ❌ Compiler cannot infer Results<Ok<ProductDto>, NotFound> from a lambda
app.MapGet("/products/{id}", async (Guid id, IDocumentSession session) =>
{
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
});

// ✅ Extract a named method — compiler unambiguously knows the return type
static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
    Guid id, IDocumentSession session)
{
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
}
app.MapGet("/products/{id}", GetProduct);
```

## 4. Forgetting `Microsoft.AspNetCore.Http.HttpResults` in test files

The concrete types (`Ok<T>`, `NotFound`, `BadRequest`, etc.) live in `Microsoft.AspNetCore.Http.HttpResults`. Without this using directive, type assertions fail to compile.

```csharp
using Microsoft.AspNetCore.Http.HttpResults; // Required for Ok<T>, NotFound, etc.
```

## 5. Accessing `.Result` on a non-union return type

When a handler returns a single concrete type (e.g. `Task<Ok<ProductDto>>`), the result directly IS the `Ok<ProductDto>`. Attempting to access `.Result` will fail because `Ok<T>` does not implement `INestedHttpResult`.

```csharp
// Handler: Task<Ok<ProductDto>>
var result = await handler.GetProduct(id, session);

// ❌ Ok<ProductDto> has no .Result property
var inner = result.Result; // compile error

// ✅ result IS the Ok<ProductDto>
_ = await Assert.That(result).IsAssignableTo<Ok<ProductDto>>();
_ = await Assert.That(result.Value!.Id).IsEqualTo(expectedId);
```

Only `Results<T1, T2>` (which implements `INestedHttpResult`) has a `.Result` property.

## 6. Using `.Produces<T>()` when it's already covered by the return type

When the declared return type provides complete type information (either a single concrete type or `Results<T1, T2>`), adding `.Produces<T>()` is redundant. It adds noise and can create duplicate entries in the OpenAPI document.

```csharp
// ❌ Redundant — Ok<ProductDto> already tells OpenAPI everything
group.MapGet("/{id}", GetProduct)
     .Produces<ProductDto>(200)     // not needed
     .Produces(404);                // not needed

// ✅ Clean — return type drives the schema
group.MapGet("/{id}", GetProduct)
     .WithName("GetProduct");
```

Reserve `.Produces<T>()` for handlers that return `IResult` (where the type is erased) and cannot be changed.

## 7. Confusing `StatusCode` with a typed result

`TypedResults.StatusCode(int)` returns `StatusCodeHttpResult`, which opens the OpenAPI schema (it can't infer a body type). Use it only for truly dynamic status codes. For known codes, pick the matching typed method.

## 8. `ServerSentEvents` is .NET 9+

`TypedResults.ServerSentEvents(...)` was introduced in .NET 9. If your target framework is .NET 8 or earlier, use a manual `IResult` implementation instead.
