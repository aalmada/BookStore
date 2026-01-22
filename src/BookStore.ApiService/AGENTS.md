# ApiService Instructions

**Scope**: `src/BookStore.ApiService/**`

## Guides
- `docs/guides/event-sourcing-guide.md` - Event sourcing patterns
- `docs/guides/marten-guide.md` - Marten queries & projections
- `docs/guides/wolverine-guide.md` - Command handling
- `docs/guides/caching-guide.md` - HybridCache patterns
- `docs/guides/real-time-notifications.md` - SSE setup
- `docs/guides/analyzer-rules.md` - BS1xxx-BS4xxx rules

## Skills
- `/scaffold-write` - Add command endpoint
- `/scaffold-read` - Add query endpoint
- `/scaffold-aggregate` - Create aggregate
- `/scaffold-projection` - Create projection
- `/debug-sse` - Debug real-time updates
- `/debug-cache` - Debug caching

## Rules
- Domain logic in aggregates; handlers coordinate and persist
- Use `record` types for Commands/Events
- `Apply` methods must be void with single event parameter
- `DateTimeOffset` (UTC) for timestamps
