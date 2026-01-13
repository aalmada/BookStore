```instructions
# BookStore — concise instructions

Purpose: short, authoritative guidance for agents working in this repository. For full details see `/docs/*` and `.github/prompts/`.

Repo summary
- Full-stack .NET 10 application: event-sourced ASP.NET Core API, Blazor frontend, Aspire orchestration. See `docs/getting-started.md` and `README.md`.

Build & run (verified)
- Restore: `dotnet restore`
- Run app locally (recommended): `aspire run` (starts API, Web, PostgreSQL, PgAdmin)
- Run tests: `dotnet test` (or `dotnet test --project <path>` for specific projects)

Key coding rules (short)
- Use file-scoped namespaces: `namespace BookStore.Namespace;`.
- Prefer `record` types for DTOs, Commands, and Events; enable nullable reference types.
- Use `DateTimeOffset` (UTC) and ISO 8601 for all timestamps.
- JSON: camelCase properties; enums serialized as strings.
- IDs: prefer `Guid.CreateVersion7()` (UUIDv7) where applicable.
- Follow analyzer rules in `docs/analyzer-rules.md` (events, commands, apply methods, handlers).

Testing & validation
- Prefer integration tests in `BookStore.AppHost.Tests`; name tests descriptively and assert specific properties.
- Run `dotnet test` and ensure CI workflows pass (`.github/workflows/ci.yml`).

Where to look for details
- Code layout and guides: `docs/` (getting-started, architecture, api-conventions, testing-guide, analyzer-rules).
- Task templates and agent prompts: `.github/prompts/`.
- Path-specific short rules: `.github/instructions/*.instructions.md`.

Usage
- Agents should trust these instructions and consult docs only when needed. Keep changes small and run tests locally before proposing PRs.

```
