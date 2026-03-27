"""Tests for scripts/grade.py.

CopilotClient is mocked throughout so no real CLI is needed.
"""

from __future__ import annotations

import asyncio
import json
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from scripts.grade import _build_grading_prompt, _grade_async, _read_outputs, grade


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_run_dir(
    tmp_path: Path,
    with_squad: bool = True,
    outputs: dict[str, str] | None = None,
    assertions: list[dict] | None = None,
) -> Path:
    run_dir = tmp_path / "run-1"
    out_dir = run_dir / "outputs"
    out_dir.mkdir(parents=True)

    for fname, content in (outputs or {}).items():
        (out_dir / fname).write_text(content)

    meta = {
        "eval_id": 1,
        "with_squad": with_squad,
        "assertions": assertions or [],
    }
    (run_dir / "eval_metadata.json").write_text(json.dumps(meta))
    return run_dir


def _eval_item(assertions: list[dict] | None = None) -> dict:
    return {
        "id": 1,
        "prompt": "Create a review squad",
        "assertions": assertions or [
            {"id": "has-orchestrator", "text": "An Orchestrator agent file is created"},
            {"id": "has-planner", "text": "A Planner agent file is created"},
        ],
    }


def _make_client_returning(response: str):
    """Return a CopilotClient mock that fires assistant.message with `response`."""
    session = MagicMock()
    session.__aenter__ = AsyncMock(return_value=session)
    session.__aexit__ = AsyncMock(return_value=None)

    captured: list = []

    def fake_on(handler):
        captured.append(handler)

    async def fake_send(query):
        event = MagicMock()
        event.type.value = "assistant.message"
        event.data.content = response
        for h in captured:
            h(event)
        # Also fire idle so done gets set in both branches
        idle = MagicMock()
        idle.type.value = "session.idle"
        for h in captured:
            h(idle)

    session.on.side_effect = fake_on
    session.send = AsyncMock(side_effect=fake_send)

    client = MagicMock()
    client.start = AsyncMock()
    client.stop = AsyncMock()
    client.create_session = AsyncMock(return_value=session)
    return client


# ---------------------------------------------------------------------------
# _read_outputs
# ---------------------------------------------------------------------------

class TestReadOutputs:
    def test_reads_text_files(self, tmp_path):
        run_dir = tmp_path / "run"
        out = run_dir / "outputs"
        out.mkdir(parents=True)
        (out / "Orchestrator.agent.md").write_text("---\nname: Orchestrator\n---")
        (out / "Planner.agent.md").write_text("---\nname: Planner\n---")

        result = _read_outputs(run_dir)
        assert "Orchestrator.agent.md" in result
        assert "Planner.agent.md" in result

    def test_skips_internal_files(self, tmp_path):
        run_dir = tmp_path / "run"
        out = run_dir / "outputs"
        out.mkdir(parents=True)
        (out / "transcript.md").write_text("## Transcript")
        (out / "metrics.json").write_text("{}")
        (out / "tool_calls_debug.jsonl").write_text("")

        result = _read_outputs(run_dir)
        # transcript.md is now included so the grader can assess inline code
        assert "transcript.md" in result
        # Internal bookkeeping files are still excluded
        assert "metrics.json" not in result
        assert "tool_calls_debug.jsonl" not in result

    def test_returns_empty_when_no_outputs_dir(self, tmp_path):
        run_dir = tmp_path / "run"
        run_dir.mkdir()
        assert _read_outputs(run_dir) == {}


# ---------------------------------------------------------------------------
# _build_grading_prompt
# ---------------------------------------------------------------------------

class TestBuildGradingPrompt:
    def test_includes_assertion_texts(self):
        assertions = [
            {"id": "a", "text": "Orchestrator is created"},
            {"id": "b", "text": "Planner has no edit tool"},
        ]
        prompt = _build_grading_prompt(
            "Build a squad", assertions, {"Agent.md": "content"}, with_squad=True
        )
        assert "Orchestrator is created" in prompt
        assert "Planner has no edit tool" in prompt

    def test_includes_output_content(self):
        prompt = _build_grading_prompt(
            "Build a squad", [], {"Orchestrator.agent.md": "---\nname: Orchestrator\n---"}, with_squad=True
        )
        assert "Orchestrator.agent.md" in prompt
        assert "name: Orchestrator" in prompt

    def test_labels_with_squad(self):
        prompt = _build_grading_prompt("X", [], {}, with_squad=True)
        assert "with the squad skill active" in prompt

    def test_labels_without_squad(self):
        prompt = _build_grading_prompt("X", [], {}, with_squad=False)
        assert "without the squad skill (baseline)" in prompt

    def test_notes_empty_outputs(self):
        prompt = _build_grading_prompt("X", [], {}, with_squad=False)
        assert "no output files were captured" in prompt


# ---------------------------------------------------------------------------
# _grade_async — happy path
# ---------------------------------------------------------------------------

