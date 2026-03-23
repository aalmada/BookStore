"""Tests for scripts/run_eval.py.

CopilotClient is mocked throughout so no real CLI is needed.
"""

import asyncio
import json
import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from scripts.run_eval import (
    find_project_root,
    run_eval,
    run_single_query,
    run_single_query_async,
)


# ---------------------------------------------------------------------------
# find_project_root
# ---------------------------------------------------------------------------

class TestFindProjectRoot:
    def test_finds_ancestor_with_github_dir(self, tmp_path):
        (tmp_path / ".github").mkdir()
        nested = tmp_path / "a" / "b" / "c"
        nested.mkdir(parents=True)
        with patch("scripts.run_eval.Path.cwd", return_value=nested):
            result = find_project_root()
        assert result == tmp_path

    def test_falls_back_to_cwd_when_no_github_dir(self, tmp_path):
        with patch("scripts.run_eval.Path.cwd", return_value=tmp_path):
            result = find_project_root()
        assert result == tmp_path


# ---------------------------------------------------------------------------
# run_single_query_async — file lifecycle
# ---------------------------------------------------------------------------

def _make_mock_client(triggered: bool):
    """Build a minimal CopilotClient mock that fires session.idle."""

    # Session mock — context manager, event subscription, send
    session = MagicMock()
    session.__aenter__ = AsyncMock(return_value=session)
    session.__aexit__ = AsyncMock(return_value=None)

    captured_handlers = {}

    def fake_on(handler):
        captured_handlers["handler"] = handler

    async def fake_send(query):
        # If triggered, simulate on_pre_tool_use having already fired from
        # within session setup; then fire session.idle to unblock done.wait().
        # We reach into the test fixture via the event name.
        event = MagicMock()
        event.type.value = "session.idle"
        if "handler" in captured_handlers:
            captured_handlers["handler"](event)

    session.on.side_effect = fake_on
    session.send = AsyncMock(side_effect=fake_send)
    session.disconnect = AsyncMock()

    # CopilotClient mock
    client = MagicMock()
    client.start = AsyncMock()
    client.stop = AsyncMock()
    client.create_session = AsyncMock(return_value=session)

    return client, captured_handlers


class TestRunSingleQueryAsync:
    @pytest.mark.asyncio
    async def test_creates_and_removes_skill_file(self, tmp_path):
        (tmp_path / ".github" / "skills").mkdir(parents=True)
        client, _ = _make_mock_client(triggered=False)

        await run_single_query_async(
            client=client,
            query="hello",
            skill_name="test-skill",
            skill_description="A useful skill.",
            timeout=5,
            project_root=str(tmp_path),
        )

        # No leftover skill directories under .github/skills
        skills_dir = tmp_path / ".github" / "skills"
        remaining = list(skills_dir.iterdir())
        assert remaining == [], f"Leftover files: {remaining}"

    @pytest.mark.asyncio
    async def test_skill_file_content_is_valid_yaml_frontmatter(self, tmp_path):
        (tmp_path / ".github" / "skills").mkdir(parents=True)
        created_contents = {}

        original_write_text = Path.write_text

        def capturing_write_text(self, content, *args, **kwargs):
            created_contents[str(self)] = content
            return original_write_text(self, content, *args, **kwargs)

        client, _ = _make_mock_client(triggered=False)

        with patch.object(Path, "write_text", capturing_write_text):
            await run_single_query_async(
                client=client,
                query="hello",
                skill_name="test-skill",
                skill_description="A useful skill.",
                timeout=5,
                project_root=str(tmp_path),
            )

        assert created_contents, "No SKILL.md was written"
        content = next(iter(created_contents.values()))
        assert content.startswith("---\n")
        assert "description: |" in content
        assert "test-skill" in content

    @pytest.mark.asyncio
    async def test_returns_false_when_not_triggered(self, tmp_path):
        (tmp_path / ".github" / "skills").mkdir(parents=True)
        client, _ = _make_mock_client(triggered=False)

        result = await run_single_query_async(
            client=client,
            query="unrelated query",
            skill_name="test-skill",
            skill_description="A useful skill.",
            timeout=5,
            project_root=str(tmp_path),
        )

        assert result is False

    @pytest.mark.asyncio
    async def test_triggered_flag_set_by_on_pre_tool_use(self, tmp_path):
        """When on_pre_tool_use fires with matching skill name, returns True."""
        (tmp_path / ".github" / "skills").mkdir(parents=True)

        session = MagicMock()
        session.__aenter__ = AsyncMock(return_value=session)
        session.__aexit__ = AsyncMock(return_value=None)
        session.disconnect = AsyncMock()

        hook_ref = {}

        async def capture_create_session(config):
            # Pull out the hook so we can call it ourselves
            hook_ref["on_pre_tool_use"] = config.get("hooks", {}).get("on_pre_tool_use")

            event = MagicMock()
            event.type.value = "session.idle"

            async def fake_send(query):
                # Fire the hook with a matching tool call
                if hook_ref.get("on_pre_tool_use"):
                    # We need to know the clean_name — it's derived from skill_name
                    # We can't predict the uuid suffix, so we scan session calls to
                    # find the system_message that was passed.
                    sm = config.get("system_message", {}).get("content", "")
                    # Extract the skill file path line to get clean_name
                    for line in sm.splitlines():
                        if line.startswith("Skill name: "):
                            skill_file_name = line[len("Skill name: "):]
                            await hook_ref["on_pre_tool_use"](
                                {"toolArgs": {"path": f".github/skills/{skill_file_name}/SKILL.md"}},
                                {},
                            )
                            break
                if session._event_handler:
                    session._event_handler(event)

            session.send = AsyncMock(side_effect=fake_send)
            session._event_handler = None

            def fake_on(handler):
                session._event_handler = handler

            session.on.side_effect = fake_on
            return session

        client = MagicMock()
        client.start = AsyncMock()
        client.stop = AsyncMock()
        client.create_session = AsyncMock(side_effect=capture_create_session)

        result = await run_single_query_async(
            client=client,
            query="do something useful",
            skill_name="test-skill",
            skill_description="Handles useful things.",
            timeout=5,
            project_root=str(tmp_path),
        )

        assert result is True


