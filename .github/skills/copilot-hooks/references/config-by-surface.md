# Configuration By Surface

This file documents configuration styles by runtime surface.

## Repository-level JSON hooks

Use JSON files under `.github/hooks/`.

Preferred runtime shape (single cross-platform command):

```json
{
  "hooks": {
    "SessionStart": [
      {
        "type": "command",
        "command": "python3 .github/hooks/scripts/session_start.py",
        "timeout": 10
      }
    ]
  }
}
```

Lowercase shape seen in other runtimes and compatibility docs:

```json
{
  "version": 1,
  "hooks": {
    "sessionStart": [
      {
        "type": "command",
        "bash": "./scripts/session-start.sh",
        "powershell": "./scripts/session-start.ps1",
        "cwd": "scripts",
        "env": {
          "LOG_LEVEL": "INFO"
        },
        "timeoutSec": 30
      }
    ]
  }
}
```

## Organization strategy for JSON hooks

Keep multiple files organized by category, then compose them in your workflow:

1. `security.json`: injection checks, secret leakage prevention.
2. `code-rules.json`: style/policy gate checks.
3. `audit.json`: prompt and tool logging.
4. `session.json`: startup/shutdown context and lifecycle handling.
5. `subagent.json`: subagent start/stop behavior.

This keeps each policy domain small and auditable.

## Key hook command fields

Common fields across surfaces:

- `type`: currently `command`
- `command` as the base command
- `cwd`: optional working directory
- `env`: optional environment variables
- `timeout` (runtime style) or `timeoutSec` (legacy/docs style)

When building reusable skills for Copilot, prefer uppercase event names and one portable base command.
