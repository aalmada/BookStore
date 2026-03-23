# Examples

## Example 1: Security + audit split by category (JSON files)

```text
.github/hooks/
  security.json
  audit.json
  session.json
  scripts/
    deny_dangerous_tool.py
    log_prompt.py
    log_tool_result.py
```

`security.json`:

```json
{
  "version": 1,
  "hooks": {
    "PreToolUse": [
      {
        "type": "command",
        "command": "python3 .github/hooks/scripts/deny_dangerous_tool.py",
        "timeout": 10
      }
    ]
  }
}
```

`audit.json`:

```json
{
  "version": 1,
  "hooks": {
    "UserPromptSubmitted": [
      {
        "type": "command",
        "command": "python3 .github/hooks/scripts/log_prompt.py",
        "timeout": 5
      }
    ],
    "PostToolUse": [
      {
        "type": "command",
        "command": "python3 .github/hooks/scripts/log_tool_result.py",
        "timeout": 5
      }
    ]
  }
}
```

## Example 2: Agent-scoped strict reviewer

```yaml
---
name: StrictCodeReviewer
description: Performs code review with stricter tool policy.
target: vscode
tools: ['search', 'read', 'runCommands', 'edit']
hooks:
  PreToolUse:
    - type: command
      command: python3 ./.github/agents/hooks/reviewer_pretool.py
      timeout: 8
---
```

## Example 3: Defensive parser pseudocode

```python
import json, sys

payload = json.load(sys.stdin)

# docs-style
name = payload.get("toolName")
args = payload.get("toolArgs")

# runtime-style fallback
if not name:
    name = payload.get("tool_name")
if not args:
    args = payload.get("tool_input")

# parse tool args if string
if isinstance(args, str):
    try:
        args = json.loads(args)
    except Exception:
        args = {"raw": args}
```

This parser pattern lets one script work across clients that differ in field names.
