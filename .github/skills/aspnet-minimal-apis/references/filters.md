# Endpoint Filters

Endpoint filters are the Minimal API equivalent of action filters in MVC. They run before and after the endpoint handler, can inspect or modify parameters and results, and compose as a pipeline.

## `IEndpointFilter` interface

```csharp
public class LoggingFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Console.WriteLine($"Before: {context.HttpContext.Request.Path}");
        var result = await next(context);
        Console.WriteLine($"After: {result}");
        return result;
    }
}
```

`EndpointFilterInvocationContext` gives you:
- `HttpContext` — full request/response context
- `Arguments` — the bound handler parameters as `IList<object?>`
- `GetArgument<T>(index)` — typed access to a handler argument by position

## Adding filters to endpoints and groups

```csharp
// Single endpoint
group.MapPost("/", CreateProduct)
     .AddEndpointFilter<ValidationFilter>();

// Group — all endpoints in the group get the filter
app.MapGroup("/api")
   .AddEndpointFilter<RequestLoggingFilter>()
   .MapAllEndpoints();

// Inline with a lambda
group.MapPost("/", CreateProduct)
     .AddEndpointFilter(async (ctx, next) =>
     {
         if (!ctx.HttpContext.User.Identity?.IsAuthenticated ?? true)
             return Results.Unauthorized();
         return await next(ctx);
     });
```

## Filter ordering

Filters execute in the **order they are added**, outermost first (like middleware):

```csharp
group.MapPost("/", CreateProduct)
     .AddEndpointFilter<TracingFilter>()    // runs 1st (outer)
     .AddEndpointFilter<ValidationFilter>() // runs 2nd (inner)
     .AddEndpointFilter<AuditFilter>();     // runs 3rd (innermost before handler)
```

Group-level filters run **before** endpoint-level filters.

## Accessing typed handler arguments

```csharp
public class ValidateIdFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        // Get the first argument (the 'id' route param in the handler)
        var id = ctx.GetArgument<Guid>(0);

        if (id == Guid.Empty)
            return Results.BadRequest("ID cannot be empty GUID.");

        return await next(ctx);
    }
}
```

Argument indices match handler parameter positions (0-based, left to right).

## Short-circuiting

Return a result directly without calling `next` to short-circuit the pipeline:

```csharp
public class ApiKeyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        var hasKey = ctx.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var key);
        if (!hasKey || !IsValid(key!))
            return Results.Problem("Invalid API key", statusCode: 401);

        return await next(ctx);
    }
}
```

## Filter factories

Use a filter factory when the filter needs access to endpoint metadata at registration time (e.g., reading an attribute placed on the endpoint):

```csharp
group.MapGet("/", GetProducts)
     .AddEndpointFilterFactory((factoryContext, next) =>
     {
         // Read metadata at build time
         var attr = factoryContext.EndpointMetadata
             .OfType<RequiresAuditAttribute>()
             .FirstOrDefault();

         if (attr is null) return next;  // no-op if no attribute

         // Return a filter delegate only when the attribute is present
         return async ctx =>
         {
             var result = await next(ctx);
             await AuditLog.WriteAsync(ctx.HttpContext, attr.Reason);
             return result;
         };
     });
```

`factoryContext.EndpointMetadata` contains everything set via `WithMetadata(...)`, `RequireAuthorization(...)`, etc.

## Filters vs. middleware

| | Endpoint Filter | Middleware |
|-|----------------|-----------|
| Scope | One endpoint or group | Entire application |
| Access to handler params | ✅ `ctx.Arguments` | ❌ |
| Short-circuit with typed result | ✅ | Only via `HttpResponse` |
| Ordering | After routing, before handler | Before routing |
| Use for | Validation, per-endpoint auditing, argument checks | Auth, CORS, compression, logging across all routes |
