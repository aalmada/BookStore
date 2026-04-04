---
name: refit
description: Use Refit to define type-safe REST clients in .NET as C# interfaces backed by HttpClient — covering interface definition (HTTP verb attributes, parameter binding, return types), DI registration with AddRefitClient, DelegatingHandler pipelines for auth/headers/logging, error handling with IApiResponse, RefitSettings, and multipart uploads. Trigger whenever the user writes, reviews, or asks about Refit clients, REST API interfaces, AddRefitClient, IApiResponse, ApiException, DelegatingHandler for HTTP, typed HTTP clients, RestService.For, or consuming HTTP APIs in .NET — even if they don't mention "Refit" by name. Always prefer this skill over guessing; Refit's attribute model, IApiResponse vs throws-by-default behavior, handler chain ordering, and scoped-client lifetime rules all have non-obvious failure modes that are easy to get wrong.
---

# Refit

Refit turns a C# interface you annotate with HTTP verb attributes into a working client backed by `HttpClient`. You write the contract; Refit generates the implementation at compile time (source generator) or runtime.

**NuGet packages**
- `Refit` — core (always required)
- `Refit.HttpClientFactory` — `AddRefitClient<T>()` DI integration (add for ASP.NET Core / DI apps)

## Quick reference

| Topic | See |
|-------|-----|
| HTTP verbs, parameter binding, return types, headers | [interface-design.md](references/interface-design.md) |
| DI registration, `AddRefitClient`, `RefitSettings`, scoped clients | [di-registration.md](references/di-registration.md) |
| Auth handlers, Bearer tokens, per-request and static headers | [authentication.md](references/authentication.md) |
| `IApiResponse`, `ApiException`, non-throwing error handling | [error-handling.md](references/error-handling.md) |

## The golden path

```csharp
// 1. Define the interface
public interface IProductsClient
{
    [Get("/api/products/{id}")]
    Task<ProductDto> GetProductAsync(Guid id, CancellationToken cancellationToken = default);

    [Post("/api/products")]
    Task<IApiResponse<ProductDto>> CreateProductAsync([Body] CreateProductRequest body, CancellationToken cancellationToken = default);
}

// 2. Register with DI
services.AddRefitClient<IProductsClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.example.com"))
    .AddHttpMessageHandler<AuthHandler>()
    .AddStandardResilienceHandler();

// 3. Use via injection
public class ProductsPage(IProductsClient client)
{
    async Task LoadAsync()
    {
        var product = await client.GetProductAsync(id);
    }
}
```

## Critical rules

- **Always include `CancellationToken cancellationToken = default`** as the last parameter on every method.
- **Handler chain order matters**: the first `AddHttpMessageHandler` call is outermost (executes first on send, last on receive). Typical order: Resilience → Auth → Correlation → Network.
- **Never register scoped DelegatingHandlers as singletons** — they'll capture scoped services and cause stale state. Register handlers as `AddTransient`.
- **`IApiResponse<T>` suppresses throwing on non-2xx**; raw `Task<T>` throws `ApiException`. Choose at the interface level per method.
- **`InnerHandler` must be `null`** when registering handlers via DI (`AddHttpMessageHandler`); assign it only when constructing manually with `RestService.For<T>`.