class TestGradeAsync:
    @pytest.mark.asyncio
    async def test_grading_json_uses_expectations_field(self, tmp_path):
        """grading.json must use `expectations` field (not `assertions`)."""
        run_dir = _make_run_dir(
            tmp_path,
            outputs={"Orchestrator.agent.md": "---\nname: Orchestrator\n---"},
        )
        eval_item = _eval_item()

        grading_response = json.dumps({
            "expectations": [
                {"text": "An Orchestrator agent file is created", "passed": True,
                 "evidence": "Found Orchestrator.agent.md in outputs."},
                {"text": "A Planner agent file is created", "passed": False,
                 "evidence": "No Planner file found."},
            ],
            "summary": {"passed": 1, "failed": 1, "total": 2, "pass_rate": 0.5},
        })
        client = _make_client_returning(grading_response)

        with patch("scripts.grade.CopilotClient", return_value=client):
            grading = await _grade_async(eval_item=eval_item, run_dir=run_dir, model=None)

        assert "expectations" in grading
        assert "assertions" not in grading
        assert grading["summary"]["total"] == 2
        assert grading["summary"]["pass_rate"] == 0.5

    @pytest.mark.asyncio
    async def test_all_fail_on_unparseable_response(self, tmp_path):
        run_dir = _make_run_dir(tmp_path)
        eval_item = _eval_item()

        client = _make_client_returning("Sorry, I can't grade that right now.")

        with patch("scripts.grade.CopilotClient", return_value=client):
            grading = await _grade_async(eval_item=eval_item, run_dir=run_dir, model=None)

        assert all(not e["passed"] for e in grading["expectations"])
        assert grading["summary"]["passed"] == 0

    @pytest.mark.asyncio
    async def test_empty_assertions_returns_perfect_score(self, tmp_path):
        run_dir = _make_run_dir(tmp_path)
        eval_item = {"id": 1, "prompt": "Do X", "assertions": []}

        client = MagicMock()
        with patch("scripts.grade.CopilotClient", return_value=client):
            grading = await _grade_async(eval_item=eval_item, run_dir=run_dir, model=None)

        assert grading["expectations"] == []
        assert grading["summary"]["pass_rate"] == 1.0
        # No API call should have been made
        client.start.assert_not_called()


# ---------------------------------------------------------------------------
# grade — writes grading.json
# ---------------------------------------------------------------------------

class TestGradeWritesFile:
    def test_writes_grading_json(self, tmp_path):
        run_dir = _make_run_dir(
            tmp_path,
            outputs={"Planner.agent.md": "---\nname: Planner\n---"},
        )
        eval_item = _eval_item()

        grading_response = json.dumps({
            "expectations": [
                {"text": "An Orchestrator agent file is created", "passed": False,
                 "evidence": "Not found."},
                {"text": "A Planner agent file is created", "passed": True,
                 "evidence": "Found Planner.agent.md."},
            ],
            "summary": {"passed": 1, "failed": 1, "total": 2, "pass_rate": 0.5},
        })
        client = _make_client_returning(grading_response)

        with patch("scripts.grade.CopilotClient", return_value=client):
            result = grade(eval_item=eval_item, run_dir=run_dir, model=None)

        grading_file = run_dir / "grading.json"
        assert grading_file.exists()
        saved = json.loads(grading_file.read_text())
        assert saved["summary"]["pass_rate"] == result["summary"]["pass_rate"]

    def test_writes_grader_model_default(self, tmp_path):
        """grading.json must record grader_model='(default)' when model=None."""
        run_dir = _make_run_dir(tmp_path, outputs={})
        grading_response = json.dumps({
            "expectations": [],
            "summary": {"passed": 0, "failed": 0, "total": 0, "pass_rate": 1.0},
        })
        client = _make_client_returning(grading_response)

        with patch("scripts.grade.CopilotClient", return_value=client):
            result = grade(eval_item=_eval_item(), run_dir=run_dir, model=None)

        saved = json.loads((run_dir / "grading.json").read_text())
        assert saved.get("grader_model") == "(default)"
        assert result.get("grader_model") == "(default)"

    def test_writes_grader_model_named(self, tmp_path):
        """grading.json must record the model string when one is specified."""
        run_dir = _make_run_dir(tmp_path, outputs={})
        grading_response = json.dumps({
            "expectations": [],
            "summary": {"passed": 0, "failed": 0, "total": 0, "pass_rate": 1.0},
        })
        client = _make_client_returning(grading_response)

        with patch("scripts.grade.CopilotClient", return_value=client):
            result = grade(eval_item=_eval_item(), run_dir=run_dir, model="gpt-4o-mini")

        saved = json.loads((run_dir / "grading.json").read_text())
        assert saved.get("grader_model") == "gpt-4o-mini"
        assert result.get("grader_model") == "gpt-4o-mini"
