# Agent Development Guide

This guide explains the **agent configuration system** used in the BookStore project and how to work with it effectively.

## What is Agent Configuration?

The BookStore project uses a structured approach to help AI coding assistants (agents) understand project conventions and automate common tasks. This system consists of:

1. **AGENTS.md Files** - Context-aware guidance documents distributed throughout the codebase
2. **Claude Skills** - Reusable automation workflows with step-by-step instructions
3. **Roslyn Analyzers** - Compile-time enforcement of architectural patterns

Together, these components ensure agents work consistently with established patterns without needing to ask basic questions or make architectural mistakes.

---

## How AGENTS.md Files Work

### The Concept

Instead of a single monolithic instruction file, the BookStore project uses **distributed, scope-specific** AGENTS.md files. Each file provides guidance relevant to its location in the codebase.

When an agent needs to work in a particular area (e.g., adding a new API endpoint), it reads the relevant AGENTS.md file to understand:
- What patterns to follow
- What pitfalls to avoid
- What documentation to reference for deeper details

### File Distribution

```
BookStore/
├── AGENTS.md                    # Global conventions (namespaces, IDs, timestamps)
├── src/
│   ├── ApiService/...
│   │   └── AGENTS.md            # Backend-specific (Event Sourcing, SSE, localization)
│   ├── Web/...
│   │   └── AGENTS.md            # Frontend-specific (ReactiveQuery, optimistic updates)
│   ├── Client/...
│   │   └── AGENTS.md            # Client SDK patterns (Refit, aggregation)
│   └── Shared/...
│       └── AGENTS.md            # Shared contracts (DTOs, notifications)
└── tests/
    ├── BookStore.AppHost.Tests/...
    │   └── AGENTS.md            # Integration testing (TUnit, SSE verification)
    └── ApiService/...
        └── AGENTS.md            # Unit testing patterns
```

**Philosophy**: An agent modifying `src/ApiService/` only needs to know ApiService conventions, not frontend patterns. This reduces cognitive load and keeps guidance focused.

### When Agents Use AGENTS.md

Agents automatically reference AGENTS.md files when:
- Starting work in a new directory
- Implementing a new feature
- Following established patterns
- Needing context about architectural decisions

---

## How Claude Skills Work

### The Concept

Skills are **executable workflows** that guide agents through multi-step processes. Instead of remembering every step to scaffold a feature, agents invoke a skill that provides a checklist.

Skills live in `.claude/skills/{skill-name}/SKILL.md` and contain:
- YAML frontmatter (name, description)
- Numbered step-by-step instructions
- Optional templates for code generation
- Turbo annotations for safe auto-execution

### Invoking Skills

Users or agents invoke skills using slash commands:

```
/scaffold-write      # Add a new mutation/command endpoint
/scaffold-read       # Add a new query endpoint
/verify-feature      # Run build, format check, and tests
/doctor              # Check development environment
```

The agent then follows the skill's instructions step-by-step.

### Skill Categories

**Scaffolding Skills**:
- `/scaffold-write` - Backend write operations (commands, events, projections)
- `/scaffold-read` - Backend read operations (queries, caching, localization)
- `/scaffold-frontend-feature` - Frontend features (components, state management)
- `/scaffold-skill` - Meta-skill to create new skills

**Verification Skills**:
- `/verify-feature` - Comprehensive verification (build + format + tests)
- `/run-integration-tests` - Integration test suite
- `/run-unit-tests` - Unit test suites

**Utility Skills**:
- `/doctor` - Environment health check
- `/rebuild-clean` - Clean build
- `/deploy-to-azure` - Azure deployment

### Creating New Skills

The project includes `/scaffold-skill` to create new workflows. This ensures consistency in skill structure across the project.

---

## Working with the Agent System

### For Developers Using Agents

When working with an AI agent on this project:

1. **Let the agent read AGENTS.md** - They provide context the agent needs
2. **Use skills for common tasks** - Don't write manual steps when a skill exists
3. **Trust the analyzers** - Build warnings (BS1xxx-BS4xxx) indicate pattern violations
4. **Verify with `/verify-feature`** - Ensures build, format, and tests pass

### For Developers Adding to the System

When extending the agent configuration:

1. **Update AGENTS.md when patterns change** - Keep them current with architectural decisions
2. **Create skills for repeated workflows** - If you do it more than twice, make a skill
3. **Use templates in skills** - Reduces boilerplate and ensures consistency
4. **Add turbo annotations carefully** - Only for truly safe, idempotent commands

---

## Key Workflows

### Adding a Backend Feature

```
1. /scaffold-write         → Creates command, handler, events, SSE setup
2. /scaffold-read          → Creates query, projection, caching
3. /verify-feature         → Validates everything works
```

The agent reads the relevant AGENTS.md files during each skill to understand patterns like:
- How to structure events (from ApiService AGENTS.md)
- How to enable SSE notifications (from ApiService AGENTS.md)
- How to implement caching (from ApiService AGENTS.md)

### Adding a Frontend Feature

```
1. /scaffold-frontend-feature    → Creates component, state management
2. Update QueryInvalidationService (manual step, guided by Web AGENTS.md)
3. /verify-feature               → Validates build and tests
```

The Web AGENTS.md explains ReactiveQuery patterns, optimistic updates, and SSE integration.

### Troubleshooting

```
/doctor              → Environment check
/rebuild-clean       → Clean build if issues persist
```

---

## Relationship to Other Documentation

The agent system complements (but doesn't replace) comprehensive documentation:

| System | Purpose |
|--------|---------|
| **AGENTS.md** | Quick reference, "just enough" for agents to work correctly |
| **docs/** guides | Deep dives for humans learning the architecture |
| **Analyzer Rules** | Compile-time enforcement of patterns |
| **Skills** | Step-by-step workflows for common tasks |

**Example**: 
- **Event Sourcing Guide** (docs/) explains *why* and *how* Event Sourcing works
- **ApiService AGENTS.md** reminds agents to use `DateTimeOffset` and past-tense event names
- **BS1xxx analyzers** enforce events as records with immutable properties
- **/scaffold-write skill** provides the exact steps to implement a new command

---

## Benefits of This Approach

### For AI Agents
✅ Context-aware guidance without needing to ask clarifying questions  
✅ Consistent patterns across all work  
✅ Automation of repetitive scaffolding tasks  
✅ Real-time feedback via analyzers  

### For Developers
✅ Onboarding agents to the project is automatic  
✅ Skills codify institutional knowledge  
✅ Reduces back-and-forth with AI assistants  
✅ Maintains architectural consistency  

### For the Codebase
✅ Self-documenting project structure  
✅ Enforced patterns via analyzers  
✅ Easy to extend with new skills and guidance  

---

## Further Reading

- [Getting Started](getting-started.md) - Setting up the development environment
- [Architecture Overview](architecture.md) - High-level system design
- [Testing Guide](testing-guide.md) - Testing philosophy and patterns
- [Analyzer Rules](analyzer-rules.md) - Complete analyzer reference
