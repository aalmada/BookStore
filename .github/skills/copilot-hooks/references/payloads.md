# Hook Payloads

This file documents common payload contracts for hook scripts.

## Input payloads

### SessionStart

```json
{
  "timestamp": 1704614400000,
  "cwd": "/path/to/project",
  "source": "new",
  "initialPrompt": "Create a new feature"
}
```

Fields:

- `timestamp`: Unix time in milliseconds.
- `cwd`: working directory.
- `source`: `new`, `resume`, or `startup`.
- `initialPrompt`: optional initial user prompt.

### SessionEnd

```json
{
  "timestamp": 1704618000000,
  "cwd": "/path/to/project",
  "reason": "complete"
}
```

Fields:

- `reason`: often one of `complete`, `error`, `abort`, `timeout`, `user_exit`.

### UserPromptSubmitted

```json
{
  "timestamp": 1704614500000,
  "cwd": "/path/to/project",
  "prompt": "Fix the authentication bug"
}
```

### PreToolUse (Copilot-style)

```json
{
  "timestamp": 1704614600000,
  "cwd": "/path/to/project",
  "toolName": "bash",
  "toolArgs": "{\"command\":\"rm -rf dist\"}"
}
```

### preToolUse (Claude-style compatibility variant)

```json
{
  "session_id": "abc123",
  "cwd": "/path/to/project",
  "tool_name": "edit",
  "tool_input": {
    "filePath": "src/app.ts",
    "newString": "..."
  }
}
```

Use defensive parsing so either shape can be handled.

### PostToolUse

```json
{
  "timestamp": 1704614700000,
  "cwd": "/path/to/project",
  "toolName": "bash",
  "toolArgs": "{\"command\":\"npm test\"}",
  "toolResult": {
    "resultType": "success",
    "textResultForLlm": "All tests passed"
  }
}
```

### ErrorOccurred

```json
{
  "timestamp": 1704614800000,
  "cwd": "/path/to/project",
  "error": {
    "message": "Network timeout",
    "name": "TimeoutError",
    "stack": "TimeoutError: ..."
  }
}
```

### AgentStop and SubagentStop

These are runtime-specific in available fields. Commonly observed fields include:

- `cwd`
- `agent_type` or equivalent role identifier
- `stop_hook_active` or equivalent recursion guard

Treat these payloads as client-specific and log raw input while developing.

## Output payloads

### Deny decision for PreToolUse

Docs-style deny shape:

```json
{
  "permissionDecision": "deny",
  "permissionDecisionReason": "Dangerous command detected"
}
```

Observed nested deny shape in some environments:

```json
{
  "hookSpecificOutput": {
    "hookEventName": "PreToolUse",
    "permissionDecision": "deny",
    "permissionDecisionReason": "Dangerous command detected"
  }
}
```

### Context/system output for lifecycle hooks

Some runtimes support supplemental outputs such as:

```json
{
  "hookSpecificOutput": {
    "hookEventName": "SessionStart",
    "additionalContext": "Project policies and reminders"
  }
}
```

Or:

```json
{
  "systemMessage": "Context compaction in progress. Re-read memory files."
}
```

If unsupported, these outputs are ignored. Keep scripts no-op safe.
