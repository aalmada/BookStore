#!/usr/bin/env python3
"""
UserPromptSubmit hook: Append every prompt to a local audit log.

Non-blocking — always exits 0. Used for observability only.
The log file is gitignored and stays local to the developer's machine.

Log location: .github/hooks/logs/audit.log

Input (stdin): VS Code UserPromptSubmit JSON payload
Output (stdout): nothing (non-blocking)
"""

import json
import os
import sys
from datetime import datetime, timezone

LOG_PATH = os.path.join(".github", "hooks", "logs", "audit.log")


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    prompt: str = data.get("prompt", "").strip()
    session_id: str = data.get("sessionId", "unknown")
    timestamp: str = datetime.now(timezone.utc).isoformat()

    os.makedirs(os.path.dirname(LOG_PATH), exist_ok=True)

    try:
        with open(LOG_PATH, "a", encoding="utf-8") as f:
            f.write(f"[{timestamp}] session={session_id}\n{prompt}\n---\n")
    except Exception:
        pass  # Never fail the hook on a logging error


if __name__ == "__main__":
    main()
