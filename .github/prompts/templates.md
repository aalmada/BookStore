# Prompt Templates

## 1. Implement Command Handler
- **Task**: "Implement `static` Wolverine handler for `{Command}`."
- **Constraints**:
  - Command must be a `record` in `.Commands` namespace.
  - Handler must be `static` and return `IResult`.
  - Use `Guid.CreateVersion7()` for new IDs.
  - Add Integration Test in `BookStore.AppHost.Tests`.

## 2. Add Frontend Feature
- **Task**: "Add `{Feature}` to Blazor frontend."
- **Constraints**:
  - Inject `I{Resource}Client`.
  - Use `ReactiveQuery<T>` for fetching.
  - Use `OptimisticUpdateService` if adding to lists.
  - Map SSE events in `QueryInvalidationService`.

## 3. Create Refit Client
- **Task**: "Create `{Resource}` client."
- **Constraints**:
  - Manually define `I{Action}{Resource}Endpoint` in `BookStore.Client`.
  - Add standard headers (`api-version`, `X-Correlation-ID`, etc.).
  - Register in `BookStoreClientExtensions.cs`.
