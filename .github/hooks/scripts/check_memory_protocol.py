#!/usr/bin/env python3
"""
PreToolUse hook: Enforce the agent memory handoff protocol.

Agents may only write to the six designated /memories/session/ files.
Writes to user memory (/memories/) or repo memory (/memories/repo/) are
blocked to prevent agents corrupting persistent state.

Input (stdin): VS Code PreToolUse JSON payload
Output (stdout): JSON deny decision, or nothing on success
"""

import json
import sys

ALLOWED_FILES: frozenset[str] = frozenset(
    {
        "task-brief.md",
        "plan.md",
        "backend-output.md",
        "frontend-output.md",
        "test-output.md",
        "review.md",
    }
)

WRITE_COMMANDS: frozenset[str] = frozenset({"create", "str_replace", "insert", "delete"})


def deny(reason: str) -> None:
    output = {
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": "deny",
            "permissionDecisionReason": reason,
        }
    }
    print(json.dumps(output))
    sys.exit(0)


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    tool_name: str = data.get("tool_name", "").lower()
    if "memory" not in tool_name:
        sys.exit(0)

    tool_input: dict = data.get("tool_input", {})
    command: str = tool_input.get("command", "")

    # Read / view operations are always allowed
    if command not in WRITE_COMMANDS:
        sys.exit(0)

    path: str = tool_input.get("path", tool_input.get("old_path", ""))

    # Must be under /memories/session/
    if not path.startswith("/memories/session/"):
        deny(
            f"Memory protocol violation: agents may only write to /memories/session/.\n"
            f"  Attempted path : {path}\n"
            f"  Allowed prefix : /memories/session/\n"
            f"  Allowed files  : {', '.join(sorted(ALLOWED_FILES))}\n\n"
            f"Writing to user memory (/memories/) or repo memory (/memories/repo/) "
            f"requires explicit user intent — do not do this from an agent."
        )

    # Must be one of the designated handoff filenames
    filename = path.rstrip("/").split("/")[-1]
    if filename not in ALLOWED_FILES:
        deny(
            f"Memory protocol violation: '{filename}' is not a recognised session handoff file.\n"
            f"  Attempted path : {path}\n"
            f"  Allowed files  : {', '.join(sorted(ALLOWED_FILES))}"
        )


if __name__ == "__main__":
    main()
