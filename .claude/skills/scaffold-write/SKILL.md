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
   - **Template**: `templates/Command.cs`

3. **Implement the Wolverine Handler**
   - Create/Update `src/ApiService/BookStore.ApiService/Handlers/{Resource}/{Resource}Handlers.cs`.
   - **Template**: `templates/Handler.cs`

4. **Expose the Endpoint (Backend)**
   - Open `src/ApiService/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`.
   - **Template**: `templates/Endpoint.cs`

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
