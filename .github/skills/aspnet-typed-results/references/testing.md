# Unit Testing Handlers with TypedResults

The primary reason to extract handler logic into named static methods (rather than inline lambdas) is testability. Because `TypedResults.*` returns concrete types, you can call the handler directly and inspect the result with type assertions — no `TestServer`, `WebApplicationFactory`, or mocked `HttpContext` required.

## The pattern: extract static handler methods

```csharp
// Production code — static method, easy to call from a test
public static class ProductHandlers
{
    public static async Task<Ok<ProductDto[]>> GetProducts(IDocumentSession session)
    {
        var products = await session.Query<ProductDto>().ToListAsync();
        return TypedResults.Ok(products.ToArray());
    }

    public static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
        Guid id, IDocumentSession session)
    {
        var product = await session.LoadAsync<ProductDto>(id);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }
}
```

## Asserting a single return type

When the method declares a single return type (e.g. `Task<Ok<ProductDto[]>>`), the result *is* the concrete type — no `.Result` unwrapping needed:

```csharp
[Test]
public async Task GetProducts_ReturnsOk()
{
    // Arrange: set up dependencies (in-memory, substitute, etc.)
    var session = /* ... */;

    // Act: call the handler directly
    var result = await ProductHandlers.GetProducts(session);

    // Assert: result IS an Ok<ProductDto[]>
    _ = await Assert.That(result).IsAssignableTo<Ok<ProductDto[]>>();
    _ = await Assert.That(result.Value).IsNotNull();
}
```

## Asserting a union return type

When the method declares `Results<T1, T2>`, you must access `.Result` to reach the inner value:

```csharp
[Test]
public async Task GetProduct_WhenFound_ReturnsOk()
{
    var result = await ProductHandlers.GetProduct(existingId, session);

    // Check the inner result type
    _ = await Assert.That(result.Result).IsAssignableTo<Ok<ProductDto>>();

    // Cast to access the value
    var ok = (Ok<ProductDto>)result.Result;
    _ = await Assert.That(ok.Value!.Id).IsEqualTo(existingId);
}

[Test]
public async Task GetProduct_WhenMissing_ReturnsNotFound()
{
    var result = await ProductHandlers.GetProduct(Guid.CreateVersion7(), session);
    _ = await Assert.That(result.Result).IsAssignableTo<NotFound>();
}
```

## Testing via the runtime interfaces

When you just want to verify the HTTP status code without importing the concrete type, use `IStatusCodeHttpResult`. This is useful for error-path tests where the exact body type is less important than the status:

```csharp
[Test]
public async Task CreateProduct_WithInvalidInput_ReturnsBadRequest()
{
    var result = await ProductHandlers.Create(invalidCommand, session);

    _ = await Assert.That(result).IsAssignableTo<IStatusCodeHttpResult>();
    var statusResult = (IStatusCodeHttpResult)result;
    _ = await Assert.That(statusResult.StatusCode).IsEqualTo(400);
}
```

Similarly, `IValueHttpResult<T>` exposes `.Value` when you want the body without committing to the exact `Ok<T>` vs `Created<T>` distinction.

## Namespaces to import in test files

```csharp
using Microsoft.AspNetCore.Http;               // IStatusCodeHttpResult, IValueHttpResult<T>
using Microsoft.AspNetCore.Http.HttpResults;   // Ok<T>, NotFound, BadRequest, etc.
```

## What to mock / not mock

Unit tests for typed-result handlers typically need:
- A lightweight in-memory or substituted repository / session
- Any options objects (e.g. `IOptions<T>`) configured directly with the desired values
- No `HttpContext` — `TypedResults` factory methods do not touch the context at construction time; they defer to `IResult.ExecuteAsync` which you never call in a unit test

Integration tests that exercise the full HTTP pipeline (routing, middleware, serialisation) should use `WebApplicationFactory` — but those are a separate concern from unit testing the handler logic.
