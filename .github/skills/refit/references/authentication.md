# Authentication and Header Handling

## Recommended approach: DelegatingHandler

Create a `DelegatingHandler` that reads auth state from injected services and adds headers to each request. This keeps auth logic out of the interface and works cleanly with DI lifecycles.

```csharp
// Handler — registered as AddTransient
public class AuthHandler(ITokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenService.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

// Registration
services.AddTransient<AuthHandler>();
services.AddRefitClient<IOrdersClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.example.com"))
    .AddHttpMessageHandler<AuthHandler>();
```

> **InnerHandler must be null when using DI.** Only set `InnerHandler` when constructing `DelegatingHandler` chains manually with `RestService.For<T>`.

## StaticHeaders via `[Headers]` attribute

Use for headers that are the same on every call (e.g., `Accept`, `X-Api-Version`). Can be applied at interface or method level; method-level overrides interface-level.

```csharp
[Headers("Accept: application/json", "X-Api-Version: 2")]
public interface IOrdersClient
{
    [Get("/api/orders/{id}")]
    Task<OrderDto> GetOrderAsync(Guid id, CancellationToken ct = default);
}
```

## Dynamic header per call via `[Header]` parameter

Pass a different value per call. Useful for `If-Match` ETags, tenant IDs, or correlation IDs that vary per request.

```csharp
[Put("/api/orders/{id}")]
Task<IApiResponse> UpdateOrderAsync(
    Guid id,
    [Body] UpdateOrderRequest body,
    [Header("If-Match")] string? etag = null,
    CancellationToken ct = default);

// Call site
await client.UpdateOrderAsync(id, body, etag: ETagHelper.GenerateETag(version));
```

## Bearer token via `[Authorize]` attribute

Shortcut for setting `Authorization: Bearer <value>` per call. Useful when the token is available at the call site (e.g., from a passed-in context).

```csharp
[Get("/api/me/orders")]
Task<List<OrderDto>> GetMyOrdersAsync(
    [Authorize] string accessToken,
    CancellationToken ct = default);

// Call site
await client.GetMyOrdersAsync(accessToken: token);
```

The scheme defaults to `"Bearer"`. Pass a different scheme: `[Authorize("ApiKey")]`.

## `AuthorizationHeaderValueGetter` in `RefitSettings`

Alternative to a DelegatingHandler when you want to supply a bearer token without writing a handler class. The function is `async` so you can call a token service.

```csharp
services.AddRefitClient<IOrdersClient>(sp => new RefitSettings
{
    AuthorizationHeaderValueGetter = async (request, ct) =>
    {
        var tokenService = sp.GetRequiredService<ITokenService>();
        return await tokenService.GetAccessTokenAsync(ct);
    }
})
.ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.example.com"));
```

> Prefer a `DelegatingHandler` for complex auth logic (token refresh, multi-factor headers), and `AuthorizationHeaderValueGetter` for simple read-once tokens.

## Custom DelegatingHandler for shared concerns

Use a single handler to inject multiple cross-cutting headers (correlation IDs, tenant, API version, locale), so these concerns don't leak into every interface method.

```csharp
public class CorrelationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", activity.TraceId.ToString());
            request.Headers.TryAddWithoutValidation("X-Causation-ID", activity.ParentId);
        }
        request.Headers.AcceptLanguage.TryParseAdd(CultureInfo.CurrentUICulture.Name);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

## Choosing the right approach

| Scenario | Approach |
|----------|----------|
| Token from DI service, refreshable | `DelegatingHandler` + `AddHttpMessageHandler<AuthHandler>` |
| Simple static bearer token at call site | `[Authorize]` parameter |
| Same token for all requests, available at startup | `AuthorizationHeaderValueGetter` in `RefitSettings` |
| Cross-cutting headers (correlation, locale, tenant) | `DelegatingHandler` registered for all clients |
| Per-call ETag or conditional headers | `[Header("If-Match")]` parameter |
| Static constant headers (Accept, API version) | `[Headers]` on interface or method |
