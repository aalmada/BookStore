# BookStore — Agent Instructions

## Purpose
Short, authoritative guidance for agents working in this repository.

## Repo Summary
- **Stack**: Full-stack .NET 10 application: event-sourced ASP.NET Core API, Blazor frontend, Aspire orchestration.
- **Documentation**: See `docs/getting-started.md` and `README.md`.

## Architecture Highlights
- **Event Sourcing**: Domain events stored via Marten; aggregates rehydrated from event streams.
- **Real-time Updates**: Server-Sent Events (SSE) for mutation notifications to connected clients.
- **Aspire**: Orchestration for API, Web, PostgreSQL, and PgAdmin resources.
- **Localization**: Multi-language support for content and multi-currency for prices.

## Build & Run
- **Restore**: `dotnet restore`
- **Run app (recommended)**: `aspire run` (starts API, Web, PostgreSQL, PgAdmin)
- **Run tests**: `dotnet test`
- **Format code**: `dotnet format`

## Key Coding Rules
- **Namespaces**: Use file-scoped namespaces: `namespace BookStore.Namespace;`.
- **DTOs/Commands/Events**: Prefer `record` types; enable nullable reference types.
- **Timestamps**: Use `DateTimeOffset` (UTC) and ISO 8601.
- **JSON**: camelCase properties; enums serialized as strings.
- **IDs**: Use `Guid.CreateVersion7()` (UUIDv7) where applicable.
- **Analyzer Rules**: Follow `docs/analyzer-rules.md` (events, commands, apply methods, handlers).

## Testing
- **Integration Tests**: Prefer `BookStore.AppHost.Tests`; name tests descriptively and assert specific properties.
- **CI**: Ensure `dotnet test` passes locally.

## Project Structure
- `src/ApiService/BookStore.ApiService`: Backend API (Domain, Aggregates, Commands, Events, Projections).
- `src/Web/BookStore.Web`: Blazor Frontend.
- `src/Client/BookStore.Client`: API Client / SDK.
- `src/Shared/BookStore.Shared`: Shared contracts, DTOs, and notification models.
- `src/BookStore.AppHost`: Aspire orchestration.

## Agent Skills
Use Claude skills for common tasks:
- `/scaffold-write` - Add new command/mutation endpoint
- `/scaffold-read` - Add new query endpoint
- `/scaffold-frontend-feature` - Add Blazor feature with reactive state
- `/verify-feature` - Run build, format, and tests
- `/doctor` - Check development environment
- `/deploy-to-azure` - Deploy to Azure using azd
