# ApiService Instructions

**Scope**: `src/ApiService/BookStore.ApiService/**`

## Core Rules
- **Domain Logic**: Belongs inside aggregates; application layer coordinates and persists events.
- **Commands/Events**: Use `record` types; add XML summaries to public APIs.
- **Handlers**: Follow Wolverine conventions (`Handle`, prefer static) and keep single responsibility.
- **Aggregates**: Return events from behavior methods; `Apply` methods must follow Marten conventions (void, single parameter).
- **Time**: `DateTimeOffset` (UTC) and ISO 8601 for JSON.

## Real-time Notifications (SSE)
When implementing write operations, enable real-time updates:
1. **Notification**: Create `record {Event}Notification` in `src/Shared/BookStore.Shared/Notifications/DomainEventNotifications.cs` implementing `IDomainEventNotification`.
2. **Listener**: Update `Infrastructure/MartenCommitListener.cs`:
   - Add `case {Resource}Projection proj:` in `ProcessDocumentChangeAsync`
   - Implement `Handle{Resource}ChangeAsync` to call `.NotifyAsync()` with the notification
3. **Frontend**: Clients using `ReactiveQuery` will auto-invalidate based on `QueryInvalidationService` mappings.

## Localization
- **Storage**: Use `Dictionary<string, string>` in projections for localized text fields (e.g., `Biographies`, `Descriptions`).
- **Retrieval**: In endpoints, use `LocalizationHelper.GetLocalizedValue(dict, culture, defaultCulture, fallback)` to extract the correct translation.
- **Culture**: Get from `CultureInfo.CurrentUICulture.Name` (set by middleware from `Accept-Language` header).

## Testing
- Prefer integration tests in `BookStore.AppHost.Tests` that assert persisted state and emitted events.

## References
- See `docs/analyzer-rules.md` for specific implementation patterns.
