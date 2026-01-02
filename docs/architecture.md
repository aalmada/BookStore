# Architecture Overview

## System Architecture

The Book Store API is built using **Event Sourcing** and **CQRS** patterns with ASP.NET Core Minimal APIs, Marten for event storage, and PostgreSQL as the database.

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Applications                      │
│  (Blazor Web, Mobile Apps, Console Apps, etc.)              │
│                                                              │
│  Uses: BookStore.Client library (Refit-based)               │
└────────────────────────┬────────────────────────────────────┘
                         │
                         │ HTTP/REST
                         │
┌────────────────────────▼────────────────────────────────────┐
│                  Book Store API                         │
│                 (ASP.NET Core Minimal APIs)                  │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │   Public     │  │    Admin     │  │   System     │     │
│  │  Endpoints   │  │  Endpoints   │  │  Endpoints   │     │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘     │
│         │                  │                  │              │
│         │                  │                  │              │
│  ┌──────▼──────────────────▼──────────────────▼───────┐    │
│  │           Command Handlers (Aggregates)            │    │
│  │  ┌──────┐  ┌────────┐  ┌──────────┐  ┌─────────┐ │    │
│  │  │ Book │  │ Author │  │ Category │  │Publisher│ │    │
│  │  └──────┘  └────────┘  └──────────┘  └─────────┘ │    │
│  └─────────────────────┬──────────────────────────────┘    │
│                        │                                     │
│                        │ Events                              │
│                        ▼                                     │
│  ┌────────────────────────────────────────────────────┐    │
│  │              Marten Event Store                     │    │
│  │  - Append events                                    │    │
│  │  - Stream management                                │    │
│  │  - Correlation/Causation tracking                   │    │
│  └────────────────────┬───────────────────────────────┘    │
│                       │                                      │
│                       │ Async Projections                    │
│                       ▼                                      │
│  ┌────────────────────────────────────────────────────┐    │
│  │          Read Models (Projections)                  │    │
│  │  ┌──────────────┐  ┌────────────┐  ┌──────────┐   │    │
│  │  │BookSearch    │  │  Author    │  │ Category │   │    │
│  │  │Projection    │  │ Projection │  │Projection│   │    │
│  │  └──────────────┘  └────────────┘  └──────────┘   │    │
│  └────────────────────────────────────────────────────┘    │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          │ PostgreSQL Protocol
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                      PostgreSQL Database                     │
│                                                              │
│  ┌──────────────┐  ┌────────────────┐  ┌────────────────┐ │
│  │  mt_events   │  │  mt_streams    │  │  Projections   │ │
│  │  (Events)    │  │  (Metadata)    │  │  (Read Models) │ │
│  └──────────────┘  └────────────────┘  └────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Core Patterns

### Event Sourcing

Instead of storing current state, we store all changes as immutable events.

> [!NOTE]
> For a comprehensive guide to event sourcing concepts, patterns, and best practices, see the [Event Sourcing Guide](event-sourcing-guide.md).

**Benefits**:
- Complete audit trail
- Time travel (reconstruct past states)
- Event replay for debugging
- Natural fit for distributed systems

**Implementation**:
```csharp
// Events are immutable records
public record BookAdded(
    Guid Id,
    string Title,
    string? Isbn,
    ...
);

// Aggregates apply events to build state
public class BookAggregate
{
    public void Apply(BookAdded @event)
    {
        Id = @event.Id;
        Title = @event.Title;
        ...
    }
}
```

See [Marten Guide](marten-guide.md) for implementation details.

### CQRS (Command Query Responsibility Segregation)

Separate models for writes (commands) and reads (queries).

**Write Side** (Commands):
- Commands routed through Wolverine message bus
- Handlers execute business logic
- Events are appended to streams
- Optimized for consistency

**Read Side** (Queries):
- Projections denormalize data
- Optimized for specific queries
- Eventually consistent

### Wolverine Command/Handler Pattern

Commands are routed through Wolverine's message bus to handlers that execute business logic.

**Command Flow**:
```
HTTP Request → Endpoint → Command → IMessageBus → Handler → Aggregate → Event → Auto-commit
```

**Benefits**:
- Clean separation of concerns
- Automatic transaction management
- Easy to test (pure functions)
- Foundation for async messaging

**Example**:
```csharp
// Endpoint: Just routing
private static Task<IResult> CreateBook(request, IMessageBus bus)
    => bus.InvokeAsync<IResult>(new CreateBook(...));

// Handler: Pure business logic
public static IResult Handle(CreateBook cmd, IDocumentSession session)
{
    var @event = BookAggregate.Create(...);
    session.Events.StartStream(cmd.Id, @event);
    // Wolverine auto-commits
    return Results.Created(...);
}
```

### Async Projections

Projections run asynchronously to build read models from events. With Wolverine integration via `.IntegrateWithWolverine()`, Wolverine manages the async projection daemon, providing distributed coordination and automatic failover across nodes.

```csharp
public class BookSearchProjection
{
    // Denormalized for fast searching
    public string Title { get; set; }
    public string? PublisherName { get; set; }
    public string AuthorNames { get; set; }
    public string SearchText { get; set; }
}
```

## Domain Model

### Aggregates

