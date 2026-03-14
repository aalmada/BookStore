#!/usr/bin/env python3
"""
SubagentStop hook: Gate for coding agents before they are allowed to finish.

For BackendDeveloper, FrontendDeveloper, and TestEngineer:
  1. Runs 'dotnet build --no-restore -q' — blocks if compilation fails.
  2. Runs 'dotnet format --verify-no-changes' — blocks if style violations found.

For all known agents:
  3. Checks stop_hook_active to prevent infinite loops.

This replaces a per-edit PostToolUse build hook — compilation runs once when
the agent has finished all its edits, not after every individual file change.

Input (stdin): VS Code SubagentStop JSON payload
Output (stdout): JSON block decision on failure, or nothing on success
"""

import json
import subprocess
import sys

# Agents that must pass build + format before stopping
BUILD_AGENTS: frozenset[str] = frozenset(
    {"BackendDeveloper", "FrontendDeveloper", "TestEngineer"}
)


def run_command(cmd: list[str], cwd: str, timeout: int = 120) -> tuple[bool, str]:
    try:
        result = subprocess.run(
            cmd,
            cwd=cwd,
            capture_output=True,
            text=True,
            timeout=timeout,
        )
        return result.returncode == 0, (result.stdout + result.stderr).strip()
    except subprocess.TimeoutExpired:
        return False, f"Command timed out after {timeout}s: {' '.join(cmd)}"
    except Exception as e:
        return False, str(e)


def block(reason: str) -> None:
    print(json.dumps({"decision": "block", "reason": reason}))
    sys.exit(0)


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except Exception:
        sys.exit(0)

    # Prevent infinite loop — if already continuing from a previous block, let it stop.
    if data.get("stop_hook_active", False):
        sys.exit(0)

    agent_type: str = data.get("agent_type", "")
    cwd: str = data.get("cwd", ".")

    if agent_type not in BUILD_AGENTS:
        sys.exit(0)

    # 1. Build check
    build_ok, build_output = run_command(
        ["dotnet", "build", "--no-restore", "-q"], cwd
    )
    if not build_ok:
        block(
            f"Build failed — fix all compilation errors before finishing.\n\n"
            f"{build_output}"
        )

    # 2. Format check
    fmt_ok, fmt_output = run_command(
        ["dotnet", "format", "--verify-no-changes", "--no-restore"],
        cwd,
        timeout=60,
    )
    if not fmt_ok:
        block(
            f"Code style violations detected — run 'dotnet format' and fix before finishing.\n\n"
            f"{fmt_output}"
        )


if __name__ == "__main__":
    main()
