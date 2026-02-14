# Skills — BookStore Agent Capabilities

## Purpose

Reusable, step-by-step workflows that guide AI agents through complex tasks in the BookStore project. Each skill encodes best practices and architectural patterns.

## Available Skills (28 total)

### Aspire Runbooks

- **`aspire__start_solution`** - Launch the full Aspire stack locally.
- **`aspire__setup_mcp`** - Wire up the Aspire MCP server for agent telemetry.

### Wolverine Operations

- **`wolverine__create_operation`** - Scaffold POST commands (event streams, HTTP endpoints).
- **`wolverine__update_operation`** - Add mutations that append events to existing streams.
- **`wolverine__delete_operation`** - Implement delete/tombstone workflows.

### Marten Modeling

- **`marten__aggregate_scaffold`** - Create event-sourced aggregates with Apply methods.
- **`marten__get_by_id`** - Add cached GET endpoints for documents.
- **`marten__list_query`** - Build paged list queries with filters and caching.
- **`marten__single_stream_projection`** - Project a single stream into a read model.
- **`marten__multi_stream_projection`** - Aggregate multiple streams (dashboards, rollups).
- **`marten__composite_projection`** - Chain projections for throughput/reuse.
- **`marten__event_projection`** - Emit documents per event for side tables.

### Frontend & Realtime

- **`frontend__feature_scaffold`** - Create Blazor pages with ReactiveQuery + optimistic updates.
- **`frontend__debug_sse`** - Troubleshoot SSE and cache invalidation loops.

### Testing & Verification

- **`test__unit_suite`** - Run analyzer + API unit suites.
- **`test__integration_suite`** - Execute the Aspire-hosted integration suite.
- **`test__verify_feature`** - Build, format, and test gatekeeper.
- **`test__integration_scaffold`** - Scaffold integration tests with SSE verification.

### Deployment

- **`deploy__azure_container_apps`** - Ship via azd to Azure Container Apps.
- **`deploy__kubernetes_cluster`** - Apply Aspire manifests to Kubernetes.
- **`deploy__rollback`** - Roll back a faulty deployment.

### Operations & Cache

- **`ops__doctor_check`** - Validate dev tooling (dotnet, Docker, azd, kubectl).
- **`ops__rebuild_clean`** - Force a clean rebuild to fix flaky assets.
- **`cache__debug_cache`** - Diagnose HybridCache/Redis issues.

### Documentation & Meta

- **`meta__cheat_sheet`** - Quick reference of stack rules and commands.
- **`meta__create_skill`** - Scaffold new skills with templates and linting.
- **`doc__write_agents_md`** - Author AGENTS.md files that delegate to skills.
- **`lang__docfx_guide`** - Write DocFX-friendly guides.

## Usage

Skills are invoked using slash commands:

```
/wolverine__create_operation   # Create new mutation endpoint
/marten__aggregate_scaffold    # Build event-sourced aggregate
/test__verify_feature          # Run all verification checks
/frontend__debug_sse           # Troubleshoot SSE issues
```

## Skill Structure

Each skill is a directory containing:
- `SKILL.md` - YAML frontmatter + markdown instructions
- `templates/` (optional) - Code templates referenced by the skill

## Creating New Skills

Use `/meta__create_skill` to create new agent capabilities following the established pattern.

## License

All skills are licensed under the MIT License, matching the BookStore project license.

