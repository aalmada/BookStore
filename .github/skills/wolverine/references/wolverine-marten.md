# Wolverine + Marten Integration

## Event Sourcing
- Use Marten for event sourcing, aggregates, and projections
- Integrate with Wolverine using `.IntegrateWithWolverine()`
- Wolverine manages Marten sessions and commits automatically

## Projections
- Async projections are managed by Wolverine (not Marten daemon)
- Use `IProjection` or `SingleStreamProjection` for read models

## Patterns
- No manual `SaveChangesAsync()` calls
- Use `Guid.CreateVersion7()` for IDs

See also: [wolverine-etag.md](wolverine-etag.md)
