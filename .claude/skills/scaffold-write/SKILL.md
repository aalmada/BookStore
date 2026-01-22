---
name: scaffold-write
description: Guide for adding a new write operation (command/mutation) to the Backend. Focuses on Event Sourcing, Wolverine commands, and Projections.
license: MIT
---

Follow this guide to implement a state-changing operation in the **Backend** (ApiService) using strict project standards.

1. **Define the Domain Event**
   - Create a `record` in `src/BookStore.ApiService/Events/`.
   - **Naming**: Past tense (e.g., `BookPublished`, not `PublishBook`).
   - **Timestamps**: MUST use `DateTimeOffset` (never `DateTime`).
   - **IDs**: MUST use `Guid.CreateVersion7()` (never `Guid.NewGuid()`).
   - **Example**:
     ```csharp
     public record BookPublished(Guid Id, DateTimeOffset Timestamp);
     ```

2. **Define the Command**
   - Create a `record` in `src/BookStore.ApiService/Commands/{Resource}/` (e.g., `Commands/Books/`).
   - **Template**: `templates/Command.cs`

3. **Implement the Wolverine Handler**
   - Create/Update `src/BookStore.ApiService/Handlers/{Resource}/{Resource}Handlers.cs`.
   - **Template**: `templates/Handler.cs`

4. **Expose the Endpoint (Backend)**
   - Open `src/BookStore.ApiService/Endpoints/{Resource}Endpoints.cs`.
   - **Template**: `templates/Endpoint.cs`

5. **Update Read Model (Projections)**
   - Open `src/BookStore.ApiService/Projections/{Resource}Projection.cs`.
   - Implement `Create` or `Apply` methods.

6. **Enable Real-time Updates (SSE)**
   - **Notification**: Create `record {Event}Notification` in `src/Shared/BookStore.Shared/Notifications/DomainEventNotifications.cs`.
   - **Listener**: Update `src/BookStore.ApiService/Infrastructure/MartenCommitListener.cs`.
     - Add `case {Resource}Projection proj:` in `ProcessDocumentChangeAsync`.
     - Implement `Handle{Resource}ChangeAsync` to `.NotifyAsync()` the new notification.
   - **Reference**: See [real-time-notifications](../../../docs/guides/real-time-notifications.md) for the complete data flow.

7. **Client Integration**
   - **Interface**: Create `src/Client/BookStore.Client/I{Action}{Resource}Endpoint.cs` manually.
   - **DTOs**: If a Request DTO is needed, create it in `src/Shared/BookStore.Shared/Models/` (do not rely on auto-generation).
   - **Registration**: Add to `BookStoreClientExtensions.cs`.

// turbo
8. **Verify**
   - Run `/verify-feature` to ensure build, format, and tests pass.

## Related Skills

**Prerequisites**:
- For complex aggregates, consider `/scaffold-aggregate` first to create the domain model

**Next Steps**:
- `/scaffold-read` - Add query endpoints for the new resource
- `/scaffold-frontend-feature` - Create UI for the new feature
- `/scaffold-test` - Create integration tests
- `/verify-feature` - Complete verification

**Specialized Skills**:
- `/scaffold-aggregate` - Generate event-sourced aggregates with Apply methods
- `/scaffold-projection` - Generate read model projections for queries

**See Also**:
- [scaffold-aggregate](../scaffold-aggregate/SKILL.md) - Detailed aggregate patterns
- [scaffold-projection](../scaffold-projection/SKILL.md) - Projection creation
- [wolverine-guide](../../../docs/guides/wolverine-guide.md) - Wolverine command/handler patterns
- ApiService AGENTS.md - Backend patterns and conventions
