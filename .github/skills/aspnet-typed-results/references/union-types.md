# Results<T1, T2, ...> Union Types

When a handler can return more than one status code, use `Results<T1, T2, ...>` as the declared return type. This keeps the compile-time type safety that `TypedResults` provides while expressing multi-outcome behaviour clearly.

## Why not just return `IResult`?

`IResult` works at runtime but erases all type information. The OpenAPI generator sees only `IResult` and cannot infer the response schema — you'd have to add `.Produces<T>()` calls manually. `Results<T1, T2>` carries the type information forward, so the OpenAPI document stays accurate automatically.

## Declaration syntax

```csharp
// Async method with two outcomes
static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
    Guid id, IDocumentSession session)
{
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(product);
}

// Three outcomes
static async Task<Results<Ok<ProductDto>, NotFound, UnauthorizedHttpResult>> GetSecureProduct(
    Guid id, ClaimsPrincipal user, IDocumentSession session)
{
    if (!user.Identity!.IsAuthenticated) return TypedResults.Unauthorized();
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
}
```

The compiler enforces the contract: returning a type not listed in `Results<...>` is a compile error.

## How the implicit cast works

`Results<T1, T2>` implements implicit cast operators for each `T`. That means you can write a bare `TypedResults.Ok(value)` — the compiler converts it to a `Results<Ok<ProductDto>, NotFound>` instance automatically. No explicit cast or wrapping is needed.

## Up to 6 type parameters

The framework ships `Results<T1, T2>` through `Results<T1, T2, T3, T4, T5, T6>`. If you genuinely need more than six outcomes, consider splitting the endpoint or returning a shared base type — that's usually a design signal worth considering.

## Accessing the inner result in tests

`Results<T1, T2>` implements `INestedHttpResult`, which exposes a `.Result` property of type `IResult`. In tests you cast `.Result` to the specific concrete type you expect:

```csharp
var result = await GetProduct(id, session);
// result is Results<Ok<ProductDto>, NotFound>
// result.Result is the actual IResult that was returned

_ = await Assert.That(result.Result).IsAssignableTo<Ok<ProductDto>>();
var ok = (Ok<ProductDto>)result.Result;
_ = await Assert.That(ok.Value!.Id).IsEqualTo(expectedId);
```

## Using with route registration

No special syntax on the route registration side — just map the method:

```csharp
// The declared return type on GetProduct feeds the OpenAPI schema; no Produces<> needed
group.MapGet("/{id:guid}", GetProduct)
     .WithName("GetProduct")
     .WithSummary("Get a product by ID");
```

## Inline lambda caveat

You cannot use a multi-line lambda that returns `Results<T1, T2>` without explicitly specifying the type parameter, because C# cannot infer union types from lambdas. Extract a named static method instead — this also makes the handler unit-testable.

```csharp
// ✅ Named method — works, and is testable
group.MapGet("/{id:guid}", GetProduct);

// ❌ Inline lambda — compiler cannot infer Results<Ok<ProductDto>, NotFound>
group.MapGet("/{id:guid}", async (Guid id, IDocumentSession session) =>
{
    var product = await session.LoadAsync<ProductDto>(id);
    return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
});
```
