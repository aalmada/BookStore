---
name: aspnet-minimal-apis
description: Structure, organize, and extend ASP.NET Core Minimal APIs using route groups, parameter binding, endpoint metadata, and filters — without controllers. Covers MapGroup with RouteGroupBuilder extension methods, all binding sources ([FromBody], [FromServices], [FromRoute], [AsParameters]), endpoint conventions (WithName, WithSummary, WithTags, RequireAuthorization, ExcludeFromDescription), endpoint filters, and the static-class/named-method organization pattern. Trigger whenever the user writes, reviews, or asks about Minimal API routing, route groups, parameter binding, endpoint registration, MapGet/MapPost/MapPut/MapDelete, IEndpointRouteBuilder, RouteGroupBuilder, [AsParameters], DI in handlers, or organizing Minimal API endpoints — even if they don't mention these terms by name. Always prefer this skill over guessing; the binding and group APIs have subtle rules that are easy to get wrong.
---

# ASP.NET Core Minimal APIs Skill

Minimal APIs let you define HTTP endpoints directly in C# without controllers, using a functional style that's concise, composable, and easy to test. The key to keeping them maintainable at scale is to apply a consistent organization pattern from the start.

## Why this matters

The default inline-lambda style works for small APIs but quickly becomes unwieldy. Route groups, named handler methods, and `RouteGroupBuilder` extension methods give you the structure of controllers without the ceremony — and they're the prerequisite for `TypedResults` to work properly (see [aspnet-typed-results](../aspnet-typed-results/SKILL.md) for response types) and for OpenAPI to self-document (see [aspnet-openapi](../aspnet-openapi/SKILL.md)).

## Quick reference

| Topic | See |
|-------|-----|
| Route groups, `MapGroup`, `RouteGroupBuilder` extension methods, nesting | [route-groups.md](references/route-groups.md) |
| Binding: route, query, body, DI, headers, `[AsParameters]`, special types | [parameter-binding.md](references/parameter-binding.md) |
| `WithName`, `WithSummary`, `WithTags`, `RequireAuthorization`, `ExcludeFromDescription`, `Accepts` | [endpoint-metadata.md](references/endpoint-metadata.md) |
| `IEndpointFilter`, filter factories, filter ordering | [filters.md](references/filters.md) |
| Common mistakes | [pitfalls.md](references/pitfalls.md) |

## The standard organization pattern

Define a `static` class per feature area. Expose a single extension method on `RouteGroupBuilder` that registers all routes. Keep handler methods as `private static` or `internal static` named methods below the registration method.

```csharp
public static class ProductEndpoints
{
    // Called from Program.cs or a central mapping extension
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetProducts)
            .WithName("GetProducts")
            .WithSummary("List all products");

        _ = group.MapGet("/{id:guid}", GetProduct)
            .WithName("GetProduct")
            .WithSummary("Get a product by ID");

        _ = group.MapPost("/", CreateProduct)
            .WithName("CreateProduct")
            .WithSummary("Create a new product")
            .RequireAuthorization();

        return group;
    }

    // Handler methods: named, static, directly callable in unit tests
    static async Task<Ok<ProductDto[]>> GetProducts(
        [FromServices] IDocumentSession session,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await session.Query<ProductDto>().ToArrayAsync(cancellationToken));

    static async Task<Results<Ok<ProductDto>, NotFound>> GetProduct(
        Guid id,
        [FromServices] IDocumentSession session,
        CancellationToken cancellationToken)
    {
        var product = await session.LoadAsync<ProductDto>(id, cancellationToken);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    static async Task<Created<ProductDto>> CreateProduct(
        [FromBody] CreateProductRequest request,
        [FromServices] IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var product = await bus.InvokeAsync<ProductDto>(new CreateProductCommand(request), cancellationToken);
        return TypedResults.Created($"/api/products/{product.Id}", product);
    }
}
```

**Register centrally in Program.cs:**
```csharp
app.MapGroup("/api/products")
   .WithTags("Products")
   .MapProductEndpoints();
```

## Essential rules at a glance

- **Extract handler methods** — inline lambdas can't return `Results<T1,T2>` and can't be unit-tested directly.
- **Group at the call site** — prefix, tags, auth, and API versioning belong on the outer group, not inside the endpoint class.
- **`[FromServices]` for DI** — service parameters are not injected automatically unless marked; use `[FromServices]` (or `FromKeyedServices`) to be explicit.
- **`[AsParameters]`** — wraps multiple query/route/header params into a single record for clean large parameter lists.
- **Route constraints** — `{id:guid}`, `{page:int:min(1)}` prevent bad input from reaching the handler.
- **`CancellationToken`** — always accept it in async handlers; it's bound automatically from the request without any attribute.

## What to read next

- Organising multiple groups and nesting → [route-groups.md](references/route-groups.md)
- Binding from query strings, headers, forms, or custom types → [parameter-binding.md](references/parameter-binding.md)
- Applying auth, rate limiting, metadata → [endpoint-metadata.md](references/endpoint-metadata.md)
- Cross-cutting logic without middleware → [filters.md](references/filters.md)
- Unexpected 404s, binding failures, or wrong DI injection → [pitfalls.md](references/pitfalls.md)
