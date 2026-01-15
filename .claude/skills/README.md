# Skills — BookStore Agent Capabilities

## Purpose

Reusable, step-by-step workflows that guide AI agents through complex tasks in the BookStore project. Each skill encodes best practices and architectural patterns.

## Available Skills (17 total)

### Scaffolding Workflows

- **`scaffold-write`** - Create new write operations (commands, events, handlers, endpoints) with Event Sourcing patterns
- **`scaffold-read`** - Create new read operations (queries, projections, caching) with CQRS patterns  
- **`scaffold-frontend-feature`** - Create Blazor components with ReactiveQuery, optimistic updates, and SSE integration
- **`scaffold-aggregate`** - Create event-sourced aggregates with proper Apply methods and Marten configuration
- **`scaffold-projection`** - Create Marten read model projections for optimized querying
- **`scaffold-test`** - Create integration tests with SSE verification and TUnit patterns
- **`scaffold-skill`** - Meta-skill for creating new agent skills

### Verification & Testing

- **`verify-feature`** - Comprehensive verification: build, format check, and all tests
- **`run-integration-tests`** - Execute full integration test suite with Aspire
- **`run-unit-tests`** - Execute API service and analyzer unit tests

### Debugging & Troubleshooting

- **`debug-sse`** - Troubleshoot Server-Sent Events (SSE) notification issues with step-by-step checklist
- **`debug-cache`** - Troubleshoot HybridCache and Redis caching issues with verification guide

### Deployment & Operations

- **`deploy-to-azure`** - Deploy application to Azure Container Apps using Azure Developer CLI (azd)
- **`deploy-kubernetes`** - Deploy to Kubernetes cluster using Aspire-generated manifests
- **`rollback-deployment`** - Rollback failed deployment to previous working version

### Utilities

- **`doctor`** - Check development environment (.NET, Docker, azd, kubectl, Aspire workload)
- **`rebuild-clean`** - Clean build from scratch to resolve transient errors

## Usage

Skills are invoked using slash commands:

```
/scaffold-write      # Create new mutation endpoint
/scaffold-aggregate  # Create event-sourced aggregate
/verify-feature      # Run all verification checks
/debug-sse           # Troubleshoot SSE issues
```

## Skill Structure

Each skill is a directory containing:
- `SKILL.md` - YAML frontmatter + markdown instructions
- `templates/` (optional) - Code templates referenced by the skill

## Creating New Skills

Use `/scaffold-skill` to create new agent capabilities following the established pattern.

## License

All skills are licensed under the MIT License, matching the BookStore project license.

