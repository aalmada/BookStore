---
name: copilot-hooks
description: >
  Build, review, and debug GitHub Copilot hook configurations for both repository-level JSON
  hooks and agent-scoped frontmatter hooks. Use this skill whenever a user mentions Copilot
  hooks, PreToolUse/PostToolUse policies, session hooks, audit logging, tool allow/deny
  enforcement, CLI hooks, VS Code agent hooks, or asks about hook payloads and configuration
  syntax. Use it even if the user does not explicitly ask for a "skill" or "hooks config"
  file.
---

# Copilot Hooks Skill

Use this skill to design and implement robust hook systems for GitHub Copilot.

This skill is intentionally split across reference files. Read only what you need:

| File | When to read it |
|---|---|
| `references/quickstart.md` | You need a fast path from request to working config |
| `references/config-by-surface.md` | You need JSON config syntax for repo-level hooks |
| `references/agent-frontmatter.md` | You need agent-scoped hooks in `.agent.md` frontmatter |
| `references/hook-events.md` | You need event names, behavior, and allow/deny capabilities |
| `references/payloads.md` | You need exact input/output payload contracts |
| `references/compatibility.md` | You need VS Code vs CLI support mapping |
| `references/examples.md` | You need copy-ready examples and composition patterns |

## Workflow

1. Identify the execution surface first:
   - Repository-level hooks JSON
   - Agent-scoped hooks in frontmatter
   - Both (global guardrails plus agent-specific behavior)
2. Pick the event set from `references/hook-events.md`.
3. Build configuration using `references/config-by-surface.md` and `references/agent-frontmatter.md`.
4. Validate payload assumptions against `references/payloads.md`.
5. Add timeouts and fail-safe behavior (deny only when confident).
6. Keep scripts short and deterministic. Hooks are synchronous and block execution.

## Output expectations

When creating or editing hooks, produce:

1. Configuration files with clear event grouping.
2. Script stubs or script updates for each hook.
3. A short compatibility note (what works in VS Code, CLI, or both).
4. A payload contract summary for each configured event.

## Guardrails

- Prefer explicit, reviewable logic over opaque shell one-liners.
- Prefer a single `command` by default.
- Prefer `python3` in command examples for cross-platform usage.
- Do not assume one runtime payload shape; support documented and observed variants.
- For deny decisions, always include a human-readable reason.
- Avoid project-specific naming unless the user explicitly requests it.
