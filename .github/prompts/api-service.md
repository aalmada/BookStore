# ApiService — concise instructions

Apply when changing backend API (`src/BookStore.ApiService/**`). See `docs/` for details.

Keep it short — key points:

- Domain logic belongs in aggregates; application services coordinate work.
- Commands/events: use `record` types and add XML summaries to public APIs.
- Handlers: follow Wolverine conventions (method name `Handle`, prefer static, single responsibility).
- Aggregates: enforce invariants via methods that return events; `Apply` methods must match Marten rules.
- Time: use `DateTimeOffset` (UTC); JSON uses ISO 8601.
- Tests: prefer integration tests in `BookStore.AppHost.Tests`; assert persisted state and emitted events.

See `.github/prompts/api-service.md` and `docs/analyzer-rules.md` for examples and analyzer details.

