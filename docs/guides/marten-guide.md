# Marten Guide

This guide documents how Marten is currently used in the BookStore API.

Scope:
- Event store and document store configuration
- Event-sourced aggregates and streams
- Projection types and lifecycle
- Multi-tenancy model
- Query patterns with IQuerySession and LINQ

For broader event sourcing concepts, see [Event Sourcing Guide](event-sourcing-guide.md).

## Architecture Summary

BookStore uses Marten for two roles:

1. Event store for write-side domain streams
2. Document store for read models and identity/tenant documents

Wolverine is integrated with Marten, and projection processing is configured for async lifecycles.

## Document Store Configuration

The Marten setup is in `src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs`, and is registered from `src/BookStore.ApiService/Program.cs` through `AddMartenEventStore`.

### Core Configuration

- Connection string comes from Aspire resource name `BookStoreDb`
- `UseLightweightSessions()` is used
- Auto schema behavior is environment-dependent:
  - Development: `AutoCreate.All`
  - Non-development: `AutoCreate.CreateOnly`
- Serialization:
  - System.Text.Json
  - Enums as strings
  - camelCase
  - custom `PartialDateJsonConverter`
- Event metadata is enabled:
  - Correlation ID
  - Causation ID
  - Headers

### Event Store Options

Current options include:

- `EventAppendMode.Rich`
- `UseArchivedStreamPartitioning = true`
- `EnableAdvancedAsyncTracking = true`
- `EnableEventSkippingInProjectionsOrSubscriptions = true`
- `UseIdentityMapForAggregates = true`
- `UseMandatoryStreamTypeDeclaration = true`

Marten is integrated with Wolverine via:

- `PublishEventsToWolverine("marten")`
- `IntegrateWithWolverine(...)` with Wolverine-managed event subscription distribution enabled

Note: the project convention is to rely on Wolverine-managed projection flow and not manually run Marten daemon workflows in feature code.

### Schema and Index Configuration

Configured schema/index mappings include:

- `BookSearchProjection`:
  - B-tree on `PublisherId`, `Title`, `Deleted`
  - GIN JSON index
  - NGram indexes on `Title`, `AuthorNames`
- `AuthorProjection`:
  - B-tree on `Name`, `Deleted`
  - NGram on `Name`
- `CategoryProjection`:
  - B-tree on `Deleted`
- `PublisherProjection`:
  - B-tree on `Name`, `Deleted`
  - NGram on `Name`
- `ApplicationUser`:
  - unique computed indexes on `NormalizedEmail`, `NormalizedUserName`
  - B-tree indexes for identity and cleanup queries
  - GIN JSON index
  - NGram on `Email`

## Event Types and Streams

Event types are explicitly registered in `RegisterEventTypes`.

### Registered Event Families

- Book events:
  - `BookAdded`, `BookUpdated`, `BookSoftDeleted`, `BookRestored`
  - `BookCoverUpdated`, `BookDiscountUpdated`
  - `BookSaleScheduled`, `BookSaleCancelled`
- Author events:
  - `AuthorAdded`, `AuthorUpdated`, `AuthorSoftDeleted`, `AuthorRestored`
- Category events:
  - `CategoryAdded`, `CategoryUpdated`, `CategorySoftDeleted`, `CategoryRestored`
- Publisher events:
  - `PublisherAdded`, `PublisherUpdated`, `PublisherSoftDeleted`, `PublisherRestored`
- User interaction events (from shared messages):
  - `UserProfileCreated`
  - favorites/ratings/cart events

### Stream Identity Pattern

Streams are keyed by `Guid` identifiers:

- Book stream: `Book.Id`
- Author stream: `Author.Id`
- Category stream: `Category.Id`
- Publisher stream: `Publisher.Id`
- User profile stream: `UserId`

Handlers use:

- `session.Events.StartStream<TAggregate>(id, event)` for creation
- `session.Events.Append(id, event)` for updates
- `session.Events.AggregateStreamAsync<TAggregate>(id)` to rehydrate state
- `session.Events.FetchStreamAsync(id)` for explicit event enumeration (used in sale logic)

## Aggregates and Apply Methods

All aggregate types live in `src/BookStore.ApiService/Aggregates`.

### BookAggregate

State includes catalog data plus soft-delete flag, prices, sales, discount percentage, and cover format.

Apply methods:

- `Apply(BookAdded)`
- `Apply(BookUpdated)`
- `Apply(BookSoftDeleted)`
- `Apply(BookRestored)`
- `Apply(BookCoverUpdated)`
- `Apply(BookSaleScheduled)`
- `Apply(BookSaleCancelled)`
- `Apply(BookDiscountUpdated)`

### AuthorAggregate

Apply methods:

- `Apply(AuthorAdded)`
- `Apply(AuthorUpdated)`
- `Apply(AuthorSoftDeleted)`
- `Apply(AuthorRestored)`

### CategoryAggregate

Apply methods:

- `Apply(CategoryAdded)`
- `Apply(CategoryUpdated)`
- `Apply(CategorySoftDeleted)`
- `Apply(CategoryRestored)`

### PublisherAggregate

Apply methods:

- `Apply(PublisherAdded)`
- `Apply(PublisherUpdated)`
- `Apply(PublisherSoftDeleted)`
- `Apply(PublisherRestored)`

### SaleAggregate

