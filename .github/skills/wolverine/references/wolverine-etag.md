# Optimistic Concurrency (ETags)

ETags are a recommended pattern for optimistic concurrency in update/delete operations, but are not required by Wolverine. You may use ETags, version numbers, or other mechanisms as appropriate for your application.

## ETag Example (Recommended)
```csharp
public record UpdateBook(Guid Id, string Title) { public string? ETag { get; init; } }

public static async Task<IResult> Handle(UpdateBook command, IDocumentSession session, HttpContext context)
{
    var streamState = await session.Events.FetchStreamStateAsync(command.Id);
    if (streamState == null) return Results.NotFound();
    var currentETag = ETagHelper.GenerateETag(streamState.Version);
    if (!ETagHelper.CheckIfMatch(context, currentETag))
        return ETagHelper.PreconditionFailed();
    // ...
    return Results.NoContent();
}
```

## Other Approaches
- Use version numbers or timestamps for concurrency checks
- For simple scenarios, you may omit concurrency checks entirely

See also: [wolverine-testing.md](wolverine-testing.md)
