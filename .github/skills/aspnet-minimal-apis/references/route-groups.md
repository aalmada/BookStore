# Route Groups

## `MapGroup()` basics

`MapGroup(prefix)` creates a `RouteGroupBuilder`. Every endpoint registered on the group inherits the prefix, applied metadata, and any registered filters.

```csharp
// All routes start with /api/products
app.MapGroup("/api/products")
   .WithTags("Products")
   .MapProductEndpoints();
```

The return value is the same `RouteGroupBuilder` — chain metadata onto the group before calling the extension method, or do it after; both work.

## `RouteGroupBuilder` extension method pattern

The recommended approach is one `static` class per feature, one extension method on `RouteGroupBuilder`:

```csharp
public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/", GetOrders).WithName("GetOrders").WithSummary("List orders");
        _ = group.MapGet("/{id:guid}", GetOrder).WithName("GetOrder");
        _ = group.MapPost("/", PlaceOrder).WithName("PlaceOrder");
        _ = group.MapDelete("/{id:guid}", CancelOrder).WithName("CancelOrder");
        return group;
    }

    static async Task<Ok<OrderDto[]>> GetOrders(...) { ... }
    static async Task<Results<Ok<OrderDto>, NotFound>> GetOrder(Guid id, ...) { ... }
    static async Task<Created<OrderDto>> PlaceOrder([FromBody] PlaceOrderRequest req, ...) { ... }
    static async Task<NoContent> CancelOrder(Guid id, ...) { ... }
}
```

Returning `group` from the extension method lets the call site chain additional metadata:

```csharp
app.MapGroup("/api/orders")
   .WithTags("Orders")
   .RequireAuthorization()           // applied to all endpoints in the group
   .MapOrderEndpoints();
```

## `IEndpointRouteBuilder` vs `RouteGroupBuilder`

Use `IEndpointRouteBuilder` when registering from `WebApplication` or mapping across multiple groups. Use `RouteGroupBuilder` when writing a feature area's extension method.

```csharp
// Registration hub — receives IEndpointRouteBuilder (WebApplication implements it)
public static class EndpointMappingExtensions
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithApiVersionSet(versionSet);

        api.MapGroup("/products")
           .WithTags("Products")
           .MapProductEndpoints();

        api.MapGroup("/orders")
           .WithTags("Orders")
           .RequireAuthorization()
           .MapOrderEndpoints();

        return app;
    }
}
```

In `Program.cs`:
```csharp
app.MapAllEndpoints();
```

## Nesting groups

Groups can be nested. The inner group prefix appends to the outer prefix, and the inner group inherits the outer group's filters and metadata.

```csharp
var admin = api.MapGroup("/admin")
               .RequireAuthorization("Admin")
               .WithTags("Admin");

admin.MapGroup("/products").MapAdminProductEndpoints();
admin.MapGroup("/orders").MapAdminOrderEndpoints();
```

Inner extension methods receive a `RouteGroupBuilder` already configured with `/admin/products` prefix and `Admin` auth policy.

## Route patterns and constraints

```
{id}            any string (bound as string parameter)
{id:guid}       Guid.TryParse must succeed (400 otherwise)
{id:int}        int.TryParse
{id:int:min(1)} int >= 1
{name:alpha}    letters only
{*path}         catch-all (greedy)
```

Constraints prevent invalid input from reaching your handler:
```csharp
group.MapGet("/{id:guid}", GetProduct)  // id is already a valid Guid when handler runs
```

## Named handler methods vs. inline lambdas

Prefer named `static` methods:
- `Results<T1,T2>` union return types only work on named methods (compiler limitation)
- Named methods are directly invokable in unit tests without HTTP infrastructure
- Stack traces are readable

Inline lambda pattern to avoid for complex handlers:
```csharp
// Avoid for non-trivial logic
group.MapGet("/", async ([FromServices] IService svc) => await svc.GetAllAsync());
```
