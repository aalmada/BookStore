#!/usr/bin/env python3
"""
PreToolUse hook: Block edits containing BookStore banned patterns.

Scans .cs files in edit tool calls and denies the operation when
any AGENTS.md code rule violation is detected.

Input (stdin): VS Code PreToolUse JSON payload
Output (stdout): JSON deny decision, or nothing on success
"""

import json
import re
import sys

# (regex_pattern, human-readable fix message)
RULES: list[tuple[str, str]] = [
    (
        r"\bGuid\.NewGuid\(\)",
        "Use Guid.CreateVersion7() instead of Guid.NewGuid()",
    ),
    (
        r"\bDateTime\.Now\b",
        "Use DateTimeOffset.UtcNow instead of DateTime.Now",
    ),
    (
        r"\b_logger\.Log(?:Information|Warning|Error|Debug|Critical|Trace)\s*\(",
        "Use [LoggerMessage] source generator — never call _logger.Log*() directly",
    ),
    (
        r"namespace\s+[\w.]+\s*\{",
        "Use file-scoped namespaces: 'namespace BookStore.X;' not 'namespace BookStore.X { }'",
    ),
    (
        r'"(?:\*DEFAULT\*|default)"',
        'Use MultiTenancyConstants.* instead of hardcoded tenant strings "*DEFAULT*" or "default"',
    ),
]


def check_content(content: str) -> list[str]:
    return [msg for pattern, msg in RULES if re.search(pattern, content)]


def extract_cs_files(tool_input: dict) -> list[tuple[str, str]]:
    """Return (path, content) pairs for .cs files, handling multiple tool input shapes."""
    results: list[tuple[str, str]] = []

    # editFiles / createFile shape: { files: [{filePath, content}] }
    for f in tool_input.get("files", []):
        if isinstance(f, dict):
            path = f.get("filePath", f.get("path", ""))
            content = f.get("content", f.get("newContent", ""))
        else:
            path, content = str(f), ""
        if path.endswith(".cs") and content:
            results.append((path, content))

    # replaceStringInFile shape: { filePath, newString }
    fp = tool_input.get("filePath", "")
    if fp.endswith(".cs"):
        new_str = tool_input.get("newString", "")
        if new_str:
            results.append((fp, new_str))

    return results


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

    cs_files = extract_cs_files(data.get("tool_input", {}))
    if not cs_files:
        sys.exit(0)

    all_violations: list[str] = []
    for path, content in cs_files:
        for violation in check_content(content):
            all_violations.append(f"  • {path}: {violation}")

    if all_violations:
        deny("BookStore code rule violations:\n" + "\n".join(all_violations))


if __name__ == "__main__":
    main()
