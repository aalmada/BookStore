# Service Defaults Instructions

**Scope**: `src/BookStore.ServiceDefaults/**`

## Guides
- `docs/guides/logging-guide.md` - Logging patterns
- `docs/guides/performance-guide.md` - Performance monitoring
- `docs/guides/aspire-guide.md` - Service discovery

## Rules
- Cross-cutting concerns: OpenTelemetry, health checks, resilience
- Keep `Extensions.cs` focused on shared infrastructure
- Aspire auto-applies ServiceDefaults to all projects
- Add here only what applies to **both** ApiService and Web

