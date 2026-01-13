# API Client Instructions (Refit Wrappers)

## 1. Interface Definitions (Refit)
- **Manual Interfaces**: Do NOT use auto-generated clients. Define `I{Action}{Resource}Endpoint.cs`.
- **Headers**: All methods MUST include standard headers:
  ```csharp
  [Header("api-version")] string api_version,
  [Header("X-Correlation-ID")] string x_Correlation_ID,
  [Header("X-Causation-ID")] string x_Causation_ID,
  [Header("Accept-Language")] string? accept_Language = null
  ```
- **Attributes**: Use `[Get]`, `[Post]`, `[Put]`, `[Delete]` with route templates.

## 2. Registration
- **Location**: `BookStoreClientExtensions.cs`.
- **Pattern**:
  ```csharp
  _ = services.AddRefitClient<IMyEndpoint>()
      .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
      .AddStandardHandlers(); // Logging, Auth, etc.
  ```

## 3. SSE Consumption
- **Service**: `BookStoreEventsService` handles the connection.
- **Notifications**: Define new notification types in `BookStore.Shared.Notifications`.
- **Mapping**: Update `_eventTypeMapping` in `BookStoreEventsService.cs` when adding new notifications.
