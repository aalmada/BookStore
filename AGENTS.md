# BookStore — Agent Instructions

## Purpose
Use this file for agent-only context: build and test commands, conventions, and project patterns. For human-facing details, see README and docs.

## Quick Reference
- **Stack**: .NET 10, C# 14, Marten, Wolverine, HybridCache, Aspire
- **Solution**: `BookStore.slnx` (new .NET 10 solution format)
- **Common commands**: `dotnet restore`, `aspire run`, `dotnet test`, `dotnet format`
- **Docs**: `docs/getting-started.md`, `docs/guides/`
- **Testing instructions**: `tests/AGENTS.md`

## Repository Map
- `src/BookStore.ApiService/`: Event-sourced API (Marten + Wolverine)
- `src/BookStore.Web/`: Blazor frontend
- `src/BookStore.AppHost/`: Aspire orchestration
- `src/BookStore.Shared/`: Shared DTOs and models
- `src/BookStore.Client/`: Refit-based API client (generated from OpenAPI)
- `src/BookStore.ApiService.Analyzers/`: Roslyn analyzers enforcing rules
- `tests/`: Unit and integration tests per project
- `docs/`: architecture and guide material

## Major Patterns
- Modular monolith with event sourcing and CQRS
- Wolverine command/handler write model and async projections
- Marten projections for read models
- SSE for real-time UI updates
- Hybrid caching with explicit invalidation
- Multi-tenancy with tenant-aware routing and storage

## Development Process (TDD)
1. Define verification (test, command, or browser check)
2. Write verification first
3. Implement
4. Verify all steps pass
5. Run `dotnet format` to ensure code style compliance

**A feature is not complete until `dotnet format` has been executed successfully.**

## Code Rules (MUST follow)
```
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ record BookAdded(...)          ❌ record AddBook(...)
✅ namespace BookStore.X;         ❌ namespace BookStore.X { }
✅ [Test] async Task (TUnit)      ❌ [Fact] (xUnit)
✅ WaitForConditionAsync          ❌ Task.Delay / Thread.Sleep
```

## Conventions and Style
- Use `record` for DTOs/Commands/Events; events are past tense
- File-scoped namespaces only
- Follow `.editorconfig` and analyzer rules in `docs/guides/analyzer-rules.md`
- Central Package Management: versions live in `Directory.Packages.props`
- Shared build settings live in `Directory.Build.props`

## Common Mistakes
- Business logic in endpoints -> put it in aggregates/handlers
- Missing SSE notification -> add to `MartenCommitListener`
- Missing cache invalidation -> call `RemoveByTagAsync` after mutations

## Quick Troubleshooting
- Build failures: check BS1xxx-BS4xxx analyzer errors first
- SSE not working: run `/frontend__debug_sse`
- Cache issues: run `/cache__debug_cache`
- Environment issues: run `/ops__doctor_check`

## Documentation Index
- Setup: `docs/getting-started.md`
- Architecture: `docs/architecture.md`
- Event sourcing: `docs/guides/event-sourcing-guide.md`
- Marten: `docs/guides/marten-guide.md`
- Wolverine: `docs/guides/wolverine-guide.md`
- Caching: `docs/guides/caching-guide.md`
- Real-time notifications: `docs/guides/real-time-notifications.md`
- Testing: `docs/guides/testing-guide.md`, `docs/guides/integration-testing-guide.md`
- Contributing: `CONTRIBUTING.md`
