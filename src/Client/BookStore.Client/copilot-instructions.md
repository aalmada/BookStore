# API Client Instructions (Refit Wrappers)

## 1. Definition Strategy
- **MANUAL Only**: Do not use auto-generators. Define interfaces manually for control (per `api-client-generation.md`).
- **Naming**: `I{Action}{Resource}Endpoint.cs` (e.g., `IGetBooksEndpoint`).

## 2. Interface Rules
- **Return Types**: `Task<T>`, `Task<PagedListDto<T>>`, or `Task<IApiResponse>`.
- **Shared DTOs**: Use `BookStore.Shared.Models`. Create new DTOs there if needed.
- **Standard Headers**:
  ```csharp
  [Header("api-version")] string api_version,
  [Header("X-Correlation-ID")] string x_Correlation_ID,
  [Header("X-Causation-ID")] string x_Causation_ID,
  [Header("Accept-Language")] string? accept_Language = null // Optional
  ```

## 3. Registration
- **File**: `BookStoreClientExtensions.cs`.
- **Pattern**: `services.AddRefitClient<I...>().ConfigureHttpClient(...)`.
