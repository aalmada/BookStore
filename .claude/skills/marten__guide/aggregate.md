# Aggregate Scaffold

Follow this guide to create a new **event-sourced aggregate** in the ApiService following strict Event Sourcing patterns.

1. **Define the Initial Event**
   - Create a `record` in `src/BookStore.ApiService/Events/`
   - **Naming**: Past tense verb (e.g., `AuthorAdded`, `AuthorUpdated`, `AuthorSoftDeleted`, `AuthorRestored`)
   - **Never** use command-style names (`CreateAuthor`, `AddAuthor`)
   - **Timestamp**: Always include `DateTimeOffset` (use `DateTimeOffset.UtcNow` in the handler, never `DateTime.Now`)
   - **Example**:
     ```csharp
     namespace BookStore.ApiService.Events;

     public record AuthorAdded(
         Guid Id,
         string Name,
         DateTimeOffset Timestamp
     );

     public record AuthorUpdated(
         Guid Id,
         string Name,
         DateTimeOffset Timestamp
     );

     public record AuthorSoftDeleted(Guid Id, DateTimeOffset Timestamp);
     public record AuthorRestored(Guid Id, DateTimeOffset Timestamp);
     ```

2. **Register Event Types in Marten**
   - Open `src/BookStore.ApiService/Infrastructure/Extensions/MartenConfigurationExtensions.cs`
   - Add all new event types in `RegisterEventTypes`:
     ```csharp
     _ = options.Events.AddEventType<Events.AuthorAdded>();
     _ = options.Events.AddEventType<Events.AuthorUpdated>();
     _ = options.Events.AddEventType<Events.AuthorSoftDeleted>();
     _ = options.Events.AddEventType<Events.AuthorRestored>();
     ```

3. **Create the Aggregate (Write Model)**
   - Create a `class` in `src/BookStore.ApiService/Aggregates/`
   - Implement `ISoftDeleted` from `Marten.Metadata` for soft-delete support
   - Properties use `private set` (enforced by analyzer BS3005)
   - Marten rehydrates the aggregate by calling `void Apply(EventType)` methods — **no** `Create` factory needed
   - **Template**:
     ```csharp
     using BookStore.ApiService.Events;
     using Marten.Metadata;

     namespace BookStore.ApiService.Aggregates;

     public class AuthorAggregate : ISoftDeleted
     {
         public Guid Id { get; private set; }
         public string Name { get; private set; } = string.Empty;
         public long Version { get; private set; }

     #pragma warning disable BS3005 // ISoftDeleted requirement — Marten sets these directly
         public bool Deleted { get; set; }
         public DateTimeOffset? DeletedAt { get; set; }
     #pragma warning restore BS3005

         // Marten calls these to rehydrate state from the event stream
         void Apply(AuthorAdded @event)
         {
             Id = @event.Id;
             Name = @event.Name;
             Deleted = false;
         }

         void Apply(AuthorUpdated @event) => Name = @event.Name;

         void Apply(AuthorSoftDeleted @event)
         {
             Deleted = true;
             DeletedAt = @event.Timestamp;
         }

         void Apply(AuthorRestored _)
         {
             Deleted = false;
             DeletedAt = null;
         }

         // --- Command methods return Result<Event> (never throw) ---

         public static Result<AuthorAdded> CreateEvent(Guid id, string name)
         {
             if (id == Guid.Empty)
                 return Result.Failure<AuthorAdded>(Error.Validation(ErrorCodes.Authors.IdRequired, "Author ID is required"));

             if (string.IsNullOrWhiteSpace(name))
                 return Result.Failure<AuthorAdded>(Error.Validation(ErrorCodes.Authors.NameRequired, "Name is required"));

             return new AuthorAdded(id, name, DateTimeOffset.UtcNow);
         }

         public Result<AuthorUpdated> UpdateEvent(string name)
         {
             if (Deleted)
                 return Result.Failure<AuthorUpdated>(Error.Conflict(ErrorCodes.Authors.AlreadyDeleted, "Cannot update a deleted author"));

             if (string.IsNullOrWhiteSpace(name))
                 return Result.Failure<AuthorUpdated>(Error.Validation(ErrorCodes.Authors.NameRequired, "Name is required"));

             return new AuthorUpdated(Id, name, DateTimeOffset.UtcNow);
         }

         public Result<AuthorSoftDeleted> SoftDeleteEvent()
         {
             if (Deleted)
                 return Result.Failure<AuthorSoftDeleted>(Error.Conflict(ErrorCodes.Authors.AlreadyDeleted, "Author is already deleted"));

             return new AuthorSoftDeleted(Id, DateTimeOffset.UtcNow);
         }

         public Result<AuthorRestored> RestoreEvent()
         {
             if (!Deleted)
                 return Result.Failure<AuthorRestored>(Error.Conflict(ErrorCodes.Authors.NotDeleted, "Author is not deleted"));

             return new AuthorRestored(Id, DateTimeOffset.UtcNow);
         }
     }
     ```

   > **Key rules:**
   > - `Apply` methods are `void`, package-private (no access modifier), and take one event parameter (BS1002)
   > - `ISoftDeleted.Deleted` and `ISoftDeleted.DeletedAt` must be `public set` — suppress BS3005 with pragma
   > - Command methods return `Result<TEvent>` — **never** `throw` business exceptions

