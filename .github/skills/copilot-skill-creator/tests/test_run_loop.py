"""Tests for scripts/run_loop.py.

run_eval and improve_description are mocked so no real Copilot CLI is needed.
"""

import json
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from scripts.run_loop import run_loop, split_eval_set


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_eval_set(n_trigger=3, n_no_trigger=3):
    items = []
    for i in range(n_trigger):
        items.append({"query": f"trigger query {i}", "should_trigger": True})
    for i in range(n_no_trigger):
        items.append({"query": f"no-trigger query {i}", "should_trigger": False})
    return items


def _make_skill_md(tmp_path: Path, name: str = "TestSkill", description: str = "desc") -> Path:
    skill_dir = tmp_path / "myskill"
    skill_dir.mkdir()
    content = f"""---
name: {name}
description: {description}
---
# TestSkill
Some content here.
"""
    (skill_dir / "SKILL.md").write_text(content)
    return skill_dir


def _all_pass_eval(eval_set, skill_name, description, **kwargs):
    """run_eval stub that marks everything as passed."""
    results = [
        {
            "query": item["query"],
            "should_trigger": item["should_trigger"],
            "pass": True,
            "triggers": 3,
            "runs": 3,
        }
        for item in eval_set
    ]
    return {
        "description": description,
        "results": results,
        "summary": {"passed": len(results), "failed": 0, "total": len(results)},
    }


def _all_fail_eval(eval_set, skill_name, description, **kwargs):
    """run_eval stub that marks everything as failed."""
    results = [
        {
            "query": item["query"],
            "should_trigger": item["should_trigger"],
            "pass": False,
            "triggers": 0 if item["should_trigger"] else 3,
            "runs": 3,
        }
        for item in eval_set
    ]
    return {
        "description": description,
        "results": results,
        "summary": {"passed": 0, "failed": len(results), "total": len(results)},
    }


# ---------------------------------------------------------------------------
# split_eval_set
# ---------------------------------------------------------------------------

class TestSplitEvalSet:
    def test_total_size_preserved(self):
        eval_set = _make_eval_set(4, 4)
        train, test = split_eval_set(eval_set, holdout=0.5)
        assert len(train) + len(test) == len(eval_set)

    def test_holdout_fraction_respected_for_triggers(self):
        eval_set = _make_eval_set(10, 0)
        _, test = split_eval_set(eval_set, holdout=0.4)
        # 10 * 0.4 = 4 trigger items in test
        assert sum(1 for e in test if e["should_trigger"]) == 4

    def test_holdout_fraction_respected_for_no_triggers(self):
        eval_set = _make_eval_set(0, 10)
        _, test = split_eval_set(eval_set, holdout=0.4)
        assert sum(1 for e in test if not e["should_trigger"]) == 4

    def test_minimum_one_item_in_test_per_group(self):
        eval_set = _make_eval_set(1, 1)
        train, test = split_eval_set(eval_set, holdout=0.1)
        # max(1, int(1*0.1)) = max(1, 0) = 1
        assert sum(1 for e in test if e["should_trigger"]) >= 1
        assert sum(1 for e in test if not e["should_trigger"]) >= 1

    def test_same_seed_produces_same_split(self):
        eval_set = _make_eval_set(6, 6)
        train1, test1 = split_eval_set(eval_set, holdout=0.4, seed=99)
        train2, test2 = split_eval_set(eval_set, holdout=0.4, seed=99)
        assert [e["query"] for e in train1] == [e["query"] for e in train2]
        assert [e["query"] for e in test1] == [e["query"] for e in test2]

    def test_different_seeds_may_produce_different_splits(self):
        eval_set = _make_eval_set(8, 8)
        train1, _ = split_eval_set(eval_set, holdout=0.4, seed=1)
        train2, _ = split_eval_set(eval_set, holdout=0.4, seed=2)
        # Very unlikely to be identical with 16 items and different seeds
        assert [e["query"] for e in train1] != [e["query"] for e in train2]

    def test_no_overlap_between_train_and_test(self):
        eval_set = _make_eval_set(5, 5)
        train, test = split_eval_set(eval_set, holdout=0.4)
        train_queries = {e["query"] for e in train}
        test_queries = {e["query"] for e in test}
        assert train_queries.isdisjoint(test_queries)


# ---------------------------------------------------------------------------
# run_loop — stops when all train queries pass
# ---------------------------------------------------------------------------

