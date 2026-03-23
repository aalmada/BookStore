# Agent-Scoped Hooks In Frontmatter

Use this when hooks should apply to one agent only.

## Example frontmatter block

```yaml
---
name: SecurityReviewer
description: Review changes and block risky tool use.
target: vscode
tools: ['search', 'read', 'edit', 'runCommands']
hooks:
  PreToolUse:
    - type: command
      command: python3 ./.github/agents/hooks/security_pretool.py
      timeout: 10
  PostToolUse:
    - type: command
      command: python3 ./.github/agents/hooks/audit_posttool.py
      timeout: 10
---
```

## Notes

1. Keep hooks narrow at the agent level. Global policy belongs in repository-level JSON.
2. For this Copilot-specific skill, prefer Copilot event casing such as `PreToolUse` and `PostToolUse`.
3. Prefer a single base `command` for all platforms.
4. Prefer `python3` for cross-platform script execution.
5. Use `timeout` in runtime-style configs.
6. If you must support legacy syntax (`bash`, `powershell`, `timeoutSec`), document that explicitly in companion docs.
7. If you also target Claude or another runtime, document lowercase event variants separately.
8. Agent-scoped hooks are ideal for specialized workflows (for example, stricter reviewer agents).

## Layering strategy

1. Repository-level hooks enforce baseline policy.
2. Agent frontmatter hooks add role-specific policy.
3. Avoid duplicating the same deny logic in both layers unless required.