# ---------------------------------------------------------------------------
# run_eval — result aggregation (no real I/O)
# ---------------------------------------------------------------------------

class TestRunEval:
    def test_all_pass_when_triggers_match_expectations(self, tmp_path):
        eval_set = [
            {"query": "should trigger", "should_trigger": True},
            {"query": "should not trigger", "should_trigger": False},
        ]

        async def fake_run_eval_async(**kwargs):
            return {
                "skill_name": "s",
                "description": "d",
                "results": [
                    {"query": "should trigger", "should_trigger": True,
                     "trigger_rate": 1.0, "triggers": 1, "runs": 1, "pass": True},
                    {"query": "should not trigger", "should_trigger": False,
                     "trigger_rate": 0.0, "triggers": 0, "runs": 1, "pass": True},
                ],
                "summary": {"total": 2, "passed": 2, "failed": 0},
            }

        with patch("scripts.run_eval._run_eval_async", new=fake_run_eval_async):
            output = run_eval(
                eval_set=eval_set,
                skill_name="s",
                description="d",
                num_workers=1,
                timeout=5,
                project_root=tmp_path,
            )

        assert output["summary"]["passed"] == 2
        assert output["summary"]["failed"] == 0

    def test_result_shape(self, tmp_path):
        async def fake_run_eval_async(**kwargs):
            return {
                "skill_name": "s",
                "description": "d",
                "results": [
                    {"query": "q", "should_trigger": True,
                     "trigger_rate": 0.0, "triggers": 0, "runs": 1, "pass": False},
                ],
                "summary": {"total": 1, "passed": 0, "failed": 1},
            }

        with patch("scripts.run_eval._run_eval_async", new=fake_run_eval_async):
            output = run_eval(
                eval_set=[{"query": "q", "should_trigger": True}],
                skill_name="s",
                description="d",
                num_workers=1,
                timeout=5,
                project_root=tmp_path,
            )

        assert "skill_name" in output
        assert "description" in output
        assert "results" in output
        assert "summary" in output
        r = output["results"][0]
        for key in ("query", "should_trigger", "trigger_rate", "triggers", "runs", "pass"):
            assert key in r


# ---------------------------------------------------------------------------
# _run_eval_async — aggregation logic
# ---------------------------------------------------------------------------

class TestRunEvalAsyncAggregation:
    """Tests the real aggregation logic inside _run_eval_async with a mocked
    run_single_query_async that returns predetermined results."""

    @pytest.mark.asyncio
    async def test_trigger_threshold_passes_above(self, tmp_path):
        from scripts.run_eval import _run_eval_async

        call_count = 0

        async def always_trigger(client, query, name, desc, timeout, root, model=None):
            nonlocal call_count
            call_count += 1
            return True

        mock_client = MagicMock()
        mock_client.start = AsyncMock()
        mock_client.stop = AsyncMock()

        with patch("scripts.run_eval.CopilotClient", return_value=mock_client), \
             patch("scripts.run_eval.run_single_query_async", side_effect=always_trigger):
            result = await _run_eval_async(
                eval_set=[{"query": "q", "should_trigger": True}],
                skill_name="s",
                description="d",
                num_workers=1,
                timeout=5,
                project_root=tmp_path,
                runs_per_query=3,
                trigger_threshold=0.5,
            )

        assert result["summary"]["passed"] == 1
        assert call_count == 3  # 3 runs requested

    @pytest.mark.asyncio
    async def test_false_trigger_counts_as_failure(self, tmp_path):
        from scripts.run_eval import _run_eval_async

        async def always_trigger(client, query, name, desc, timeout, root, model=None):
            return True

        mock_client = MagicMock()
        mock_client.start = AsyncMock()
        mock_client.stop = AsyncMock()

        with patch("scripts.run_eval.CopilotClient", return_value=mock_client), \
             patch("scripts.run_eval.run_single_query_async", side_effect=always_trigger):
            result = await _run_eval_async(
                eval_set=[{"query": "q", "should_trigger": False}],
                skill_name="s",
                description="d",
                num_workers=1,
                timeout=5,
                project_root=tmp_path,
                runs_per_query=1,
                trigger_threshold=0.5,
            )

        assert result["summary"]["passed"] == 0
        assert result["summary"]["failed"] == 1
