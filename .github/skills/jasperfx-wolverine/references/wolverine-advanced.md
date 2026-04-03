# Wolverine Advanced

## Async Messaging
- Use Wolverine for background jobs, scheduled messages, and distributed workflows
- Handlers can return multiple outputs (cascading messages)

## Background Jobs
- Schedule jobs using Wolverine's message bus
- Use recurring jobs for periodic tasks

## Cascading Messages
- Handlers can return tuples (e.g., `(IResult, SendEmail)`)
- Wolverine dispatches additional messages after transaction commit

## Configuration
- Use `AddWolverine()` and `opts.Policies.AutoApplyTransactions()`
- Discover handlers with `opts.Discovery.IncludeAssembly()`

See also: [wolverine-marten.md](wolverine-marten.md)
