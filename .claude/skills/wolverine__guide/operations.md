# Write Operations

## Architecture

Each write operation follows a strict 3-layer separation:

```
Client → Endpoint (thin routing) → IMessageBus → Handler (business logic) → Marten session
```

- **Endpoint** (`Endpoints/Admin/Admin{Resource}Endpoints.cs`): Maps HTTP route, extracts ETag from `If-Match` header, builds command, calls `bus.InvokeAsync<IResult>` with tenant context.
- **Handler** (`Handlers/{Resources}/{Resource}Handlers.cs`): Static `Handle` method. Loads aggregate, validates, calls aggregate method, appends event, invalidates cache.
- **Wolverine** manages the Marten session — no manual `SaveChangesAsync()`.
- **SSE notifications** are emitted automatically by `ProjectionCommitListener` on projection commit.

## Quick Decision Guide

| Scenario | Use |
|----------|-----|
| Create a new resource via `POST` (starts a new event stream) | [Create operation](#create-operation) |
| Modify an existing resource via `PUT` or `PATCH` (appends an event) | [Update operation](#update-operation) |
| Remove or archive a resource via `DELETE` (appends a tombstone event) | [Delete operation](#delete-operation) |

After adding an operation, ensure projections handle the new event. See [`/marten__guide`](../marten__guide/SKILL.md) — Projections.

---

## Create operation

Follow this guide to implement a **Create Operation** (Start Stream) in the ApiService.

1.  **Define the Domain Event**
    -   Create a `record` in `src/BookStore.ApiService/Events/`
    -   **Naming**: `{Resource}Created` (past-tense, period, no verbs)
    -   **Template**: `templates/Event.cs`

2.  **Define the Command**
    -   Add a `record` to `src/BookStore.ApiService/Commands/{Resource}/{Resource}Commands.cs`
    -   **Naming**: `Create{Resource}`
    -   **ID**: Auto-generate via `public Guid Id { get; init; } = Guid.CreateVersion7();`
    -   **Template**: `templates/Command.cs`

3.  **Implement the Handler**
    -   Add a static `Handle(Create{Resource} command, ...)` method to `src/BookStore.ApiService/Handlers/{Resources}/{Resource}Handlers.cs`
    -   Call `{Resource}Aggregate.CreateEvent(...)` → check `IsFailure` → `StartStream<{Resource}Aggregate>`
    -   Invalidate list cache with `cache.RemoveByTagAsync([CacheTags.{Resource}List], default)`
    -   Return `Results.Created($"/api/admin/{resource_plural}/{command.Id}", new {{ id = command.Id }})`
    -   **Template**: `templates/CreateHandler.cs`

4.  **Implement the Endpoint**
    -   Add a `MapPost` route to `src/BookStore.ApiService/Endpoints/Admin/Admin{Resource}Endpoints.cs`
    -   Build the command from the request body; pass `new DeliveryOptions {{ TenantId = tenantContext.TenantId }}`
    -   Return `bus.InvokeAsync<IResult>(command, deliveryOptions, cancellationToken)`
    -   **Template**: `templates/CreateEndpoint.cs`

5.  **Update Read Models**
    -   Ensure your projections handle the `{Resource}Created` event.

---

## Update operation

Follow this guide to implement an **Update Operation** (Append Event) in the ApiService.

1.  **Define the Domain Event**
    -   Create a `record` in `src/BookStore.ApiService/Events/`
    -   **Naming**: `{Resource}Updated` or specific past-tense action (e.g., `BookPriceChanged`)
    -   **Template**: `templates/Event.cs`

2.  **Define the Command**
    -   Add a `record` to `src/BookStore.ApiService/Commands/{Resource}/{Resource}Commands.cs`
    -   **Naming**: `{Verb}{Resource}` (e.g., `UpdateAuthor`)
    -   **Interface**: Implement `IHaveETag` for optimistic concurrency: `: IHaveETag { public string? ETag { get; set; } }`
    -   **Template**: `templates/Command.cs`

3.  **Implement the Handler**
    -   Add a static `Handle(Update{Resource} command, ...)` method to `src/BookStore.ApiService/Handlers/{Resources}/{Resource}Handlers.cs`
    -   Load aggregate via `session.Events.AggregateStreamAsync<{Resource}Aggregate>(command.Id)`
    -   Return 404 if `null`; check `ETagHelper.ParseETag` / `ETagHelper.PreconditionFailed()` for version mismatch
    -   Call `aggregate.UpdateEvent(...)` → check `IsFailure` → `session.Events.Append`
    -   Invalidate caches: `cache.RemoveByTagAsync([CacheTags.{Resource}List, CacheTags.ForItem(..., command.Id)], ct)`
    -   Return `Results.NoContent()`
    -   **Template**: `templates/UpdateHandler.cs`

4.  **Implement the Endpoint**
    -   Add a `MapPut` (or `MapPatch`) route to `src/BookStore.ApiService/Endpoints/Admin/Admin{Resource}Endpoints.cs`
    -   Extract ETag from the `If-Match` header: `context.Request.Headers["If-Match"].FirstOrDefault()`
    -   Set it on the command: `new Commands.Update{Resource}(id, ...) {{ ETag = etag }}`
    -   Return `bus.InvokeAsync<IResult>(command, deliveryOptions, cancellationToken)`
    -   **Template**: `templates/UpdateEndpoint.cs`

5.  **Update Read Models**
    -   Ensure your projections handle the new event.

---

## Delete operation

Follow this guide to implement a **Delete Operation** (Soft Delete / Tombstone) in the ApiService.

1.  **Define the Domain Event**
    -   Create a `record` in `src/BookStore.ApiService/Events/`
    -   **Naming**: `{Resource}Deleted`
    -   **Template**: `templates/Event.cs`

2.  **Define the Command**
    -   Add a `record` to `src/BookStore.ApiService/Commands/{Resource}/{Resource}Commands.cs`
    -   **Naming**: `SoftDelete{Resource}`
    -   **Interface**: Implement `IHaveETag`: `: IHaveETag { public string? ETag { get; set; } }`
    -   **Template**: `templates/Command.cs`

3.  **Implement the Handler**
    -   Add a static `Handle(SoftDelete{Resource} command, ...)` method to `src/BookStore.ApiService/Handlers/{Resources}/{Resource}Handlers.cs`
    -   Load aggregate, validate ETag, call `aggregate.SoftDeleteEvent()`, `session.Events.Append`
    -   Invalidate caches; return `Results.NoContent()`
    -   **Template**: `templates/DeleteHandler.cs`

4.  **Implement the Endpoint**
    -   Add a `MapDelete` route to `src/BookStore.ApiService/Endpoints/Admin/Admin{Resource}Endpoints.cs`
    -   Extract ETag from `If-Match` header; set on command; dispatch via `bus.InvokeAsync`
    -   **Template**: `templates/DeleteEndpoint.cs`

5.  **Update Read Models**
    -   Ensure your projections handle the `{Resource}Deleted` event (e.g., set `Deleted = true`).
