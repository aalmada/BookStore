"""Shared utilities for copilot-squad eval scripts."""

from __future__ import annotations

import json
from pathlib import Path


def find_project_root() -> Path:
    """Find the project root by walking up from cwd looking for .github/.

    Used to construct workspace paths for eval artifacts.
    """
    current = Path.cwd()
    for parent in [current, *current.parents]:
        if (parent / ".github").is_dir():
            return parent
    return current


def parse_skill_md(skill_path: Path) -> tuple[str, str, str]:
    """Parse a SKILL.md file, returning (name, description, full_content)."""
    content = (skill_path / "SKILL.md").read_text()
    lines = content.split("\n")

    if lines[0].strip() != "---":
        raise ValueError("SKILL.md missing frontmatter (no opening ---)")

    end_idx = None
    for i, line in enumerate(lines[1:], start=1):
        if line.strip() == "---":
            end_idx = i
            break

    if end_idx is None:
        raise ValueError("SKILL.md missing frontmatter (no closing ---)")

    name = ""
    description = ""
    frontmatter_lines = lines[1:end_idx]
    i = 0
    while i < len(frontmatter_lines):
        line = frontmatter_lines[i]
        if line.startswith("name:"):
            name = line[len("name:"):].strip().strip('"').strip("'")
        elif line.startswith("description:"):
            value = line[len("description:"):].strip()
            # Handle YAML multiline indicators (>, |, >-, |-)
            if value in (">", "|", ">-", "|-"):
                continuation_lines: list[str] = []
                i += 1
                while i < len(frontmatter_lines) and (
                    frontmatter_lines[i].startswith("  ") or frontmatter_lines[i].startswith("\t")
                ):
                    continuation_lines.append(frontmatter_lines[i].strip())
                    i += 1
                description = " ".join(continuation_lines)
                continue
            else:
                description = value.strip('"').strip("'")
        i += 1

    return name, description, content


def load_evals(evals_path: Path) -> list[dict]:
    """Load evals from an evals.json file and return the evals list."""
    data = json.loads(evals_path.read_text())
    evals = data.get("evals", data) if isinstance(data, dict) else data
    if not isinstance(evals, list):
        raise ValueError(f"Expected a list of evals in {evals_path}")
    return evals
