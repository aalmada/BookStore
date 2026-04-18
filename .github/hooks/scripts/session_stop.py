#!/usr/bin/env python3
"""
Stop hook: Block session end if the build is broken.

Runs 'dotnet build --no-restore -v:m' when the session ends. If compilation
fails, the agent is blocked from stopping so it can fix errors first.

Note: -v:m (minimal) is used instead of -q (quiet) because .NET 10 SDK's
quiet mode triggers a "Question build" that exits with code 1 when any
output is not up-to-date — without doing the actual compile.

Checks stop_hook_active to prevent infinite loops.

Input (stdin): VS Code Stop JSON payload
Output (stdout): JSON block decision on build failure, or nothing on success
"""

import json
import subprocess
import sys


def run_build(cwd: str) -> tuple[bool, str]:
    try:
        result = subprocess.run(
            ["dotnet", "build", "--no-restore", "-v:m"],
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=120,
        )
        return result.returncode == 0, (result.stdout + result.stderr).strip()
    except Exception as e:
        return False, str(e)


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    # Prevent infinite loop — if we already tried to block once, let it stop.
    if data.get("stop_hook_active", False):
        sys.exit(0)

    cwd: str = data.get("cwd", ".")
    ok, output = run_build(cwd)

    if not ok:
        result = {
            "hookSpecificOutput": {
                "hookEventName": "Stop",
                "decision": "block",
                "reason": (
                    "Build is broken — fix all compilation errors before finishing.\n\n"
                    + output
                ),
            }
        }
        print(json.dumps(result))


if __name__ == "__main__":
    main()
