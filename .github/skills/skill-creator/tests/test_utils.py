"""Tests for scripts/utils.py — parse_skill_md."""

import pytest
from pathlib import Path

from scripts.utils import parse_skill_md


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def write_skill(tmp_path: Path, content: str) -> Path:
    skill_dir = tmp_path / "my-skill"
    skill_dir.mkdir()
    (skill_dir / "SKILL.md").write_text(content)
    return skill_dir


# ---------------------------------------------------------------------------
# Happy-path tests
# ---------------------------------------------------------------------------

class TestParseSkillMdBasic:
    def test_parses_name_and_inline_description(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: my-skill
description: Does something useful.
---

# Body
""")
        name, description, content = parse_skill_md(skill_dir)
        assert name == "my-skill"
        assert description == "Does something useful."
        assert "# Body" in content

    def test_parses_quoted_name(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: "quoted-skill"
description: A description.
---
""")
        name, _, _ = parse_skill_md(skill_dir)
        assert name == "quoted-skill"

    def test_parses_single_quoted_name(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: 'single-quoted'
description: A description.
---
""")
        name, _, _ = parse_skill_md(skill_dir)
        assert name == "single-quoted"

    def test_parses_pipe_multiline_description(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: my-skill
description: |
  First line.
  Second line.
---
""")
        _, description, _ = parse_skill_md(skill_dir)
        assert "First line." in description
        assert "Second line." in description

    def test_parses_block_fold_multiline_description(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: my-skill
description: >
  Folded line one.
  Folded line two.
---
""")
        _, description, _ = parse_skill_md(skill_dir)
        assert "Folded line one." in description

    def test_parses_pipe_strip_multiline_description(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: my-skill
description: |-
  Stripped pipe.
  Second.
---
""")
        _, description, _ = parse_skill_md(skill_dir)
        assert "Stripped pipe." in description

    def test_returns_full_content(self, tmp_path):
        raw = "---\nname: s\ndescription: d\n---\n\nBody text\n"
        skill_dir = write_skill(tmp_path, raw)
        _, _, content = parse_skill_md(skill_dir)
        assert content == raw

    def test_extra_frontmatter_fields_are_ignored(self, tmp_path):
        skill_dir = write_skill(tmp_path, """\
---
name: my-skill
description: A description.
license: MIT
allowed-tools:
  - read_file
---
""")
        name, description, _ = parse_skill_md(skill_dir)
        assert name == "my-skill"
        assert description == "A description."


# ---------------------------------------------------------------------------
# Error-path tests
# ---------------------------------------------------------------------------

class TestParseSkillMdErrors:
    def test_missing_opening_fence_raises(self, tmp_path):
        skill_dir = write_skill(tmp_path, "name: s\ndescription: d\n")
        with pytest.raises(ValueError, match="missing frontmatter"):
            parse_skill_md(skill_dir)

    def test_missing_closing_fence_raises(self, tmp_path):
        skill_dir = write_skill(tmp_path, "---\nname: s\ndescription: d\n")
        with pytest.raises(ValueError, match="missing frontmatter"):
            parse_skill_md(skill_dir)

    def test_missing_skill_md_raises(self, tmp_path):
        empty_dir = tmp_path / "empty"
        empty_dir.mkdir()
        with pytest.raises(FileNotFoundError):
            parse_skill_md(empty_dir)
