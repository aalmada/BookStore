# Client SDK Instructions

**Scope**: `src/Client/BookStore.Client/**`

## Core Rules
- **Refit**: Use `Refit` for all API definitions. 
- **DTOs**: Ensure DTOs match API contracts exactly. Uses `record` types.
- **Serialization**: Handle JSON serialization/deserialization correctly (camelCase, ISO 8601).
- **Error Handling**: Gracefully handle API errors and exceptions.

## Architecture
The client uses an **Interface Aggregation** pattern to keep endpoints granular while providing a unified entry point.

1.  **Granular Endpoints**: Define each API endpoint as a separate interface (e.g., `IGetBooksEndpoint`, `ICreateBookEndpoint`).
    - Use `[Get("/books")]`, `[Post("/books")]`, etc.
    - Return `Task<T>` or `Task<IApiResponse<T>>`.
2.  **Aggregated Interfaces**: specific clients inherit from relevant endpoint interfaces.
    - Example: `public interface IBooksClient : IGetBooksEndpoint, ICreateBookEndpoint ...`
3.  **Extensions**: Use `BookStoreClientExtensions.cs` to manage dependency injection.

## Dependency Injection
Use `AddBookStoreClient` to register all clients.

```csharp
builder.Services.AddBookStoreClient(
    baseAddress: new Uri("https://api.bookstore.com"),
    configureClient: client => client.AddStandardResilienceHandler()
);
```

## SSE Events
Use `AddBookStoreEvents` to register the `BookStoreEventsService` for real-time updates.

```csharp
builder.Services.AddBookStoreEvents(new Uri("https://api.bookstore.com"));
```
