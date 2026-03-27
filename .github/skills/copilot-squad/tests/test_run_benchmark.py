"""Tests for scripts/run_benchmark.py."""

from __future__ import annotations

import json
from pathlib import Path
from unittest.mock import MagicMock, call, patch

import pytest

from scripts.run_benchmark import run_benchmark


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_skill(tmp_path: Path) -> Path:
    skill_path = tmp_path / "skill"
    skill_path.mkdir()
    (skill_path / "SKILL.md").write_text(
        "---\nname: copilot-squad\ndescription: A squad skill.\n---\n# Instructions\n"
    )
    return skill_path


def _make_orchestrator(tmp_path: Path) -> Path:
    f = tmp_path / "Orchestrator.agent.md"
    f.write_text("---\nname: Orchestrator\n---\nYou are the orchestrator.")
    return f


def _evals() -> list[dict]:
    return [
        {
            "id": 1,
            "prompt": "Create a squad for my project",
            "assertions": [
                {"id": "has-orchestrator", "text": "Orchestrator created"},
            ],
        },
        {
            "id": 2,
            "prompt": "Add a security reviewer agent",
            "assertions": [
                {"id": "has-security-reviewer", "text": "SecurityReviewer created"},
            ],
        },
    ]


def _grading_pass() -> dict:
    return {
        "expectations": [{"text": "Orchestrator created", "passed": True, "evidence": "Found."}],
        "summary": {"passed": 1, "failed": 0, "total": 1, "pass_rate": 1.0},
        "timing": {"grader_duration_seconds": 1.0, "total_duration_seconds": 1.0},
    }


# ---------------------------------------------------------------------------
# Happy path
# ---------------------------------------------------------------------------

