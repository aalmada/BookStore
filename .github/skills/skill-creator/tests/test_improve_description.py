"""Tests for scripts/improve_description.py.

_call_copilot is mocked so no real Copilot CLI is required.
"""

import asyncio
import json
from pathlib import Path
from unittest.mock import patch, AsyncMock

import pytest

from scripts.improve_description import improve_description


# ---------------------------------------------------------------------------
# Fixtures / helpers
# ---------------------------------------------------------------------------

MINIMAL_EVAL_RESULTS = {
    "description": "old description",
    "results": [],
    "summary": {"passed": 0, "total": 0, "failed": 0},
}


def _eval_results_with(failed_queries=None, false_queries=None):
    results = []
    for q in (failed_queries or []):
        results.append({"query": q, "should_trigger": True, "pass": False,
                        "triggers": 0, "runs": 3})
    for q in (false_queries or []):
        results.append({"query": q, "should_trigger": False, "pass": False,
                        "triggers": 3, "runs": 3})
    passed = sum(1 for r in results if r["pass"])
    return {
        "description": "old description",
        "results": results,
        "summary": {"passed": passed, "total": len(results), "failed": len(results) - passed},
    }


# ---------------------------------------------------------------------------
# Prompt construction
# ---------------------------------------------------------------------------

class TestImproveDescriptionPrompt:
    def test_current_description_in_prompt(self):
        captured_prompts = []

        def fake_run(coro):
            # Extract prompt from _call_copilot coroutine arg
            # The coroutine is _call_copilot(prompt, model)
            # We need to inspect it — simplest: patch at call site
            return "<new_description>improved</new_description>"

        with patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: "<new_description>improved</new_description>"):
            # Also capture what was passed to _call_copilot
            original_call = None

            async def capturing_call_copilot(prompt, model):
                captured_prompts.append(prompt)
                return "<new_description>improved</new_description>"

            with patch("scripts.improve_description._call_copilot",
                       side_effect=capturing_call_copilot):
                with patch("scripts.improve_description.asyncio.run",
                           side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
                    result = improve_description(
                        skill_name="my-skill",
                        skill_content="# My Skill\nDoes things.",
                        current_description="old description",
                        eval_results=MINIMAL_EVAL_RESULTS,
                        history=[],
                        model="gpt-5",
                    )

        assert result == "improved"
        assert captured_prompts, "No prompt was captured"
        prompt = captured_prompts[0]
        assert "old description" in prompt
        assert "my-skill" in prompt

    def test_failed_triggers_appear_in_prompt(self):
        eval_results = _eval_results_with(failed_queries=["how do I deploy"])

        captured_prompts = []

        async def capturing_call(prompt, model):
            captured_prompts.append(prompt)
            return "<new_description>better</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=capturing_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            improve_description(
                skill_name="s",
                skill_content="body",
                current_description="desc",
                eval_results=eval_results,
                history=[],
                model="gpt-5",
            )

        assert "how do I deploy" in captured_prompts[0]
        assert "FAILED TO TRIGGER" in captured_prompts[0]

    def test_false_triggers_appear_in_prompt(self):
        eval_results = _eval_results_with(false_queries=["write me a poem"])

        captured_prompts = []

        async def capturing_call(prompt, model):
            captured_prompts.append(prompt)
            return "<new_description>better</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=capturing_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            improve_description(
                skill_name="s",
                skill_content="body",
                current_description="desc",
                eval_results=eval_results,
                history=[],
                model="gpt-5",
            )

        assert "write me a poem" in captured_prompts[0]
        assert "FALSE TRIGGERS" in captured_prompts[0]

    def test_history_entries_appear_in_prompt(self):
        history = [{"description": "previous attempt", "passed": 2, "total": 5,
                    "failed": 3, "results": []}]

        captured_prompts = []

        async def capturing_call(prompt, model):
            captured_prompts.append(prompt)
            return "<new_description>better</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=capturing_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            improve_description(
                skill_name="s",
                skill_content="body",
                current_description="desc",
                eval_results=MINIMAL_EVAL_RESULTS,
                history=history,
                model="gpt-5",
            )

        assert "previous attempt" in captured_prompts[0]
        assert "PREVIOUS ATTEMPTS" in captured_prompts[0]


# ---------------------------------------------------------------------------
# Response parsing
# ---------------------------------------------------------------------------

class TestImproveDescriptionParsing:
    def test_extracts_new_description_from_tags(self):
        async def fake_call(prompt, model):
            return "<new_description>Extracted description.</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            result = improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        assert result == "Extracted description."

    def test_strips_surrounding_quotes(self):
        async def fake_call(prompt, model):
            return '<new_description>"Quoted description."</new_description>'

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            result = improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        assert result == "Quoted description."

    def test_falls_back_to_raw_text_without_tags(self):
        async def fake_call(prompt, model):
            return "Raw fallback description."

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            result = improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        assert result == "Raw fallback description."

    def test_multiline_description_in_tags(self):
        async def fake_call(prompt, model):
            return "<new_description>\n  Multi\n  line.\n</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            result = improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        assert "Multi" in result
        assert "line." in result


# ---------------------------------------------------------------------------
# 1024-character truncation safety net
# ---------------------------------------------------------------------------

class TestImproveDescriptionTruncation:
    def test_calls_copilot_twice_when_over_limit(self):
        over_limit = "x" * 1025
        short = "Short description."
        call_count = 0

        async def fake_call(prompt, model):
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return f"<new_description>{over_limit}</new_description>"
            return f"<new_description>{short}</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            result = improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        assert call_count == 2
        assert result == short

    def test_second_call_includes_over_limit_text(self):
        over_limit = "y" * 1025
        short = "Concise."
        second_prompts = []
        call_count = 0

        async def fake_call(prompt, model):
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return f"<new_description>{over_limit}</new_description>"
            second_prompts.append(prompt)
            return f"<new_description>{short}</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        assert second_prompts
        assert over_limit[:50] in second_prompts[0]
        assert "1024" in second_prompts[0]


# ---------------------------------------------------------------------------
# Log file writing
# ---------------------------------------------------------------------------

class TestImproveDescriptionLogging:
    def test_writes_log_file_when_log_dir_provided(self, tmp_path):
        async def fake_call(prompt, model):
            return "<new_description>logged description</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
                log_dir=tmp_path, iteration=1,
            )

        log_files = list(tmp_path.glob("improve_iter_*.json"))
        assert len(log_files) == 1
        transcript = json.loads(log_files[0].read_text())
        assert transcript["final_description"] == "logged description"
        assert transcript["iteration"] == 1

    def test_no_log_file_without_log_dir(self, tmp_path):
        async def fake_call(prompt, model):
            return "<new_description>no log</new_description>"

        with patch("scripts.improve_description._call_copilot", side_effect=fake_call), \
             patch("scripts.improve_description.asyncio.run",
                   side_effect=lambda coro: asyncio.get_event_loop().run_until_complete(coro)):
            improve_description(
                skill_name="s", skill_content="body",
                current_description="old",
                eval_results=MINIMAL_EVAL_RESULTS, history=[], model="m",
            )

        # tmp_path should remain empty
        assert list(tmp_path.iterdir()) == []
