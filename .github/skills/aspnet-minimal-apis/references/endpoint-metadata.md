# Endpoint Metadata

Metadata is applied with fluent extension methods on the `RouteHandlerBuilder` returned by `MapGet/Post/...`, or on a `RouteGroupBuilder` to apply to all endpoints in the group.

## Core naming and documentation

```csharp
group.MapGet("/{id:guid}", GetProduct)
     .WithName("GetProduct")           // used for link generation and OpenAPI operationId
     .WithSummary("Get a product by ID")       // short OpenAPI summary
     .WithDescription("Returns the full product including variants and images.");  // long description
```

> `WithName` is required for URL generation via `LinkGenerator` or `Results.Created`. It must be unique across the application.

## Tags

Tags group related endpoints in OpenAPI UI (Swagger) and can be applied per-endpoint or per-group.

```csharp
// Preferred: apply at group registration — keeps endpoint classes tag-free
app.MapGroup("/api/products")
   .WithTags("Products")
   .MapProductEndpoints();

// Per-endpoint override if a subset needs different tags
group.MapGet("/export", ExportProducts)
     .WithTags("Products", "Export");
```

Never add `WithTags` inside the feature's `RouteGroupBuilder` extension method — the call site knows the tag; the feature class doesn't need to.

## Authorization

```csharp
// Require any authenticated user
group.MapPost("/", CreateProduct).RequireAuthorization();

// Require a specific policy
group.MapDelete("/{id:guid}", DeleteProduct).RequireAuthorization("Admin");

// Require multiple policies
group.MapPut("/{id:guid}", UpdateProduct).RequireAuthorization("Admin", "ProductManager");

// Applied to the whole group — individual endpoints inherit
app.MapGroup("/admin")
   .RequireAuthorization("Admin")
   .MapAdminEndpoints();

// Opt individual endpoint out of group auth
group.MapGet("/public-overview", GetOverview).AllowAnonymous();
```

## Excluding from OpenAPI

```csharp
// Health checks, internal /, diagnostic endpoints
app.MapGet("/", () => "OK").ExcludeFromDescription();
app.MapHealthChecks("/healthz").ExcludeFromDescription();
```

## Content type negotiation

```csharp
// Declare accepted request content types (for OpenAPI and runtime validation)
group.MapPost("/import", ImportProducts)
     .Accepts<ImportRequest>("application/json");

// File upload
group.MapPost("/upload", UploadCover)
     .Accepts<IFormFile>("multipart/form-data")
     .DisableAntiforgery();
```

## Custom metadata via `WithMetadata`

Use `WithMetadata` to attach any object as endpoint metadata. Custom middleware, filters, and OpenAPI transformers can read it.

```csharp
// Attach a custom attribute
group.MapGet("/", GetProducts)
     .WithMetadata(new CacheControlAttribute(maxAge: 60));

// Attach a marker interface/class
group.MapPost("/bulk", BulkCreate)
     .WithMetadata(new RateLimitAttribute(requestsPerMinute: 10));
```

Reading it in a filter or transformer:
```csharp
var cacheAttr = context.HttpContext.GetEndpoint()
    ?.Metadata.GetMetadata<CacheControlAttribute>();
```

## API versioning

When using `Asp.Versioning`:
```csharp
// Build version set once, apply to the top-level group
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .HasApiVersion(new ApiVersion(2))
    .Build();

var api = app.MapGroup("/api").WithApiVersionSet(apiVersionSet);

// Per-version endpoint registration
api.MapGroup("/products")
   .MapToApiVersion(1)
   .MapProductEndpointsV1();

api.MapGroup("/products")
   .MapToApiVersion(2)
   .MapProductEndpointsV2();
```

## `ProducesResponseType` / `.Produces<T>()`

When using `TypedResults.*` as return types, OpenAPI schemas are inferred automatically — **do not add `.Produces<T>()`** for those. Only add it for handlers that return `IResult` or plain objects:

```csharp
// NOT needed when handler returns TypedResults
static async Task<Ok<ProductDto>> GetProduct(...) => TypedResults.Ok(product);

// Required when handler returns IResult (avoid this pattern)
group.MapGet("/{id}", GetProduct)
     .Produces<ProductDto>(200)
     .Produces(404);
```

See [aspnet-typed-results](../aspnet-typed-results/SKILL.md) for guidance on return types.

## Rate limiting

```csharp
group.MapPost("/", CreateProduct)
     .RequireRateLimiting("fixed-window");

// Or disable rate limiting on an endpoint in an otherwise-limited group
group.MapGet("/status", GetStatus)
     .DisableRateLimiting();
```

## CORS

```csharp
group.MapGet("/public", GetPublicData)
     .RequireCors("AllowAll");
```

## Summary: where to put what

| Metadata | Put it at… |
|----------|------------|
| `WithTags` | group registration (call site in `Program.cs` or hub) |
| `RequireAuthorization` | group level (or endpoint level for exceptions) |
| `AllowAnonymous` | individual endpoint (to opt out of group auth) |
| `WithName` | inside the feature's extension method (names are feature-specific) |
| `WithSummary` / `WithDescription` | inside the feature's extension method |
| `ExcludeFromDescription` | individual system/health endpoints |
| `WithMetadata` | wherever the attribute is semantically coupled |
