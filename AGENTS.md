# BookStore — Agent Instructions

## Purpose

Use this file for agent-only context: build and test commands, conventions, and project patterns. For human-facing details, see README and docs.

## Quick Reference

- **Stack**: .NET 10, C# 14, Marten, Wolverine, HybridCache, Aspire, Playwright
- **Solution**: `BookStore.slnx` (new .NET 10 solution format)
- **Common commands**: `dotnet restore`, `aspire run`, `dotnet test`, `dotnet format`
- **Docs**: `docs/getting-started.md`, `docs/guides/`
- **Testing instructions**: `tests/AGENTS.md`

### Running Tests (TUnit)

TUnit-specific arguments must be passed after `--` so they are forwarded as program arguments rather than parsed by `dotnet test`:

```bash
# Run all tests (uses all available cores by default)
dotnet test

# Limit parallelism in resource-constrained environments
dotnet test -- --maximum-parallel-tests 4

# Filter tests by category
dotnet test -- --treenode-filter "/*/*/*/*[Category=Integration]"
```

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
5. Run `dotnet format` to ensure code style compliance. Issues not automatically fixed must be resolved manually.

**A feature is not complete until `dotnet format --verify-no-changes` exits with code 0.**

## Code Rules (MUST follow)

```text
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ record BookAdded(...)          ❌ record AddBook(...)
✅ namespace BookStore.X;         ❌ namespace BookStore.X { }
✅ [Test] async Task (TUnit)      ❌ [Fact] (xUnit)
✅ WaitForConditionAsync          ❌ Task.Delay / Thread.Sleep
✅ [LoggerMessage(...)]           ❌ _logger.LogInformation(...) / LogWarning / LogError / etc.
✅ MultiTenancyConstants.*        ❌ Hardcoded "*DEFAULT*" / "default"
```

### Logging Pattern

**MUST use LoggerMessage source generator for ALL logging.** See `/lang__logger_message` skill for details, templates, and examples.

### Error Handling Pattern

**MUST use Result pattern with typed Error codes and ProblemDetails.** See `/lang__problem_details` skill for error types, status codes, and usage.

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
- Playwright browser missing (integration tests fail with browser launch error): install browsers with `node tests/BookStore.AppHost.Tests/bin/Debug/net10.0/.playwright/package/index.js install chromium` (build the project first)

## MCP Servers for Documentation

Use MCP servers to get up-to-date documentation instead of relying on training data:

- **Context7** (`mcp_context7_resolve-library-id` → `mcp_context7_get-library-docs`): Use for any library in the stack — Marten, Wolverine, Aspire, Refit, Bogus, TUnit, etc.
- **Microsoft Learn** (`mcp_microsoftdocs_microsoft_docs_search` / `mcp_microsoftdocs_microsoft_docs_fetch`): Use for .NET, ASP.NET Core, Entity Framework, Azure, and any Microsoft technology.

Always prefer MCP server lookups over guessing API shapes or behaviour from training data.

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
