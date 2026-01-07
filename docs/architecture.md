# Architecture Overview

## System Architecture

The Book Store API is built using **Event Sourcing** and **CQRS** patterns with ASP.NET Core Minimal APIs, Marten for event storage, and PostgreSQL as the database.

```mermaid
graph TD
    Client["Client Applications<br/>(Blazor Web, Mobile Apps, Console Apps)<br/>Uses: BookStore.Client library (Refit-based)"]

    subgraph "Book Store API (ASP.NET Core Minimal APIs)"
        direction TB
        subgraph Endpoints
            Public[Public Endpoints]
            Admin[Admin Endpoints]
            System[System Endpoints]
        end
        
        subgraph Handlers [Command Handlers / Aggregates]
            Book[Book]
            Author[Author]
            Category[Category]
            Publisher[Publisher]
        end
    end

    subgraph Marten [Marten Event Store]
        EventStore["Event Store<br/>- Append events<br/>- Stream management<br/>- Correlation/Causation"]
        Projections["Read Models / Projections<br/>- BookSearchProjection<br/>- AuthorProjection<br/>- CategoryProjection"]
    end

    subgraph Database [PostgreSQL Database]
        Postgres["Tables:<br/>- mt_events (Events)<br/>- mt_streams (Metadata)<br/>- Projections (Read Models)"]
    end

    Client -- HTTP/REST --> Endpoints
    Public --> Handlers
    Admin --> Handlers
    System --> Handlers
    
    Handlers -- Events --> EventStore
    EventStore -- Async Projections --> Projections
    
    EventStore -- PostgreSQL Protocol --> Postgres
    Projections -- PostgreSQL Protocol --> Postgres
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
**Command Flow**:
```mermaid
graph LR
    HTTP[HTTP Request] --> Endpoint
    Endpoint --> Command
    Command --> Bus[IMessageBus]
    Bus --> Handler
    Handler --> Aggregate
    Aggregate --> Event
    Event --> Commit[Auto-commit]
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
```mermaid
sequenceDiagram
    participant Client
    participant API as API Endpoint
    participant Domain as Domain Model
    participant Marten as Marten Event Store
    
    Client->>API: 1. HTTP Request
    API->>Domain: 2. Load Aggregate
    Domain->>Domain: 3. Business Logic
    Domain->>Domain: 4. Generate Event
    Domain->>Marten: 5. Append Event
    Marten->>Marten: 6. SaveChanges
    API->>Client: 7. Return Response
```
```

### Read Path (Query)

```
```mermaid
sequenceDiagram
    participant Client
    participant API as API Endpoint
    participant DB as Read Model DB
    
    Client->>API: 1. HTTP Request
    API->>DB: 2. Query Projection
    DB->>API: 3. Return Data (DTOs)
    API->>API: 4. Apply Filters/Pagination
    API->>Client: 5. Return Results
```
```

### Projection Update (Async)

```
```mermaid
sequenceDiagram
    participant EventStore as Marten Event Store
    participant Daemon as Async Daemon
    participant Builder as Projection Builder
    participant DB as Read Model DB

    EventStore->>Daemon: 1. New Event Appended
    Daemon->>Builder: 2. Detect Event
    Builder->>Builder: 3. Process Event
    Builder->>DB: 4. Update Read Model
    DB-->>Daemon: 5. Commit Checkpoint
```
```

## Technology Stack

### Backend
- **ASP.NET Core 10** - Web framework
- **Minimal APIs** - Endpoint definition
- **Wolverine** - Command/handler pattern and message bus
- **Marten** - Event store and document DB
- **PostgreSQL 16** - Database with extensions
- **Aspire** - [Orchestration](aspire-guide.md) and observability
- **Scalar** - API documentation

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
- **TUnit** - Modern testing framework with built-in code coverage
- **Bogus** - Fake data generation for tests
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
- Natural fit with stream versions
- Prevents lost updates

### 6. Identity Stored as Documents (Not Event Sourced)

**Why**:
- **Standardization**: ASP.NET Core Identity provides robust, battle-tested security.
- **Compliance**: GDPR "Right to be Forgotten" is easier to implement with mutable documents than immutable event streams.
- **Simplicity**: Authentication state (current password hash, lock status) is more critical than the history of changes.
- **Performance**: High-frequency read path (login) benefits from simple index lookups.

**Trade-offs**:
- **Audit Trail**: Account changes (password reset, email change) are not automatically event-sourced (must use separate audit logs).
- **Consistency**: Auth data lives outside the primary event stream (though still in Postgres/Marten).

## Scalability Considerations

### Horizontal Scaling

- **API Servers**: Stateless, can scale horizontally
- **Projection Daemon**: Can run on dedicated instances
- **PostgreSQL**: Read replicas for queries

### Performance Optimizations

- **Hybrid Caching**: Two-tier caching (L1 in-memory + L2 Redis) with localization support
- **Connection Pooling**: Npgsql connection pooling
- **Async Projections**: Non-blocking event processing
- **Denormalization**: Optimized read models

### Event Store Growth

- **Archiving**: Old streams can be archived
- **Snapshots**: Aggregate snapshots for large streams (future)
- **Partitioning**: PostgreSQL table partitioning (future)

## Security Considerations

### Authentication & Authorization

### Authentication & Authorization

The application implements a **Token-based authentication system**:

- **JWT Bearer Tokens** - Primary authentication method
  - Used by **Blazor Web App**, Mobile Apps, and integrations
  - Stateless authentication
  - Tokens stored securely in client (HttpOnly cookies or secure storage recommended for production)
  - Automatic token refresh logic

- **Passkey Support** - Passwordless authentication (.NET 10)
  - WebAuthn/FIDO2 standards
  - Phishing-resistant biometric authentication
  - Built-in .NET 10 Identity support

- **Role-Based Authorization** - Admin endpoints protected
  - Admin role for full access
  - Extensible for additional roles
  
See [Authentication Guide](authentication-guide.md) and [Passkey Guide](passkey-guide.md) for details.

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
