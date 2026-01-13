# Backend Instructions (ApiService)

## 1. Event Sourcing (Marten)
- **Immutability**: All Events, Commands, and Projections MUST be distinct types.
- **Naming**: Events MUST be past tense (e.g., `BookPublished`).
- **IDs**: Use `Guid.CreateVersion7()` for NEW IDs. Never `Guid.NewGuid()`.
- **Timestamps**: Always use `DateTimeOffset` (UTC). Never `DateTime`.

## 2. Command Processing (Wolverine)
- **Pattern**:
  - `Commands/{Resource}/`: Record definitions.
  - `Handlers/{Resource}/`: Static handler methods.
  - `Endpoints/{Resource}/`: Thin routing layer (just `InvokeAsync`).
- **Transactions**: Rely on Wolverine's auto-transaction policy. Do NOT call `SaveChangesAsync`.
- **Concurrency**: Use `ETagHelper` in handlers for Update/Delete commands.

## 3. Projections
- **Structure**:
  - `Create(Event e)`: Initial state.
  - `Apply(Event e)`: State mutation.
- **Localization**: Store translations in `Dictionary<string, string>` (e.g., `Descriptions`).

## 4. Notifications (SSE)
- **Flow**: Projection Update -> `MartenCommitListener` -> `NotifyAsync`.
- **Records**: Define notifications in `BookStore.Shared.Notifications`.
- **Trigger**: Always update `MartenCommitListener` when adding a new Projection.
