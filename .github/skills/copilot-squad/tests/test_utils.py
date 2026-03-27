"""Tests for scripts/utils.py."""

import json
from pathlib import Path
from unittest.mock import patch

import pytest

from scripts.utils import find_project_root, load_evals, parse_skill_md


class TestFindProjectRoot:
    def test_finds_ancestor_with_github_dir(self, tmp_path):
        (tmp_path / ".github").mkdir()
        nested = tmp_path / "a" / "b"
        nested.mkdir(parents=True)
        with patch("scripts.utils.Path.cwd", return_value=nested):
            result = find_project_root()
        assert result == tmp_path

    def test_falls_back_to_cwd_when_no_github_dir(self, tmp_path):
        with patch("scripts.utils.Path.cwd", return_value=tmp_path):
            result = find_project_root()
        assert result == tmp_path


class TestParseSkillMd:
    def test_parses_simple_frontmatter(self, tmp_path):
        (tmp_path / "SKILL.md").write_text(
            "---\nname: my-skill\ndescription: Does something useful.\n---\n# Body\n"
        )
        name, description, content = parse_skill_md(tmp_path)
        assert name == "my-skill"
        assert description == "Does something useful."
        assert "# Body" in content

    def test_parses_multiline_description(self, tmp_path):
        (tmp_path / "SKILL.md").write_text(
            "---\nname: multi\ndescription: >\n  Line one.\n  Line two.\n---\n"
        )
        name, description, _ = parse_skill_md(tmp_path)
        assert name == "multi"
        assert "Line one." in description
        assert "Line two." in description

    def test_raises_without_opening_fence(self, tmp_path):
        (tmp_path / "SKILL.md").write_text("name: bad\n")
        with pytest.raises(ValueError, match="missing frontmatter"):
            parse_skill_md(tmp_path)

    def test_raises_without_closing_fence(self, tmp_path):
        (tmp_path / "SKILL.md").write_text("---\nname: bad\n")
        with pytest.raises(ValueError, match="closing"):
            parse_skill_md(tmp_path)


class TestLoadEvals:
    def test_loads_evals_from_dict_wrapper(self, tmp_path):
        data = {
            "skill_name": "copilot-squad",
            "evals": [
                {"id": 1, "prompt": "Do X", "assertions": []},
                {"id": 2, "prompt": "Do Y", "assertions": []},
            ],
        }
        evals_file = tmp_path / "evals.json"
        evals_file.write_text(json.dumps(data))
        result = load_evals(evals_file)
        assert len(result) == 2
        assert result[0]["id"] == 1

    def test_loads_evals_from_bare_list(self, tmp_path):
        data = [{"id": 1, "prompt": "Do X"}]
        evals_file = tmp_path / "evals.json"
        evals_file.write_text(json.dumps(data))
        result = load_evals(evals_file)
        assert len(result) == 1

    def test_raises_on_invalid_format(self, tmp_path):
        evals_file = tmp_path / "evals.json"
        evals_file.write_text('"not a list or dict"')
        with pytest.raises(ValueError):
            load_evals(evals_file)
