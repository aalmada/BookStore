# Compatibility Matrix

Use this as a planning guide. Validate in your actual client/runtime.

| Hook event | VS Code coding agent | Copilot CLI | Notes |
|---|---|---|---|
| `SessionStart` | Yes | Yes | Copilot canonical event name |
| `SessionEnd` | Yes | Yes | Copilot canonical event name |
| `UserPromptSubmitted` | Yes | Yes | Copilot canonical event name |
| `PreToolUse` | Yes | Yes | Primary policy gate |
| `PostToolUse` | Yes | Yes | Logging/metrics event |
| `ErrorOccurred` | Yes | Yes | Error telemetry |
| `AgentStop` | Runtime-dependent | Runtime-dependent | Treat as advanced/client-specific |
| `SubagentStop` | Runtime-dependent | Runtime-dependent | Treat as advanced/client-specific |
| `SubagentStart` | Runtime-dependent | Runtime-dependent | Often tied to agent orchestration support |
| `PreCompact` | VS Code-specific in some environments | No | Context-compaction lifecycle event |

## Field compatibility guidance

1. Prefer one base `command` in new configs.
2. Add OS-specific fields only when required:
   - `windows`, `linux`, `macos`
3. Use `timeout` in runtime-style configs.
4. For this Copilot-specific skill, prefer Copilot event casing:
   - `SessionStart`, `PreToolUse`, `PostToolUse`, `UserPromptSubmitted`
5. Treat lowercase names as compatibility variants for other runtimes such as Claude:
   - `sessionStart`, `preToolUse`, `postToolUse`, `userPromptSubmitted`
6. Support legacy/docs-style fields only when needed:
   - `bash`, `powershell`, `timeoutSec`
7. If your runtime expects alternate names, map explicitly:
   - payload fields like `tool_name` and `tool_input`
   - payload fields like `toolName` and `toolArgs`
8. Keep a thin adapter layer in scripts so policy logic is runtime-agnostic.