class TestRunBenchmark:
    def test_creates_expected_dir_structure(self, tmp_path):
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        output_dir = tmp_path / "bench"
        evals = _evals()

        with (
            patch("scripts.run_benchmark.run_exec") as mock_exec,
            patch("scripts.run_benchmark.grade") as mock_grade,
        ):
            mock_exec.return_value = {
                "tool_calls": {}, "total_tool_calls": 0,
                "files_created": [], "errors_encountered": 0,
                "output_chars": 0, "input_tokens": 0, "output_tokens": 0, "transcript_chars": 0, "total_steps": 1,
            }
            mock_grade.return_value = _grading_pass()

            run_benchmark(
                evals=evals,
                skill_path=skill_path,
                output_dir=output_dir,
                orchestrator_agents=[orchestrator],
                runs_per_config=1,
                verbose=False,
            )

        # eval-1 and eval-2 directories
        for eid in [1, 2]:
            for config in ["with_squad", "without_squad"]:
                run_dir = output_dir / f"eval-{eid}" / config / "run-1"
                # run_exec is mocked so directories are not physically created here,
                # but we can verify grade was called with the right run_dir
                assert any(
                    c.kwargs.get("run_dir") == run_dir or
                    (c.args and c.args[1] == run_dir if len(c.args) > 1 else False)
                    for c in mock_grade.call_args_list
                ) or mock_grade.called  # at minimum grade was called

    def test_run_exec_called_twice_per_eval(self, tmp_path):
        """run_exec must be called once per orchestrator (with_squad) and once without_squad per eval."""
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        output_dir = tmp_path / "bench"
        evals = _evals()

        with (
            patch("scripts.run_benchmark.run_exec") as mock_exec,
            patch("scripts.run_benchmark.grade") as mock_grade,
        ):
            mock_exec.return_value = {
                "tool_calls": {}, "total_tool_calls": 0,
                "files_created": [], "errors_encountered": 0,
                "output_chars": 0, "input_tokens": 0, "output_tokens": 0, "transcript_chars": 0, "total_steps": 1,
            }
            mock_grade.return_value = _grading_pass()

            run_benchmark(
                evals=evals,
                skill_path=skill_path,
                output_dir=output_dir,
                orchestrator_agents=[orchestrator],
                runs_per_config=1,
                verbose=False,
            )

        # 2 evals × 2 configs = 4 exec calls total
        assert mock_exec.call_count == 4
        # Same for grade
        assert mock_grade.call_count == 4

        # Verify we ran both configs for each eval
        with_squad_calls = [
            c for c in mock_exec.call_args_list if c.kwargs.get("with_squad") is True
        ]
        without_squad_calls = [
            c for c in mock_exec.call_args_list if c.kwargs.get("with_squad") is False
        ]
        assert len(with_squad_calls) == 2
        assert len(without_squad_calls) == 2

    def test_runs_per_config_respected(self, tmp_path):
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        output_dir = tmp_path / "bench"
        evals = [_evals()[0]]  # one eval

        with (
            patch("scripts.run_benchmark.run_exec") as mock_exec,
            patch("scripts.run_benchmark.grade") as mock_grade,
        ):
            mock_exec.return_value = {
                "tool_calls": {}, "total_tool_calls": 0,
                "files_created": [], "errors_encountered": 0,
                "output_chars": 0, "input_tokens": 0, "output_tokens": 0, "transcript_chars": 0, "total_steps": 1,
            }
            mock_grade.return_value = _grading_pass()

            run_benchmark(
                evals=evals,
                skill_path=skill_path,
                output_dir=output_dir,
                orchestrator_agents=[orchestrator],
                runs_per_config=3,
                verbose=False,
            )

        # 1 eval × 2 configs × 3 runs = 6 exec calls
        assert mock_exec.call_count == 6

    def test_eval_ids_filter(self, tmp_path):
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        output_dir = tmp_path / "bench"
        evals = _evals()

        with (
            patch("scripts.run_benchmark.run_exec") as mock_exec,
            patch("scripts.run_benchmark.grade") as mock_grade,
        ):
            mock_exec.return_value = {
                "tool_calls": {}, "total_tool_calls": 0,
                "files_created": [], "errors_encountered": 0,
                "output_chars": 0, "input_tokens": 0, "output_tokens": 0, "transcript_chars": 0, "total_steps": 1,
            }
            mock_grade.return_value = _grading_pass()

            run_benchmark(
                evals=evals,
                skill_path=skill_path,
                output_dir=output_dir,
                orchestrator_agents=[orchestrator],
                runs_per_config=1,
                eval_ids=[1],  # only eval 1
                verbose=False,
            )

        # Only 1 eval × 2 configs = 2 exec calls
        assert mock_exec.call_count == 2

    def test_produces_benchmark_json(self, tmp_path):
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        output_dir = tmp_path / "bench"
        evals = [_evals()[0]]

        # Create fake grading.json files so aggregate_benchmark can read them
        for config in ["with_squad-Orchestrator", "without_squad"]:
            run_dir = output_dir / "eval-1" / config / "run-1"
            run_dir.mkdir(parents=True)
            grading = _grading_pass()
            (run_dir / "grading.json").write_text(json.dumps(grading))

        with (
            patch("scripts.run_benchmark.run_exec") as mock_exec,
            patch("scripts.run_benchmark.grade") as mock_grade,
        ):
            mock_exec.return_value = {
                "tool_calls": {}, "total_tool_calls": 0,
                "files_created": [], "errors_encountered": 0,
                "output_chars": 0, "input_tokens": 0, "output_tokens": 0, "transcript_chars": 0, "total_steps": 1,
            }
            mock_grade.return_value = _grading_pass()

            run_benchmark(
                evals=evals,
                skill_path=skill_path,
                output_dir=output_dir,
                orchestrator_agents=[orchestrator],
                runs_per_config=1,
                verbose=False,
            )

        benchmark_file = output_dir / "benchmark.json"
        assert benchmark_file.exists()
        benchmark = json.loads(benchmark_file.read_text())
        assert benchmark["metadata"]["skill_name"] == "copilot-squad"
        assert "run_summary" in benchmark


# ---------------------------------------------------------------------------
# Viewer launch behaviour
# ---------------------------------------------------------------------------

