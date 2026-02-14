# AGENTS.md Best Practices

This file documents best practices for creating effective AGENTS.md files, based on research and real-world implementations.

## Core Principles

### 1. Conciseness (Keep Under 150 Lines)
AI agents load files into context windows. Excessive length wastes tokens and buries important information.

**Strategy**: Link to detailed documentation instead of inlining content.

❌ **Bad** (500 lines explaining event sourcing):
```markdown
## Event Sourcing
Event sourcing is a pattern where...
[detailed explanation]
[code examples]
[architecture diagrams]
```

✅ **Good** (2 lines + link):
```markdown
## Documentation Index
| Event Sourcing | `docs/guides/event-sourcing-guide.md` |
```

### 2. Commands First, Explanations Second
Lead with executable commands. Agents need actionable instructions, not context.

❌ **Bad**:
```markdown
To run the application, you need to start the Aspire host which orchestrates all services including PostgreSQL, Redis, and the API...
```

✅ **Good**:
```markdown
**Run**: `aspire run`
```

### 3. Skill References Over Detailed Steps
Defer complex workflows to skills. Keep AGENTS.md as a routing/index file.

❌ **Bad**:
```markdown
To add a new write operation:
1. Create a command record in Commands/
2. Create an event record in Events/
3. Add Apply method to aggregate
4. Create handler in Handlers/
5. Register projection...
```

✅ **Good**:
```markdown
**Scaffold**: `/wolverine__create_operation`
```

### 4. Good vs. Bad Pattern Examples
Show concrete examples side-by-side for clarity.

❌ **Bad**:
```markdown
Use Guid.CreateVersion7() for IDs.
Use DateTimeOffset.UtcNow for timestamps.
Events should be in past tense.
```

✅ **Good**:
```markdown
```
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ record BookAdded(...)          ❌ record AddBook(...)
```
```

### 5. Third-Person Voice
Use descriptive, objective language, not instructional.

❌ **Bad**:
```markdown
You should use .NET 10 for this project.
I will help you write code following PEP 8.
```

✅ **Good**:
```markdown
**Stack**: .NET 10, C# 14
**Code Style**: Follows PEP 8 standards
```

### 6. Never Include Sensitive Information
AGENTS.md is potentially public. Never include credentials, API keys, or proprietary secrets.

❌ **Bad**:
```markdown
DATABASE_URL=postgresql://admin:MyP@ssw0rd@prod-db.company.com/maindb
API_KEY=sk_live_abc123xyz789
```

✅ **Good**:
```markdown
**Secrets Management**:
- Production: AWS Secrets Manager (`prod/*`)
- Development: `.env` file (gitignored, copy from `.env.example`)
```

## Recommended Structure

```markdown
# {Project} — Agent Instructions

## Quick Reference
- **Stack**: {technologies + versions}
- **Docs**: {key documentation paths}
- **Run**: {start} | **Test**: {test} | **Format**: {format}

## Key Rules (MUST follow)
```
✅ {good}                         ❌ {bad}
```

## Common Mistakes
- ❌ {mistake} → {solution/skill}

## Project Layout
| Path | Purpose |

## Skills
| Category | Skills |

## Quick Troubleshooting
- **{Problem}**: {Solution}

## Documentation Index
| Topic | Guide |
```

## Hierarchical Organization for Monorepos

Use nested AGENTS.md files when root exceeds 150 lines:

```
project/
├── AGENTS.md                    # Organization-wide standards
├── src/
│   └── ApiService/
│       └── AGENTS.md           # Backend-specific guidance
├── infrastructure/
│   └── AGENTS.md               # IaC-specific guidance
└── tests/
    └── AGENTS.md               # Testing-specific guidance
```

**Rules**:
- Root: General practices, project overview
- Subdirectory: Technology-specific details
- Nearest file takes precedence (hierarchical override)

## File-Scoped Operations (Prefer Fast Feedback)

Enable quick iteration by providing file-scoped commands:

❌ **Bad** (forces full builds):
```markdown
## Testing
Always run: `dotnet test`
```

✅ **Good** (allows targeted testing):
```markdown
## Testing
- **Single test**: `dotnet test --filter ClassName`
- **Project**: `dotnet test tests/BookStore.ApiService.UnitTests/`
- **Full suite**: `dotnet test` (only when explicitly requested)
```

## Permission Boundaries

Define what agents can do autonomously vs. what requires approval:

```markdown
## Permissions

### Allowed Without Prompting
- Read any source file
- Run linters/formatters on single files
- Run unit tests on specific test files

### Require Approval First
- Installing packages (`dotnet add package`)
- Git operations (`git push`, `git commit`)
- Deleting files or directories
- Running full test suite
- Infrastructure changes
```

## Maintenance

### When to Update
- Pull requests that change workflows or commands
- New skills added
- Technology stack upgrades
- New project conventions established

### Quarterly Audit Checklist
- [ ] All commands still work as written
- [ ] All skill references point to existing skills
- [ ] All documentation links are valid
- [ ] No outdated or deprecated information
- [ ] File size still under 150 lines
- [ ] No sensitive information present

## Testing Your AGENTS.md

Ask an AI agent to perform common tasks using only your AGENTS.md:

1. **Setup**: "Set up this project for development"
2. **Run**: "Start the application"
3. **Test**: "Run tests for [specific feature]"
4. **Add Feature**: "Add a new [entity/endpoint/page]"
5. **Debug**: "Why aren't real-time updates working?"

If the agent gets stuck, needs clarification, or makes mistakes, update AGENTS.md accordingly.

## Common Anti-Patterns

### ❌ Anti-Pattern 1: Massive Files (1000+ lines)
Wastes tokens, buries information. Split into subdirectory files or link to detailed docs.

### ❌ Anti-Pattern 2: Vague Instructions
"Run the tests" vs. "Run `dotnet test tests/BookStore.ApiService.UnitTests/`"

### ❌ Anti-Pattern 3: No Skill Integration
Explaining detailed workflows instead of referencing `/wolverine__create_operation`, `/frontend__debug_sse`, etc.

### ❌ Anti-Pattern 4: Outdated Information
Last updated 2 years ago, references deprecated tools/patterns.

### ❌ Anti-Pattern 5: No Good/Bad Examples
Agents may copy legacy code without explicit anti-pattern warnings.

### ❌ Anti-Pattern 6: Time-Sensitive Content
"Deploy on Friday" or "Use version 9 (version 10 releases next month)"

### ❌ Anti-Pattern 7: Forcing Full Builds
No file-scoped commands, every check runs entire test suite.

## References

- [AGENTS.md Standard](https://gist.github.com/0xfauzi/7c8f65572930a21efa62623557d83f6e) - Comprehensive best practices guide
- [OpenAI Codex Documentation](https://platform.openai.com/docs) - How OpenAI uses AGENTS.md
- [GitHub Copilot Instructions](https://docs.github.com/en/copilot) - Native AGENTS.md support
