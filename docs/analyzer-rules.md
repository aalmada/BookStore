# Roslyn Analyzer Rules

The BookStore.ApiService.Analyzers project enforces architectural patterns for Event Sourcing, CQRS, and Domain-Driven Design in the backend API service.

## Overview

The analyzer provides **14 rules across 4 categories** to ensure consistent architecture:

- **Event Sourcing Rules** (BS1xxx): Enforce event immutability and proper structure
- **CQRS Command Rules** (BS2xxx): Ensure commands follow CQRS patterns  
- **Aggregate Rules** (BS3xxx): Validate Marten conventions and event sourcing patterns
- **Handler Rules** (BS4xxx): Enforce Wolverine handler conventions

## Rule Catalog

### Event Sourcing Rules (BS1xxx)

#### BS1001: Events must be declared as record types
- **Severity**: Warning
- **Category**: EventSourcing

Events represent immutable historical facts and should use C# `record` types.

**❌ Bad:**
```csharp
namespace BookStore.ApiService.Events;

public class BookAdded  // Should be a record
{
    public Guid Id { get; init; }
    public string Title { get; init; }
}
```

**✅ Good:**
```csharp
namespace BookStore.ApiService.Events;

public record BookAdded(Guid Id, string Title);
```

#### BS1002: Event properties must be immutable
- **Severity**: Warning
- **Category**: EventSourcing

Event properties must not have mutable setters to preserve historical integrity.

**❌ Bad:**
```csharp
namespace BookStore.ApiService.Events;

public record BookAdded
{
    public string Title { get; set; }  // Should use init
}
```

**✅ Good:**
```csharp
namespace BookStore.ApiService.Events;

public record BookAdded(string Title);
// or
public record BookAdded
{
    public string Title { get; init; }
}
```

#### BS1003: Events must be in Events namespace
- **Severity**: Warning
- **Category**: Architecture

Events should be organized in namespaces ending with `.Events` for consistency.

**❌ Bad:**
```csharp
namespace BookStore.ApiService.Models;

public record BookAdded(Guid Id);  // Should be in Events namespace
```

**✅ Good:**
```csharp
namespace BookStore.ApiService.Events;

public record BookAdded(Guid Id);
```

---

### CQRS Command Rules (BS2xxx)

#### BS2001: Commands must be declared as record types
- **Severity**: Warning
- **Category**: CQRS

Commands are immutable DTOs and should use C# `record` types.

**❌ Bad:**
```csharp
namespace BookStore.ApiService.Commands.Books;

public class CreateBook  // Should be a record
{
    public string Title { get; init; }
}
```

**✅ Good:**
```csharp
namespace BookStore.ApiService.Commands.Books;

public record CreateBook(string Title);
```

#### BS2002: Commands must be in the Commands namespace
- **Severity**: Warning
- **Category**: Architecture

Commands should be organized in namespaces ending with `.Commands`.

**❌ Bad:**
```csharp
namespace BookStore.ApiService.Endpoints.Admin;

public record CreateBookRequest(string Title);  // Should be in Commands namespace
```

**✅ Good:**
```csharp
namespace BookStore.ApiService.Commands.Books;

public record CreateBook(string Title);
```

#### BS2003: Command properties should use init accessors
- **Severity**: Info (Suggestion)
- **Category**: CQRS

Command properties should use init-only setters to ensure immutability after construction.

**❌ Suboptimal:**
```csharp
namespace BookStore.ApiService.Commands.Books;

public record CreateBook
{
    public string Title { get; set; }  // Should use init
}
```

**✅ Good:**
```csharp
namespace BookStore.ApiService.Commands.Books;

public record CreateBook
{
    public string Title { get; init; }
}
```

---

### Aggregate Rules (BS3xxx)

#### BS3001: Apply methods must return void
- **Severity**: Error
- **Category**: EventSourcing

Marten requires `Apply` methods to return `void` for event application.

**❌ Bad:**
```csharp
public class BookAggregate
{
    public BookAdded Apply(BookAdded @event)  // Should return void
    {
        Id = @event.Id;
        return @event;
    }
}
```

**✅ Good:**
```csharp
public class BookAggregate
{
    void Apply(BookAdded @event)
    {
        Id = @event.Id;
    }
}
```

#### BS3002: Apply methods must have exactly one parameter
- **Severity**: Error
- **Category**: EventSourcing

Marten requires `Apply` methods to have exactly one parameter (the event).

**❌ Bad:**
```csharp
public class BookAggregate
{
    void Apply(BookAdded @event, string reason)  // Too many parameters
    {
        Id = @event.Id;
    }
}
```

**✅ Good:**
```csharp
public class BookAggregate
{
    void Apply(BookAdded @event)
    {
        Id = @event.Id;
    }
}
```