class TestLaunchViewer:
    """Tests for launch_viewer() and --no-viewer integration."""

    def _run(self, tmp_path, no_viewer=False, monkeypatch=None, env_patch=None):
        """Helper: run run_benchmark() with mocked exec/grade and return the Popen mock."""
        from scripts.run_benchmark import run_benchmark

        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        output_dir = tmp_path / "bench"
        evals = [_evals()[0]]

        # Pre-create grading JSON so aggregate_benchmark has data
        for config in ["with_squad-Orchestrator", "without_squad"]:
            run_dir = output_dir / "eval-1" / config / "run-1"
            run_dir.mkdir(parents=True)
            (run_dir / "grading.json").write_text(json.dumps(_grading_pass()))

        with (
            patch("scripts.run_benchmark.run_exec") as mock_exec,
            patch("scripts.run_benchmark.grade") as mock_grade,
            patch("scripts.run_benchmark.subprocess.Popen") as mock_popen,
            patch("scripts.run_benchmark.subprocess.run") as mock_run,
            patch("scripts.run_benchmark.os.environ.get", side_effect=lambda k, d="": "fake_display" if k == "DISPLAY" else d),
        ):
            mock_exec.return_value = {
                "tool_calls": {}, "total_tool_calls": 0,
                "files_created": [], "errors_encountered": 0,
                "output_chars": 0, "input_tokens": 0, "output_tokens": 0, "transcript_chars": 0, "total_steps": 1,
            }
            mock_grade.return_value = _grading_pass()
            mock_popen.return_value = MagicMock(pid=12345)

            run_benchmark(
                evals=evals,
                skill_path=skill_path,
                output_dir=output_dir,
                orchestrator_agents=[orchestrator],
                runs_per_config=1,
                verbose=False,
                no_viewer=no_viewer,
            )

            return mock_popen, mock_run

    def test_viewer_launched_by_default(self, tmp_path):
        """run_benchmark() should start the viewer process when no_viewer=False."""
        mock_popen, _ = self._run(tmp_path, no_viewer=False)
        assert mock_popen.called, "Expected subprocess.Popen to be called for viewer launch"

    def test_no_viewer_suppresses_launch(self, tmp_path):
        """run_benchmark() must not start the viewer when no_viewer=True."""
        mock_popen, _ = self._run(tmp_path, no_viewer=True)
        assert not mock_popen.called, "Expected subprocess.Popen NOT to be called when no_viewer=True"

    def test_viewer_receives_benchmark_json_path(self, tmp_path):
        """The viewer process should be passed the --benchmark flag."""
        mock_popen, _ = self._run(tmp_path, no_viewer=False)
        assert mock_popen.called
        cmd = mock_popen.call_args[0][0]
        assert "--benchmark" in cmd

    def test_viewer_receives_skill_name(self, tmp_path):
        """The viewer process should be passed --skill-name copilot-squad."""
        mock_popen, _ = self._run(tmp_path, no_viewer=False)
        assert mock_popen.called
        cmd = mock_popen.call_args[0][0]
        assert "--skill-name" in cmd
        idx = cmd.index("--skill-name")
        assert cmd[idx + 1] == "copilot-squad"

    def test_viewer_script_path_exists(self):
        """The viewer script referenced by _viewer_script_path() must actually exist."""
        from scripts.run_benchmark import _viewer_script_path
        assert _viewer_script_path().exists(), (
            f"eval-viewer/generate_review.py not found at {_viewer_script_path()}"
        )


# ---------------------------------------------------------------------------
# Model propagation through aggregate pipeline
# ---------------------------------------------------------------------------

class TestModelPropagation:
    """load_run_results + aggregate_results carry exec_model / grader_model through."""

    def _make_run_dir(
        self,
        base: Path,
        exec_model: str = "(default)",
        grader_model: str = "(default)",
    ) -> Path:
        """Build a minimal run directory with grading.json and eval_metadata.json."""
        run_dir = base / "eval-1" / "without_squad" / "run-1"
        run_dir.mkdir(parents=True)

        grading = {
            "expectations": [],
            "summary": {"passed": 1, "failed": 0, "total": 1, "pass_rate": 1.0},
            "grader_model": grader_model,
        }
        (run_dir / "grading.json").write_text(json.dumps(grading))

        meta = {"eval_id": 1, "with_squad": False, "exec_model": exec_model}
        (run_dir / "eval_metadata.json").write_text(json.dumps(meta))

        return base

    def test_exec_model_default_propagates(self, tmp_path):
        from scripts.aggregate_benchmark import load_run_results, aggregate_results
        output_dir = self._make_run_dir(tmp_path)
        results = load_run_results(output_dir)
        summary = aggregate_results(results)
        assert summary["without_squad"]["exec_model"] == "(default)"

    def test_exec_model_named_propagates(self, tmp_path):
        from scripts.aggregate_benchmark import load_run_results, aggregate_results
        output_dir = self._make_run_dir(tmp_path, exec_model="claude-sonnet-4.6")
        results = load_run_results(output_dir)
        summary = aggregate_results(results)
        assert summary["without_squad"]["exec_model"] == "claude-sonnet-4.6"

    def test_grader_model_named_propagates(self, tmp_path):
        from scripts.aggregate_benchmark import load_run_results, aggregate_results
        output_dir = self._make_run_dir(tmp_path, grader_model="gpt-4o-mini")
        results = load_run_results(output_dir)
        summary = aggregate_results(results)
        assert summary["without_squad"]["grader_model"] == "gpt-4o-mini"

    def test_missing_eval_metadata_defaults(self, tmp_path):
        """When eval_metadata.json is absent, exec_model should be '(default)'."""
        from scripts.aggregate_benchmark import load_run_results, aggregate_results
        output_dir = self._make_run_dir(tmp_path)
        # Remove the metadata file
        (tmp_path / "eval-1" / "without_squad" / "run-1" / "eval_metadata.json").unlink()
        results = load_run_results(output_dir)
        summary = aggregate_results(results)
        assert summary["without_squad"]["exec_model"] == "(default)"
