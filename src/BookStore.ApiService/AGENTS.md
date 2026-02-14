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
- `/wolverine__create_operation` - Add new command endpoint
- `/wolverine__update_operation` - Append events to existing aggregates
- `/wolverine__delete_operation` - Implement delete/tombstone endpoints
- `/marten__aggregate_scaffold` - Create event-sourced aggregates
- `/marten__get_by_id` - Add cached GET-by-id queries
- `/marten__list_query` - Add paginated list queries
- `/marten__single_stream_projection` - Build per-stream projections
- `/marten__multi_stream_projection` - Aggregate multiple streams
- `/marten__composite_projection` - Chain projections for reuse/perf
- `/marten__event_projection` - Emit documents per event
- `/frontend__debug_sse` - Debug SSE and reactive updates
- `/cache__debug_cache` - Debug caching issues

## Rules
- Domain logic in aggregates; handlers coordinate and persist
- Use `record` types for Commands/Events
- `Apply` methods must be void with single event parameter
- `DateTimeOffset` (UTC) for timestamps
