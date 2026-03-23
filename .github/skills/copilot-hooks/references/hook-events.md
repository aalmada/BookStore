# Hook Events

This matrix summarizes practical behavior and decision capabilities.

| Event | Purpose | Can block? | Typical use |
|---|---|---|---|
| `SessionStart` | New or resumed session initialization | No | Inject context, start audit session |
| `SessionEnd` | Session completion or termination | No | Cleanup, summary logs |
| `UserPromptSubmitted` | User prompt submission event | No | Prompt audit logging, telemetry |
| `PreToolUse` | Before any tool execution | Yes | Allow/deny enforcement, policy gates |
| `PostToolUse` | After tool execution | No | Result logging, metrics, failure alerting |
| `ErrorOccurred` | Runtime error event | No | Error reporting, diagnostics |
| `AgentStop` | Main agent finished response | Runtime-specific | Final validation, completion checks |
| `SubagentStop` | Subagent finished response | Runtime-specific | Per-agent completion gates |

## Blocking semantics

In Copilot environments, deny decisions are typically processed in `PreToolUse`.

- Return no output to allow.
- Return explicit decision JSON only when blocking.
- Include a reason that the user can act on.

For stop-related events, behavior can vary by runtime. Treat them as advanced and verify in your client before relying on hard gates. If you also target Claude, document the lowercase event variants separately.
