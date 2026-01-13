---
applyTo: "src/BookStore.ApiService/**"
---

# ApiService path-specific instructions

Apply to: `src/BookStore.ApiService/**` (short reference).

Keep changes minimal and follow these essentials:

- Business logic belongs inside aggregates; application layer coordinates and persists events.
- Use `record` for Commands and Events; add XML summaries to public APIs.
- Handlers: follow Wolverine conventions (`Handle`, prefer static) and keep single responsibility.
- Aggregates: return events from behavior methods; `Apply` methods must follow Marten conventions (void, single parameter).
- Use `DateTimeOffset` (UTC) and ISO 8601 for JSON.
- Prefer integration tests (`BookStore.AppHost.Tests`) that assert persisted state and emitted events.

See `docs/analyzer-rules.md` and `.github/prompts/api-service.md` for examples.