4. **Create Unit Tests**
   - Create test file in `tests/BookStore.ApiService.UnitTests/Aggregates/`
   - Use TUnit (`[Test]`, `await Assert.That(...)`)
   - **Test Pattern**:
     ```csharp
     [Test]
     public async Task CreateEvent_ValidData_ReturnsAuthhorAdded()
     {
         var id = Guid.CreateVersion7();
         var result = AuthorAggregate.CreateEvent(id, "Martin Fowler");

         await Assert.That(result.IsSuccess).IsTrue();
         await Assert.That(result.Value.Id).IsEqualTo(id);
         await Assert.That(result.Value.Name).IsEqualTo("Martin Fowler");
     }

     [Test]
     public async Task UpdateEvent_WhenDeleted_ReturnsFailure()
     {
         var aggregate = new AuthorAggregate();
         aggregate.Apply(new AuthorSoftDeleted(Guid.CreateVersion7(), DateTimeOffset.UtcNow));

         var result = aggregate.UpdateEvent("New Name");

         await Assert.That(result.IsFailure).IsTrue();
     }
     ```

5. **Verify Analyzer Compliance**
   - Run `dotnet build` to check for BS1xxx-BS4xxx warnings
   - Ensure:
     - ✅ Events are `record` types (BS1001)
     - ✅ `Apply` methods are `void`, single parameter (BS1002)
     - ✅ Aggregate command methods return `Result<TEvent>` (not void)
     - ✅ `Guid.CreateVersion7()` used, not `Guid.NewGuid()` (BS4001)
     - ✅ `DateTimeOffset.UtcNow` used, not `DateTime.Now` (BS4002)

6. **Next Steps**
    - Read [`projections.md`](projections.md) to create read models from this aggregate's events
    - Use [`/wolverine__guide`](../wolverine__guide/SKILL.md) to add command/handler/endpoint flows
    - Use [`/test__integration_scaffold`](../test__integration_scaffold/SKILL.md) to create integration tests
    - Use [`/test__verify_feature`](../test__verify_feature/SKILL.md) to run all verification checks

## Related Skills

**Next Steps**:
- [`projections.md`](projections.md) — Create read models from aggregate events
- [`/wolverine__guide`](../wolverine__guide/SKILL.md) — Add commands, handlers, and endpoints for this aggregate
- [`/test__integration_scaffold`](../test__integration_scaffold/SKILL.md) — Create integration tests
- [`/test__verify_feature`](../test__verify_feature/SKILL.md) — Run all verification checks

**See Also**:
- [event-sourcing-guide](../../../docs/guides/event-sourcing-guide.md) — Event Sourcing patterns
- [marten-guide](../../../docs/guides/marten-guide.md) — Marten event store integration
- [analyzer-rules](../../../docs/guides/analyzer-rules.md) — Code analyzer rules (BS1xxx-BS4xxx)
- ApiService AGENTS.md — Backend patterns and conventions

## Key Rules to Remember

- **Apply Methods**: MUST be `void` with single event parameter (Marten convention)
- **Behavior Methods**: Return events, don't mutate state directly
- **IDs**: Use `Guid.CreateVersion7()` for new aggregates
- **Immutability**: Use `record` types with `init` properties
- **Validation**: Validate in behavior methods, throw exceptions for invalid operations
