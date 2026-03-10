# Skills — BookStore Agent Capabilities

## Purpose

Reusable, step-by-step workflows that guide AI agents through complex tasks in the BookStore project. Each skill encodes best practices and architectural patterns.

## Available Skills (19 total)

### Aspire Runbooks

- **`aspire__start_solution`** - Launch the full Aspire stack locally.
- **`aspire__setup_mcp`** - Wire up the Aspire MCP server for agent telemetry.

### Wolverine Operations

- **`wolverine__guide`** - All write operations: CREATE (POST/start-stream), UPDATE (PUT/PATCH/append-event), DELETE (soft-delete/tombstone). Read `operations.md` for the relevant section.

### Marten Modeling

- **`marten__guide`** - All Marten tasks: event-sourced aggregates, projections (single-stream, multi-stream, composite, event), and query endpoints (get-by-id, paged list). Read `aggregate.md`, `projections.md`, or `queries.md` for the relevant section.

### Frontend & Realtime

- **`frontend__feature_scaffold`** - Create Blazor pages with ReactiveQuery + optimistic updates.
- **`frontend__debug_sse`** - Troubleshoot SSE and cache invalidation loops.

### Testing & Verification

- **`test__unit_suite`** - Run analyzer + API unit suites.
- **`test__integration_suite`** - Execute the Aspire-hosted integration suite.
- **`test__verify_feature`** - Build, format, and test gatekeeper.
- **`test__integration_scaffold`** - Scaffold integration tests with SSE verification.

### Deployment

- **`deploy__rollback`** - Roll back a faulty deployment.

### Operations & Cache

- **`ops__doctor_check`** - Validate dev tooling (dotnet, Docker, azd, kubectl).
- **`ops__rebuild_clean`** - Force a clean rebuild to fix flaky assets.
- **`cache__debug_cache`** - Diagnose HybridCache/Redis issues.

### Documentation & Language Patterns

- **`meta__cheat_sheet`** - Quick reference of stack rules and commands.
- **`lang__docfx_guide`** - Write DocFX-friendly guides.
- **`lang__logger_message`** - Add high-performance logging with LoggerMessage source generator.
- **`lang__problem_details`** - Add RFC 7807 ProblemDetails error responses with typed error codes.

## Usage

Skills are invoked using slash commands:

```
/wolverine__guide              # Create new mutation endpoint
/marten__guide                 # Build event-sourced aggregate or projection
/test__verify_feature          # Run all verification checks
/frontend__debug_sse           # Troubleshoot SSE issues
```

## Skill Structure

Each skill is a directory containing:
- `SKILL.md` - YAML frontmatter + markdown instructions
- `templates/` (optional) - Code templates referenced by the skill

## Creating New Skills

Create a new directory under `.claude/skills/<prefix>__<slug>/` with a `SKILL.md` file following the structure below. Follow naming conventions in `NAMING-CONVENTIONS.md`.

## License

All skills are licensed under the MIT License, matching the BookStore project license.

