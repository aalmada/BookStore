---
name: Scaffold Write
description: Guide for adding a new write operation (command/mutation) to the Backend. Focuses on Event Sourcing, Wolverine commands, and Projections.
---

Follow this guide to implement a state-changing operation in the **Backend** (ApiService) using strict project standards.

1. **Define the Domain Event**
   - Create a `record` in `src/ApiService/BookStore.ApiService/Events/`.
   - **Naming**: Past tense (e.g., `BookPublished`, not `PublishBook`).
   - **Timestamps**: MUST use `DateTimeOffset` (never `DateTime`).
   - **IDs**: MUST use `Guid.CreateVersion7()` (never `Guid.NewGuid()`).
   - **Example**:
     ```csharp
     public record BookPublished(Guid Id, DateTimeOffset Timestamp);
     ```

2. **Define the Command**
   - Create a `record` in `src/ApiService/BookStore.ApiService/Commands/{Resource}/` (e.g., `Commands/Books/`).
   - **For Creation**:
     ```csharp
     public record CreateBook(...) {
         public Guid Id { get; init; } = Guid.CreateVersion7(); // Rule: Use Version 7
     }
     ```
   - **For Updates/Deletes**:
     ```csharp
     public record UpdateBook(...) {
         public string? ETag { get; init; } // Rule: Optimistic Concurrency
     }
     ```

3. **Implement the Wolverine Handler**
   - Create/Update `src/ApiService/BookStore.ApiService/Handlers/{Resource}/{Resource}Handlers.cs`.
   - **Signature**: `public static IResult Handle(Command cmd, IDocumentSession session, ...)`
   - **Event Creation**: Always use `DateTimeOffset.UtcNow`.
   - **Flow**:
     1. **Ident**: `await session.Events.FetchStreamStateAsync(cmd.Id)`
     2. **Concurrency**: `ETagHelper.CheckIfMatch(context, currentETag)`
     3. **Load**: `await session.Events.AggregateStreamAsync<Aggregate>(cmd.Id)`
     4. **Logic**: `var @event = aggregate.DoSomething(..., DateTimeOffset.UtcNow);`
     5. **Store**: `session.Events.Append(cmd.Id, @event)`
     6. **Return**: `Results.Ok` (Wolverine auto-commits).

4. **Expose the Endpoint (Backend)**
   - Open `src/ApiService/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`.
   - **Signature**: MUST accept `CancellationToken`.
   - **Implementation**:
     - Inject `IMessageBus`.
     - `var etag = context.Request.Headers["If-Match"].FirstOrDefault();`
     - `return await bus.InvokeAsync<IResult>(command, cancellationToken);`

5. **Update Read Model (Projections)**
   - Open `src/ApiService/BookStore.ApiService/Projections/{Resource}Projection.cs`.
   - Implement `Create` or `Apply` methods.

6. **Enable Real-time Updates (SSE)**
   - **Notification**: Create `record {Event}Notification` in `src/Shared/BookStore.Shared/Notifications/DomainEventNotifications.cs`.
   - **Listener**: Update `src/ApiService/BookStore.ApiService/Infrastructure/MartenCommitListener.cs`.
     - Add `case {Resource}Projection proj:` in `ProcessDocumentChangeAsync`.
     - Implement `Handle{Resource}ChangeAsync` to `.NotifyAsync()` the new notification.

7. **Client Integration**
   - **Interface**: Create `src/Client/BookStore.Client/I{Action}{Resource}Endpoint.cs` manually.
   - **DTOs**: If a Request DTO is needed, create it in `src/Shared/BookStore.Shared/Models/` (do not rely on auto-generation).
   - **Registration**: Add to `BookStoreClientExtensions.cs`.

8. **Verify**
   - Run `/verify-feature` to ensure build, format, and tests pass.