class TestRunLoopTermination:
    def test_terminates_on_first_all_pass_iteration(self, tmp_path):
        eval_set = _make_eval_set(2, 2)
        skill_dir = _make_skill_md(tmp_path)

        with patch("scripts.run_loop.run_eval", side_effect=_all_pass_eval), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            result = run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override=None,
                num_workers=1,
                timeout=10,
                max_iterations=5,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.0,
                model="test-model",
                verbose=False,
            )

        assert result["iterations_run"] == 1
        assert "all_passed" in result["exit_reason"]

    def test_stops_at_max_iterations(self, tmp_path):
        eval_set = _make_eval_set(2, 2)
        skill_dir = _make_skill_md(tmp_path)

        with patch("scripts.run_loop.run_eval", side_effect=_all_fail_eval), \
             patch("scripts.run_loop.improve_description", return_value="new desc"), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            result = run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override=None,
                num_workers=1,
                timeout=10,
                max_iterations=3,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.0,
                model="test-model",
                verbose=False,
            )

        assert result["iterations_run"] == 3
        assert "max_iterations" in result["exit_reason"]


# ---------------------------------------------------------------------------
# run_loop — output shape
# ---------------------------------------------------------------------------

class TestRunLoopOutputShape:
    def test_return_value_has_required_keys(self, tmp_path):
        eval_set = _make_eval_set(2, 2)
        skill_dir = _make_skill_md(tmp_path)

        with patch("scripts.run_loop.run_eval", side_effect=_all_pass_eval), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            result = run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override=None,
                num_workers=1,
                timeout=10,
                max_iterations=5,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.0,
                model="test-model",
                verbose=False,
            )

        for key in ("exit_reason", "original_description", "best_description",
                    "best_score", "final_description", "iterations_run", "history"):
            assert key in result, f"missing key: {key}"

    def test_history_contains_one_entry_per_iteration(self, tmp_path):
        eval_set = _make_eval_set(1, 1)
        skill_dir = _make_skill_md(tmp_path)

        with patch("scripts.run_loop.run_eval", side_effect=_all_fail_eval), \
             patch("scripts.run_loop.improve_description", return_value="new desc"), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            result = run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override=None,
                num_workers=1,
                timeout=10,
                max_iterations=2,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.0,
                model="test-model",
                verbose=False,
            )

        assert len(result["history"]) == 2

    def test_best_description_is_from_best_scoring_iteration(self, tmp_path):
        """Second iteration fails; best description should come from iteration 1."""
        eval_set = _make_eval_set(2, 0)
        skill_dir = _make_skill_md(tmp_path, description="original desc")

        call_count = [0]

        def alternating_eval(eval_set, skill_name, description, **kwargs):
            call_count[0] += 1
            if call_count[0] == 1:
                # First iteration: all pass
                return _all_pass_eval(eval_set, skill_name, description, **kwargs)
            else:
                # Second iteration: all fail (after description improved)
                return _all_fail_eval(eval_set, skill_name, description, **kwargs)

        with patch("scripts.run_loop.run_eval", side_effect=alternating_eval), \
             patch("scripts.run_loop.improve_description", return_value="improved desc"), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            result = run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override=None,
                num_workers=1,
                timeout=10,
                max_iterations=2,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.0,
                model="test-model",
                verbose=False,
            )

        # Iteration 1 used the original description and was all-pass
        assert result["best_description"] == "original desc"


# ---------------------------------------------------------------------------
# run_loop — description override
# ---------------------------------------------------------------------------

class TestRunLoopDescriptionOverride:
    def test_uses_override_description_instead_of_skill_md(self, tmp_path):
        eval_set = _make_eval_set(1, 0)
        skill_dir = _make_skill_md(tmp_path, description="skill md desc")

        seen_descriptions = []

        def capture_eval(eval_set, skill_name, description, **kwargs):
            seen_descriptions.append(description)
            return _all_pass_eval(eval_set, skill_name, description, **kwargs)

        with patch("scripts.run_loop.run_eval", side_effect=capture_eval), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override="override desc",
                num_workers=1,
                timeout=10,
                max_iterations=1,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.0,
                model="test-model",
                verbose=False,
            )

        assert seen_descriptions[0] == "override desc"


# ---------------------------------------------------------------------------
# run_loop — train/test split
# ---------------------------------------------------------------------------

class TestRunLoopTrainTestSplit:
    def test_train_test_split_sizes_reported(self, tmp_path):
        eval_set = _make_eval_set(6, 4)
        skill_dir = _make_skill_md(tmp_path)

        with patch("scripts.run_loop.run_eval", side_effect=_all_pass_eval), \
             patch("scripts.run_loop.find_project_root", return_value=tmp_path):

            result = run_loop(
                eval_set=eval_set,
                skill_path=skill_dir,
                description_override=None,
                num_workers=1,
                timeout=10,
                max_iterations=1,
                runs_per_query=1,
                trigger_threshold=0.5,
                holdout=0.4,
                model="test-model",
                verbose=False,
            )

        assert result["train_size"] + result["test_size"] == len(eval_set)
        assert result["test_size"] >= 1
