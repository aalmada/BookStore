# Error Handling

## Default behaviour: `Task<T>` throws `ApiException`

When a method returns `Task<T>` or `Task`, Refit throws `ApiException` for any non-2xx HTTP response. `ApiException` carries the full response so you can inspect it.

```csharp
try
{
    var product = await client.GetProductAsync(id);
    // use product...
}
catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // handle not found
}
catch (ApiException ex)
{
    var problemDetails = await ex.GetContentAsAsync<ProblemDetails>();
    logger.LogError("API error {StatusCode}: {Content}", ex.StatusCode, ex.Content);
}
```

Key `ApiException` members:

| Member | Description |
|--------|-------------|
| `StatusCode` | `HttpStatusCode` of the response |
| `Content` | Raw response body as string |
| `ReasonPhrase` | HTTP reason phrase |
| `Headers` | Response headers |
| `GetContentAsAsync<T>()` | Deserialize the error body to `T` |
| `RequestMessage` | The original `HttpRequestMessage` |

## Non-throwing: `IApiResponse<T>`

Declare the return type as `Task<IApiResponse<T>>` (or `Task<IApiResponse>` for no body) to suppress the throw. The response object is always returned regardless of status code, and the caller inspects `IsSuccessful`.

```csharp
public interface IOrdersClient
{
    [Post("/api/orders")]
    Task<IApiResponse<OrderDto>> CreateOrderAsync([Body] CreateOrderRequest body, CancellationToken ct = default);

    [Delete("/api/orders/{id}")]
    Task<IApiResponse> DeleteOrderAsync(Guid id, CancellationToken ct = default);
}

// Usage
var response = await client.CreateOrderAsync(request);
if (response.IsSuccessful)
{
    var order = response.Content; // non-null when IsSuccessful is true
    return Results.Created($"/api/orders/{order.Id}", order);
}
else
{
    var problem = await response.Error!.GetContentAsAsync<ProblemDetails>();
    return Results.Problem(problem);
}
```

`IApiResponse<T>` members:

| Member | Description |
|--------|-------------|
| `IsSuccessful` | `true` when `IsSuccessStatusCode` and content non-null |
| `IsSuccessStatusCode` | `true` for 2xx status codes |
| `Content` | Deserialized response body (non-null when `IsSuccessful`) |
| `Error` | `ApiException?` — non-null when not successful |
| `StatusCode` | `HttpStatusCode` |
| `Headers` | Response headers |
| `ContentHeaders` | Content-specific headers |

> `IApiResponse<T>` is `IDisposable`. Wrap usage in `using` if building long-lived response objects, though in most cases it is disposed automatically within the request scope.

## Mapping non-throwing to Result pattern

In projects using a `Result<T>` pattern (ProblemDetails-based), map the `IApiResponse` at the call site:

```csharp
var response = await client.CreateOrderAsync(new CreateOrderRequest(...));
return response.IsSuccessful
    ? Result.Success(response.Content!)
    : Result.Failure<OrderDto>(response.StatusCode switch
    {
        HttpStatusCode.Conflict => Error.Conflict("Order.Duplicate", "An order already exists"),
        HttpStatusCode.UnprocessableEntity => Error.Validation("Order.Invalid", response.Error!.Content ?? ""),
        _ => Error.Unexpected("Order.ApiError", $"HTTP {(int)response.StatusCode}")
    });
```

## Custom `ExceptionFactory`

When using `Task<T>` (throwing mode) but you want to suppress or replace exceptions globally, set `ExceptionFactory` in `RefitSettings`:

```csharp
services.AddRefitClient<IOrdersClient>(new RefitSettings
{
    ExceptionFactory = response =>
    {
        // Only throw for server errors (5xx); treat 4xx as domain errors handled by caller
        if ((int)response.StatusCode >= 500)
            return ApiException.Create(response.RequestMessage!, response.RequestMessage!.Method, response, new RefitSettings());
        return Task.FromResult<Exception?>(null);
    }
});
```

## Choosing between throwing and non-throwing

| Scenario | Recommendation |
|----------|---------------|
| Read-only queries where failures propagate up | `Task<T>` (throwing) |
| Mutations (POST/PUT/DELETE) with business-rule 4xx responses | `Task<IApiResponse<T>>` |
| When you need to inspect status codes at the call site | `Task<IApiResponse<T>>` |
| Gateway/proxy that re-maps errors | `Task<IApiResponse<T>>` + custom mapping |
| Integration tests asserting on status codes | `Task<IApiResponse<T>>` |

## DelegatingHandler for centralised error logging

Handle logging in a handler rather than at every call site:

```csharp
public class ErrorLoggingHandler(ILogger<ErrorLoggingHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("HTTP {Method} {Uri} → {Status}: {Content}",
                request.Method, request.RequestUri, (int)response.StatusCode, content);
        }
        return response;
    }
}
```
