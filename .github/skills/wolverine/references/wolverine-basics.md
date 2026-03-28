# Wolverine Basics

## What is Wolverine?
Wolverine is a .NET framework for command/handler patterns, message bus, and async messaging. It enables clean separation of business logic, automatic transaction management, and easy testing.

- Use immutable `record` types for commands
- Handlers are `public static` methods named `Handle`
- Endpoints route to handlers via `IMessageBus.InvokeAsync()`

## Command/Handler Pattern
- **Command**: Immutable record representing user intent
- **Handler**: Pure function with business logic
- **Endpoint**: Thin HTTP layer, creates command and invokes handler

## Handler Discovery
- Method name: `Handle`
- First parameter: Command type
- Return type: Determines behavior (e.g., `IResult` for HTTP)
- Additional parameters: Auto-injected (e.g., `IDocumentSession`, `ILogger`)

## Example
```csharp
public record CreateBook(string Title);

public static class BookHandlers
{
    public static IResult Handle(CreateBook command, IDocumentSession session)
    {
        // Business logic
        session.Events.StartStream<BookAggregate>(command.Id, ...);
        return Results.Created(...);
    }
}
```

See also: [wolverine-advanced.md](wolverine-advanced.md)
