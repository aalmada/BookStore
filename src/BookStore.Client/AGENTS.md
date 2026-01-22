# Client SDK Instructions

**Scope**: `src/BookStore.Client/**`

## Guides
- `docs/guides/api-client-generation.md` - Client generation
- `docs/guides/api-conventions-guide.md` - API conventions

## Rules
- Use Refit for API definitions with `record` DTOs
- **Interface Aggregation**: Each endpoint as interface, aggregated in `IBooksClient` etc.
- Use `AddBookStoreClient` for DI registration
- Use `AddBookStoreEvents` for SSE via `BookStoreEventsService`