`SaleAggregate` is used to project sale state from existing book stream events in handlers (`FetchStreamAsync` plus manual apply), especially to validate overlaps and cancellation logic.

Apply methods:

- `Apply(BookSaleScheduled)`
- `Apply(BookSaleCancelled)`

### UserProfile Aggregate Stream

`UserProfile` is a projection document built from user-centric events, and handlers also rehydrate it using `AggregateStreamAsync<UserProfile>` for command decisions.

Apply methods include favorites, ratings, and cart events.

## Projection Model and Lifecycle

Projection registration is in `RegisterProjections`.

All configured projections are async (`SnapshotLifecycle.Async` or `ProjectionLifecycle.Async`).

### Snapshot Projections (Single-Stream style)

- `CategoryProjection`
- `AuthorProjection`
- `BookSearchProjection`
- `PublisherProjection`
- `UserProfile`

These are registered with `options.Projections.Snapshot<T>(SnapshotLifecycle.Async)` and use convention-based `Create` and `Apply` methods.

### Multi-Stream Projections

- `BookStatisticsProjection : MultiStreamProjection<BookStatistics, Guid>`
- `AuthorStatisticsProjectionBuilder : MultiStreamProjection<AuthorStatistics, Guid>`
- `CategoryStatisticsProjectionBuilder : MultiStreamProjection<CategoryStatistics, Guid>`
- `PublisherStatisticsProjectionBuilder : MultiStreamProjection<PublisherStatistics, Guid>`

The author/category/publisher statistics projections use custom groupers to route one event to multiple target documents and to handle membership changes (added/removed relationships) using batched prior-state loading.

### Projection Commit Listener

`ProjectionCommitListener` is registered both as a document session listener and async projection listener.

Responsibilities:

- Invalidate HybridCache tags after projection document changes
- Emit SSE notifications after read-model updates

This ensures side effects happen after projection commits.

## Document Types and Purpose

### Primary Read Models

- `BookSearchProjection`:
  - Main read model for catalog browsing/search
  - Denormalized publisher and author data
  - Supports price/sale display and ngram search
- `AuthorProjection`:
  - Author display model with localized biographies
- `CategoryProjection`:
  - Category display model with localized names
- `PublisherProjection`:
  - Publisher display model

### User-Facing State Documents

- `UserProfile`:
  - Favorites, ratings, shopping cart state per user
- `BookStatistics`:
  - Likes and rating aggregates per book
- `AuthorStatistics`:
  - Book counts per author
- `CategoryStatistics`:
  - Book counts per category
- `PublisherStatistics`:
  - Book counts per publisher

### Identity and Tenant Documents

- `ApplicationUser`:
  - Identity user document persisted in Marten
  - includes roles, passkeys, refresh tokens, and confirmation fields
- `Tenant`:
  - tenant registry/configuration document
  - marked with `[DoNotPartition]` and `[Identity]` on `Id`

## Multi-Tenancy with Marten

BookStore uses conjoined tenancy.

### Configuration

- `options.Events.TenancyStyle = TenancyStyle.Conjoined`
- `options.Policies.AllDocumentsAreMultiTenanted()`

Default behavior is tenant-partitioned events/documents in shared tables.

The `Tenant` document is explicitly non-partitioned with `[DoNotPartition]`.

### Session Scoping

`IDocumentSession` is scoped per request/message and opened with tenant id:

- In Wolverine handlers: tenant from `IMessageContext.TenantId` when available
- In HTTP requests: tenant from `ITenantContext`

`IQuerySession` resolves to that same scoped session.

Practical effect: endpoint and handler queries are tenant-scoped by default.

### Metadata Propagation

`MartenMetadataMiddleware` and Wolverine correlation middleware populate:

- `session.CorrelationId`
- `session.CausationId`
- metadata headers like tenant/user/ip/user-agent

This is used for tracing and downstream event metadata.

## Querying Patterns

Read endpoints use `IQuerySession` and Marten LINQ heavily.

### Common Patterns

1. `session.Query<T>()` with filter + sort + pagination
2. Include related read models with `Include(...).On(...)`
3. Use `ToPagedListAsync(page, pageSize, ct)` for paginated responses
4. Use `LoadAsync<T>(id)` for direct lookups (for example `UserProfile`)
5. Use specialized operators where needed:
   - `NgramSearch(...)` for text search over indexed fields
   - `OrderBySql(...)` for JSON/dictionary-based localized sorting

### Query Examples in Current Code

- Book catalog queries include publisher/author/category projections
- Author/category/publisher list endpoints filter soft-deleted rows and apply paging
- Admin endpoints query all rows (including deleted where appropriate)
- Shopping cart endpoint loads `UserProfile` then joins against `BookSearchProjection`

### Cross-Tenant Query Exception Case

Most code is tenant-scoped. One explicit security flow in JWT refresh uses fallback lookup outside current tenant (using non-tenant-scoped session) only when token lookup in current tenant fails.

## Operational Notes

- Program startup applies configured Marten schema to ensure required database objects are present before serving requests
- Read-model invalidation and SSE notification flow depends on projection commit listener wiring
- Keep projections async unless there is a concrete need for inline consistency trade-offs

## Related Guides

- [Event Sourcing Guide](event-sourcing-guide.md)
- [Wolverine Guide](wolverine-guide.md)
- [Caching Guide](caching-guide.md)
- [Real-Time Notifications](real-time-notifications.md)
