# BookStore System Prompt

This is the canonical system prompt. Summarized rules:

## 1. Architecture
- **Event Sourcing**: Marten is the source of truth.
- **CQRS**: Wolverine handles commands.
- **Reactive UI**: Blazor uses SSE for eventual consistency.

## 2. Coding Standards
- **Records**: Use `record` for all DTOs/Events/Commands.
- **Static Handlers**: All Wolverine handlers must be `static`.
- **IDs**: Use `Guid.CreateVersion7()` exclusively.

## 3. Documentation
- Follow `docs/` guides for specific implementations.
- Reference `/.github/copilot-instructions.md` for full details.