**Book Aggregate**:
- Root entity for book management
- Enforces business rules
- Emits events: `BookAdded`, `BookUpdated`, `BookSoftDeleted`, `BookRestored`

**Author Aggregate**:
- Manages author information
- Tracks biography and metadata

**Category Aggregate**:
- Supports multi-language translations
- Manages category hierarchy

**Publisher Aggregate**:
- Publisher information management

### Events

All events include:
- Domain data (title, ISBN, etc.)
- Marten metadata (correlation ID, causation ID, timestamp)

Example event flow:
```
1. User creates book → BookAdded event
2. Event stored in mt_events table
3. Async projection updates BookSearchProjection
4. Read model available for queries
```

## Data Flow

### Write Path (Command)

```
1. HTTP Request → Endpoint
2. Load Aggregate from Event Stream
3. Execute Business Logic
4. Generate Domain Event
5. Append Event to Stream
6. SaveChanges (atomic)
7. Return Response
```

### Read Path (Query)

```
1. HTTP Request → Endpoint
2. Query Projection (Read Model)
3. Apply Filters/Pagination
4. Return Results
```

### Projection Update (Async)

```
1. Event Appended to Stream
2. Async Daemon Detects New Event
3. Projection Builder Processes Event
4. Update Read Model
5. Commit Changes
```

## Technology Stack

### Backend
- **ASP.NET Core 10** - Web framework
- **Minimal APIs** - Endpoint definition
- **Wolverine 5.9.2** - Command/handler pattern and message bus
- **Marten 8.17.0** - Event store and document DB
- **PostgreSQL 16** - Database
- **Aspire** - [Orchestration](aspire-guide.md) and observability
- **Scalar 2.11.10** - API documentation

### Features
- **Event Sourcing** - Marten event store
- **CQRS** - Separate read/write models
- **Optimistic Concurrency** - ETags with stream versions
- **Distributed Tracing** - Correlation/causation IDs
- **Multi-language** - Category translations
- **Full-text Search** - PostgreSQL trigrams
- **API Versioning** - Header-based
- **Soft Deletion** - Logical deletes with restore

### Infrastructure
- **Docker** - Container runtime
- **PgAdmin** - Database management
- **OpenTelemetry** - Distributed tracing
- **Health Checks** - Service monitoring
- **Roslyn Analyzers** - Custom analyzers for Event Sourcing/CQRS patterns ([docs](analyzer-rules.md))
- **Roslynator.Analyzers** - Enhanced code analysis
- **Refit** - Type-safe REST library for .NET

## Key Design Decisions

### 1. Event Sourcing with Marten

**Why**: 
- Built-in event store on PostgreSQL
- No additional infrastructure needed
- Strong .NET integration
- Async projection support

**Trade-offs**:
- Learning curve for event sourcing
- Eventually consistent reads
- More complex than CRUD

### 2. Minimal APIs

**Why**:
- Less boilerplate than controllers
- Better performance
- Cleaner endpoint definition
- Native OpenAPI support

### 3. Async Projections

**Why**:
- Decouples write and read models
- Optimized read models for specific queries
- Scalable (can run on separate processes)

**Trade-offs**:
- Eventually consistent
- Projection lag possible
- More complex than direct queries

### 4. Soft Deletion

**Why**:
- Preserve data integrity
- Support undo/restore
- Maintain referential integrity
- Audit trail

### 5. ETags for Concurrency

**Why**:
- Standard HTTP mechanism
- Works with any client
- Natural fit with stream versions
- Prevents lost updates

## Scalability Considerations

### Horizontal Scaling

- **API Servers**: Stateless, can scale horizontally
- **Projection Daemon**: Can run on dedicated instances
- **PostgreSQL**: Read replicas for queries

### Performance Optimizations

- **Output Caching**: Public endpoints cached
- **Connection Pooling**: Npgsql connection pooling
- **Async Projections**: Non-blocking event processing
- **Denormalization**: Optimized read models

### Event Store Growth

- **Archiving**: Old streams can be archived
- **Snapshots**: Aggregate snapshots for large streams (future)
- **Partitioning**: PostgreSQL table partitioning (future)

## Security Considerations

### Authentication & Authorization

- **Future**: Add JWT authentication
- **Future**: Role-based authorization
- **Current**: Admin endpoints unprotected (development only)

### Data Protection

- **Soft Deletion**: Prevents accidental data loss
- **Event Immutability**: Events cannot be modified
- **Audit Trail**: Complete history of all changes

## Monitoring & Observability

### Health Checks

- PostgreSQL connectivity
- Marten event store
- Projection daemon status

### Distributed Tracing

- Correlation IDs track workflows
- Causation IDs track event chains
- OpenTelemetry integration

### Logging

- Structured logging with Serilog
- Aspire dashboard for log aggregation
- Event store for audit logs

## Next Steps

- **[Event Sourcing Guide](event-sourcing-guide.md)** - Event sourcing concepts and patterns
- **[Marten Guide](marten-guide.md)** - Event sourcing implementation with Marten
- **[Wolverine Guide](wolverine-guide.md)** - Command/handler pattern
- **[Aspire Orchestration Guide](aspire-guide.md)** - Service orchestration details
- **[Getting Started](getting-started.md)** - Setup and running the application