#### BS3003: Apply methods should be private or internal
- **Severity**: Warning
- **Category**: EventSourcing

`Apply` methods are called by Marten during rehydration and should not be public.

**❌ Bad:**
```csharp
public class BookAggregate
{
    public void Apply(BookAdded @event)  // Should be private
    {
        Id = @event.Id;
    }
}
```

**✅ Good:**
```csharp
public class BookAggregate
{
    void Apply(BookAdded @event)  // private by default
    {
        Id = @event.Id;
    }
}
```

#### BS3004: Aggregate command methods should return events
- **Severity**: Warning
- **Category**: EventSourcing

Aggregate command methods generate events for event sourcing and should return event types.

**❌ Bad:**
```csharp
public class BookAggregate
{
    public void UpdateTitle(string title)  // Should return event
    {
        // ...
    }
}
```

**✅ Good:**
```csharp
public class BookAggregate
{
    public BookTitleUpdated UpdateTitle(string title)
    {
        return new BookTitleUpdated(Id, title);
    }
}
```

#### BS3005: Aggregate properties should not have public setters
- **Severity**: Warning
- **Category**: DomainModel

Aggregate state changes should only occur through Apply methods, not direct property setters.

**❌ Bad:**
```csharp
public class BookAggregate
{
    public Guid Id { get; set; }  // Should use init or private set
    public string Title { get; set; }
}
```

**✅ Good:**
```csharp
public class BookAggregate
{
    public Guid Id { get; set; }  // Marten needs this for rehydration
    public string Title { get; private set; } = string.Empty;
    
    void Apply(BookTitleUpdated @event)
    {
        Title = @event.Title;  // State changes through Apply
    }
}
```

---

### Handler Convention Rules (BS4xxx)

#### BS4001: Handler methods should be named 'Handle'
- **Severity**: Info (Suggestion)
- **Category**: CQRS

Wolverine discovers handlers by the method name `Handle`.

**❌ Suboptimal:**
```csharp
public static class BookHandlers
{
    public static IResult ProcessCreateBook(CreateBook cmd)  // Should be named Handle
    {
        // ...
    }
}
```

**✅ Good:**
```csharp
public static class BookHandlers
{
    public static IResult Handle(CreateBook cmd)
    {
        // ...
    }
}
```

#### BS4002: Handler methods should be static
- **Severity**: Warning
- **Category**: CQRS

Static handler methods provide better performance in Wolverine.

**❌ Bad:**
```csharp
public class BookHandlers
{
    public IResult Handle(CreateBook cmd)  // Should be static
    {
        // ...
    }
}
```

**✅ Good:**
```csharp
public static class BookHandlers
{
    public static IResult Handle(CreateBook cmd)
    {
        // ...
    }
}
```

#### BS4003: Handler first parameter should be a command type
- **Severity**: Info (Suggestion)
- **Category**: CQRS

Wolverine routes messages based on the first parameter type, which should be from a `.Commands` namespace.

**❌ Suboptimal:**
```csharp
public static IResult Handle(string bookId)  // Should accept a command
{
    // ...
}
```

**✅ Good:**
```csharp
public static IResult Handle(CreateBook cmd)
{
    // ...
}
```

---

## Configuration

### Adjusting Rule Severity

You can configure rule severities in `.editorconfig`:

```ini
# Make BS2002 an error instead of warning
dotnet_diagnostic.BS2002.severity = error

# Disable BS4001 if you prefer different handler naming
dotnet_diagnostic.BS4001.severity = none
```

### Suppressing Rules

For specific cases where you need to suppress a rule:

```csharp
#pragma warning disable BS2002
public record SpecialRequest(string Data);  // Not in Commands namespace for a reason
#pragma warning restore BS2002
```

Or use attributes:

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("Architecture", "BS2002")]
public record SpecialRequest(string Data);
```

---

## Benefits

✅ **Consistent Architecture**: Enforces Event Sourcing and CQRS patterns across the codebase  
✅ **Early Detection**: Catches architectural violations during development  
✅ **Team Alignment**: Helps new developers follow established patterns  
✅ **Reduced Code Review**: Automated checks reduce manual review burden  
✅ **IDE Integration**: Real-time feedback in Visual Studio, VS Code, and Rider

---

## Testing

The analyzer includes comprehensive unit tests using actual C# files (not strings) for better maintainability. Tests are organized in `TestData` folders by diagnostic ID.

Run tests:
```bash
dotnet test --project src/BookStore.ApiService.Analyzers.Tests/BookStore.ApiService.Analyzers.Tests.csproj
```

---

## Further Reading

- [Event Sourcing Guide](event-sourcing-guide.md)
- [Marten Guide](marten-guide.md)
- [Wolverine Guide](wolverine-guide.md)
- [Architecture Overview](architecture.md)
