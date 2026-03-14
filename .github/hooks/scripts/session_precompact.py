#!/usr/bin/env python3
"""
PreCompact hook: Remind agents to re-read session memory before context is truncated.

The PreCompact event fires before VS Code compacts the conversation history.
It uses the common output format only (no hookSpecificOutput), so we emit a
systemMessage that appears in chat, prompting the agent to reload its working
context from the session memory files.

Input (stdin): VS Code PreCompact JSON payload
Output (stdout): JSON systemMessage reminder
"""

import json
import sys

MESSAGE = """\
⚠️  Context compaction in progress.

Re-read your session memory files to restore working context before continuing:
  • /memories/session/task-brief.md   (task scope)
  • /memories/session/plan.md         (implementation plan)
  • /memories/session/backend-output.md  (backend work done so far)
  • /memories/session/frontend-output.md (frontend work done so far)
  • /memories/session/test-output.md  (tests written so far)
  • /memories/session/review.md       (review findings so far)

Use the vscode/memory tool with command='view' on each relevant file.
"""


def main() -> None:
    try:
        json.load(sys.stdin)  # consume stdin
    except Exception:
        pass

    print(json.dumps({"systemMessage": MESSAGE}))


if __name__ == "__main__":
    main()
