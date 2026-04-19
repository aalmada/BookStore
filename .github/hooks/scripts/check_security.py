#!/usr/bin/env python3
"""
PreToolUse hook: Block edits containing OWASP Top 10 security risks.

Scans .cs and .razor files for hardcoded credentials, injection
vulnerabilities, and dangerous authorization changes.

Input (stdin): VS Code PreToolUse JSON payload
Output (stdout): JSON deny decision, or nothing on success
"""

import json
import re
import sys

# Each entry: (pattern, owasp_category, message)
# Checked line-by-line so we can look at the preceding line for exceptions.
LINE_RULES: list[tuple[str, str]] = [
    # A02 — hardcoded credentials
    (
        r'(?i)\bpassword\s*=\s*"[^"]{3,}"',
        "OWASP A02 (Cryptographic Failures): Hardcoded password",
    ),
    (
        r'(?i)\b(?:api[_\-]?key|apikey)\s*=\s*"[^"]{3,}"',
        "OWASP A02 (Cryptographic Failures): Hardcoded API key",
    ),
    (
        r'(?i)\b(?:secret|token)\s*=\s*"[^"]{8,}"',
        "OWASP A02 (Cryptographic Failures): Hardcoded secret or token",
    ),
    # A02 — embedded credentials in connection strings
    (
        r'(?i)(?:Server|Host|Data Source)=.{0,80}(?:Password|Pwd)=[^;\"\\s]{3,}',
        "OWASP A02 (Cryptographic Failures): Connection string with embedded credentials",
    ),
    # A03 — SQL injection via string interpolation
    (
        r'\$"[^"]*(?:WHERE|SELECT|INSERT|UPDATE|DELETE|DROP)[^"]*\{',
        "OWASP A03 (Injection): Potential SQL injection via string interpolation — use Marten API or parameterised queries",
    ),
]

# Rules that allow an exemption if the preceding line contains "// safe: <reason>"
EXEMPTABLE_RULES: list[tuple[str, str]] = [
    # A01 — broken access control
    (
        r"\[AllowAnonymous\]",
        "OWASP A01 (Broken Access Control): [AllowAnonymous] requires a '// safe: <reason>' comment on the preceding line",
    ),
    (
        r"\.AllowAnonymous\(\)",
        "OWASP A01 (Broken Access Control): .AllowAnonymous() requires a '// safe: <reason>' comment on the preceding line",
    ),
]

# Razor-only exemptable rules
RAZOR_EXEMPTABLE_RULES: list[tuple[str, str]] = [
    (
        r"\(MarkupString\)",
        "OWASP A03 (XSS): MarkupString requires a '// safe: <reason>' comment to confirm the HTML is sanitised",
    ),
]

_SAFE_COMMENT = re.compile(r"^\s*(?://|@\*)\s*safe:", re.IGNORECASE)


def check_lines(lines: list[str], path: str) -> list[str]:
    violations: list[str] = []

    for line in lines:
        for pattern, message in LINE_RULES:
            if re.search(pattern, line):
                violations.append(message)

    for i, line in enumerate(lines):
        prev = lines[i - 1] if i > 0 else ""
        for pattern, message in EXEMPTABLE_RULES:
            if re.search(pattern, line) and not _SAFE_COMMENT.match(prev):
                violations.append(message)

    if path.endswith(".razor"):
        for i, line in enumerate(lines):
            prev = lines[i - 1] if i > 0 else ""
            for pattern, message in RAZOR_EXEMPTABLE_RULES:
                if re.search(pattern, line) and not _SAFE_COMMENT.match(prev):
                    violations.append(message)

    return violations


def extract_files(tool_input: dict) -> list[tuple[str, str]]:
    """Return (path, content) pairs for .cs and .razor files."""
    results: list[tuple[str, str]] = []

    for f in tool_input.get("files", []):
        if isinstance(f, dict):
            path = f.get("filePath", f.get("path", ""))
            content = f.get("content", f.get("newContent", ""))
        else:
            path, content = str(f), ""
        if path.endswith((".cs", ".razor")) and content:
            results.append((path, content))

    fp = tool_input.get("filePath", "")
    if fp.endswith((".cs", ".razor")):
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

    files = extract_files(data.get("tool_input", {}))
    if not files:
        sys.exit(0)

    all_violations: list[str] = []
    for path, content in files:
        lines = content.splitlines()
        for v in check_lines(lines, path):
            all_violations.append(f"  • {path}: {v}")

    if all_violations:
        deny("Security policy violations:\n" + "\n".join(all_violations))


if __name__ == "__main__":
    main()
