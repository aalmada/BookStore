# Backend Instructions (ApiService)

## 1. Event Sourcing (Marten)
- **Streams**: 
  - One stream per Aggregate ID.
  - Use `session.Events.StartStream<TAggregate>(id, @event)` for creation.
  - Use `session.Events.Append(id, @event)` for updates.
- **Aggregates**:
  - Encapsulate business logic.
  - `Apply` methods: Determine state from events (Private, return void).
  - Command methods: Return Events (Do not modify state directly).

## 2. Projections (Read Models)
- **Separation**: NEVER query events directly. Query Projections: `session.Query<BookProjection>()`.
- **Localization**: Store maps in projections (`Dictionary<string, string> Descriptions`).
- **Async**: Default to `.Add<T>(ProjectionLifecycle.Async)` for scalability.

## 3. Messaging (Wolverine)
- **Handlers**: 
  - `public static IResult Handle(Command cmd, IDocumentSession session)`.
  - Return `IResult` (e.g., `Results.Ok`, `Results.Created`).
- **Side Effects**: publish notifications via `IMessageBus` only AFTER transaction commit (handled by Wolverine/Marten integration).

## 4. Caching
- **HybridCache**: Use `cache.GetOrCreateLocalizedAsync`.
- **Tags**: Use granular tags (e.g., `cache.Tag("book:{id}")`).
- **Invalidation**: Handled by `QueryInvalidationService` via SSE (see Frontend).

## 5. Notifications (SSE)
- **Definition**: Create `record {Event}Notification` in `BookStore.Shared`.
- **Trigger**: Update `MartenCommitListener.ProcessDocumentChangeAsync`.
  - Detect projection change -> `localNotificationService.NotifyAsync()`.
