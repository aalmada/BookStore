"""Tests for scripts/run_exec.py.

CopilotClient is mocked throughout so no real CLI is needed.
"""

from __future__ import annotations

import json
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, call, patch

import pytest

from scripts.run_exec import (
    _collect_worktree_outputs,
    _extract_writes,
    _git_repo_root,
    _inactivity_watcher,
    _is_write_call,
    _parse_agent_body,
    _parse_tool_args,
    _run_async,
)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_skill(tmp_path: Path, name: str = "copilot-squad") -> Path:
    skill_path = tmp_path / "skill"
    skill_path.mkdir()
    (skill_path / "SKILL.md").write_text(
        f"---\nname: {name}\ndescription: A test squad skill.\n---\n# Instructions\nDo things.\n"
    )
    return skill_path


def _make_orchestrator(tmp_path: Path, body: str = "You are the APA orchestrator.") -> Path:
    f = tmp_path / "Orchestrator.agent.md"
    f.write_text(f"---\nname: Orchestrator\nmodel: gpt-5\n---\n\n{body}")
    return f


def _eval_item(eid: int = 1, prompt: str = "Add a GET /api/fornecedores endpoint") -> dict:
    return {
        "id": eid,
        "prompt": prompt,
        "assertions": [{"id": "creates-controller", "text": "A REST controller is created"}],
    }


def _make_session(write_args: dict | None = None) -> tuple[MagicMock, list]:
    """Return (session_mock, registered_handlers[]). Fires idle after optional write hook."""
    session = MagicMock()
    session.__aenter__ = AsyncMock(return_value=session)
    session.__aexit__ = AsyncMock(return_value=None)
    registered: list = []

    def fake_on(handler):
        registered.append(handler)

    session.on.side_effect = fake_on

    async def send_and_idle(query):
        # Fire any pre_tool_use hooks set via session config
        # (hooks are passed in create_session kwargs, not on session directly)
        idle = MagicMock()
        idle.type.value = "session.idle"
        for h in registered:
            h(idle)

    session.send = AsyncMock(side_effect=send_and_idle)
    return session, registered


def _make_client_with_hook_support(write_args: dict | None = None):
    """Build a client that lets us fire the pre_tool_use hook during send."""
    captured_configs: list[dict] = []

    async def fake_create_session(config):
        captured_configs.append(config)
        session = MagicMock()
        session.__aenter__ = AsyncMock(return_value=session)
        session.__aexit__ = AsyncMock(return_value=None)
        registered: list = []

        def fake_on(handler):
            registered.append(handler)

        session.on.side_effect = fake_on

        async def send_and_idle(query):
            pre_hook = config.get("hooks", {}).get("on_pre_tool_use")
            if pre_hook and write_args:
                # Pass toolArgs as a JSON string to simulate real SDK behavior.
                await pre_hook(
                    {"toolName": "create", "toolArgs": json.dumps(write_args)}, None
                )
            idle = MagicMock()
            idle.type.value = "session.idle"
            for h in registered:
                h(idle)

        session.send = AsyncMock(side_effect=send_and_idle)
        return session

    client = MagicMock()
    client.start = AsyncMock()
    client.stop = AsyncMock()
    client.create_session = AsyncMock(side_effect=fake_create_session)
    return client, captured_configs


# ---------------------------------------------------------------------------
# _is_write_call
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# _parse_tool_args
# ---------------------------------------------------------------------------

class TestParseToolArgs:
    def test_dict_passthrough(self):
        args = {"path": "/x.md", "content": "hello"}
        assert _parse_tool_args(args) == args

    def test_json_string_parsed(self):
        args = {"file_text": "body", "path": "/y.cs"}
        assert _parse_tool_args(json.dumps(args)) == args

    def test_invalid_json_returns_empty(self):
        assert _parse_tool_args("not json") == {}

    def test_non_dict_json_returns_empty(self):
        assert _parse_tool_args("[1, 2, 3]") == {}

    def test_none_returns_empty(self):
        assert _parse_tool_args(None) == {}


class TestIsWriteCall:
    def test_known_write_tool_name(self):
        assert _is_write_call("write_file", {})
        assert _is_write_call("create_file", {})
        assert _is_write_call("str_replace_editor", {})

    def test_create_tool_name_recognised(self):
        """SDK 'create' tool must be recognised as a write operation."""
        assert _is_write_call("create", {})

    def test_heuristic_path_and_content(self):
        assert _is_write_call("some_tool", {"path": "/x.md", "content": "hello"})
        assert _is_write_call("custom", {"filePath": "/y.ts", "newString": "code"})

    def test_file_text_key_recognised(self):
        """SDK 'create' tool passes content as 'file_text'; must be recognised."""
        # 'create' is in _WRITE_TOOL_NAMES so the name check fires first, but
        # also verify the heuristic works for unknown tool names using file_text.
        assert _is_write_call("unknown_creator", {"path": "/x.cs", "file_text": "body"})

    def test_not_a_write_tool(self):
        assert not _is_write_call("read_file", {"path": "/x.md"})
        assert not _is_write_call("search_files", {"query": "foo"})


# ---------------------------------------------------------------------------
# _extract_writes
# ---------------------------------------------------------------------------

class TestExtractWrites:
    def test_single_file(self):
        args = {"filePath": "/agents/Orchestrator.agent.md", "content": "---\nname: Orchestrator\n---"}
        pairs = _extract_writes(args)
        assert len(pairs) == 1
        assert pairs[0][0] == "/agents/Orchestrator.agent.md"

    def test_file_text_key(self):
        """SDK 'create' tool uses 'file_text'; content must be extracted."""
        args = {"path": "/src/Foo.cs", "file_text": "namespace Foo;"}
        pairs = _extract_writes(args)
        assert len(pairs) == 1
        assert pairs[0][0] == "/src/Foo.cs"
        assert pairs[0][1] == "namespace Foo;"

    def test_multi_replace(self):
        args = {
            "replacements": [
                {"filePath": "/a.md", "newString": "v1"},
                {"filePath": "/b.md", "newString": "v2"},
            ]
        }
        assert len(_extract_writes(args)) == 2

    def test_empty_args(self):
        assert _extract_writes({}) == []


# ---------------------------------------------------------------------------
# _parse_agent_body
# ---------------------------------------------------------------------------

class TestParseAgentBody:
    def test_strips_frontmatter(self, tmp_path):
        f = tmp_path / "Orchestrator.agent.md"
        f.write_text("---\nname: Orchestrator\nmodel: gpt-5\n---\n\nYou are the orchestrator.")
        assert _parse_agent_body(f) == "You are the orchestrator."

    def test_no_frontmatter_returns_full(self, tmp_path):
        f = tmp_path / "agent.md"
        f.write_text("No frontmatter here.")
        assert _parse_agent_body(f) == "No frontmatter here."

    def test_preserves_body_with_dashes(self, tmp_path):
        f = tmp_path / "agent.md"
        f.write_text("---\nname: X\n---\nBody with --- dashes inside.")
        assert "Body with" in _parse_agent_body(f)


# ---------------------------------------------------------------------------
# _inactivity_watcher
# ---------------------------------------------------------------------------

class TestInactivityWatcher:
    @pytest.mark.asyncio
    async def test_sets_done_after_inactivity(self):
        """Watcher must set done when last_activity is old enough."""
        import asyncio, time
        done = asyncio.Event()
        last_activity = [time.monotonic() - 100]  # already 100 s stale
        errors_ref = [0]
        await _inactivity_watcher(done, last_activity, inactivity_timeout=1, errors_ref=errors_ref)
        assert done.is_set()
        assert errors_ref[0] == 1

    @pytest.mark.asyncio
    async def test_does_not_fire_while_active(self):
        """Watcher must not fire when activity keeps the clock fresh."""
        import asyncio, time
        done = asyncio.Event()
        last_activity = [time.monotonic()]  # fresh
        errors_ref = [0]

        # Kick off watcher with a 0.3 s timeout; cancel it quickly
        task = asyncio.create_task(
            _inactivity_watcher(done, last_activity, inactivity_timeout=0.3, errors_ref=errors_ref)
        )
        # Simulate two bursts of activity within the timeout window
        await asyncio.sleep(0.05)
        last_activity[0] = time.monotonic()
        await asyncio.sleep(0.05)
        last_activity[0] = time.monotonic()
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass
        # done was never set by the watcher (activity kept resetting the clock)
        assert not done.is_set()

    @pytest.mark.asyncio
    async def test_stops_cleanly_if_done_already_set(self):
        """Watcher exits immediately when done is preset."""
        import asyncio, time
        done = asyncio.Event()
        done.set()
        last_activity = [time.monotonic() - 100]
        errors_ref = [0]
        await _inactivity_watcher(done, last_activity, inactivity_timeout=1, errors_ref=errors_ref)
        # errors_ref stays 0 because the while-loop exits before the stale check
        assert errors_ref[0] == 0


# ---------------------------------------------------------------------------
# _git_repo_root
# ---------------------------------------------------------------------------

class TestGitRepoRoot:
    def test_returns_path_from_git(self, tmp_path):
        fake_root = "/home/user/repo"
        with patch("scripts.run_exec.subprocess.run") as mock_run:
            mock_run.return_value = MagicMock(stdout=fake_root + "\n")
            result = _git_repo_root(tmp_path)
        assert result == Path(fake_root)
        mock_run.assert_called_once()
        args = mock_run.call_args[0][0]
        assert args[:2] == ["git", "rev-parse"]


# ---------------------------------------------------------------------------
# _collect_worktree_outputs
# ---------------------------------------------------------------------------

class TestCollectWorktreeOutputs:
    def test_copies_modified_and_untracked(self, tmp_path):
        worktree = tmp_path / "wt"
        worktree.mkdir()
        task_dir = tmp_path / "task"
        task_dir.mkdir()

        # Put a file in the worktree that "git diff" will report
        (worktree / "Foo.cs").write_text("class Foo {}")

        def fake_run(cmd, **kwargs):
            m = MagicMock()
            if "diff" in cmd:
                m.stdout = "Foo.cs\n"
            else:
                m.stdout = ""
            return m

        with patch("scripts.run_exec.subprocess.run", side_effect=fake_run):
            captured = _collect_worktree_outputs(worktree, task_dir)

        assert "Foo.cs" in captured
        assert (task_dir / "Foo.cs").read_text() == "class Foo {}"

    def test_deduplicates_modified_and_untracked(self, tmp_path):
        worktree = tmp_path / "wt"
        worktree.mkdir()
        task_dir = tmp_path / "task"
        task_dir.mkdir()
        (worktree / "Bar.cs").write_text("class Bar {}")

        def fake_run(cmd, **kwargs):
            m = MagicMock()
            # Same file appears in both diff and ls-files output
            m.stdout = "Bar.cs\n"
            return m

        with patch("scripts.run_exec.subprocess.run", side_effect=fake_run):
            captured = _collect_worktree_outputs(worktree, task_dir)

        assert captured.count("Bar.cs") == 1

    def test_skips_missing_files(self, tmp_path):
        """Entries from git diff that don't exist on disk are silently skipped."""
        worktree = tmp_path / "wt"
        worktree.mkdir()
        task_dir = tmp_path / "task"
        task_dir.mkdir()

        def fake_run(cmd, **kwargs):
            m = MagicMock()
            m.stdout = "Ghost.cs\n"
            return m

        with patch("scripts.run_exec.subprocess.run", side_effect=fake_run):
            captured = _collect_worktree_outputs(worktree, task_dir)

        assert captured == []


# ---------------------------------------------------------------------------
# _run_async — without_squad
# ---------------------------------------------------------------------------

class TestRunAsyncWithoutSquad:
    @pytest.mark.asyncio
    async def test_no_system_message(self, tmp_path):
        """without_squad must not inject a system_message."""
        skill_path = _make_skill(tmp_path)
        client, captured_configs = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(_eval_item(), skill_path, tmp_path / "run", False, 5, None)

        assert len(captured_configs) == 1
        assert "system_message" not in captured_configs[0]

    @pytest.mark.asyncio
    async def test_only_one_session_created(self, tmp_path):
        """without_squad must use exactly one session."""
        skill_path = _make_skill(tmp_path)
        client, captured_configs = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(_eval_item(), skill_path, tmp_path / "run", False, 5, None)

        assert len(captured_configs) == 1

    @pytest.mark.asyncio
    async def test_writes_captured_to_task_dir(self, tmp_path):
        """Files written by the model should appear in outputs/task/."""
        skill_path = _make_skill(tmp_path)
        write_args = {"filePath": "/workspace/src/Controller.java", "content": "class Ctrl {}"}
        client, _ = _make_client_with_hook_support(write_args=write_args)

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            metrics = await _run_async(_eval_item(), skill_path, tmp_path / "run", False, 5, None)

        assert "Controller.java" in metrics["files_created"]
        assert (tmp_path / "run" / "outputs" / "task" / "Controller.java").exists()

    @pytest.mark.asyncio
    async def test_metadata_records_without_squad(self, tmp_path):
        """eval_metadata.json must record with_squad=False."""
        skill_path = _make_skill(tmp_path)
        client, _ = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(_eval_item(eid=5, prompt="Add route"),
                              skill_path, tmp_path / "run", False, 5, None)

        meta = json.loads((tmp_path / "run" / "eval_metadata.json").read_text())
        assert meta["eval_id"] == 5
        assert meta["with_squad"] is False

    @pytest.mark.asyncio
    async def test_metadata_records_exec_model_default(self, tmp_path):
        """eval_metadata.json must record exec_model='(default)' when no model is given."""
        skill_path = _make_skill(tmp_path)
        client, _ = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(_eval_item(eid=6, prompt="Add route"),
                              skill_path, tmp_path / "run", False, 5, None)

        meta = json.loads((tmp_path / "run" / "eval_metadata.json").read_text())
        assert meta.get("exec_model") == "(default)"

    @pytest.mark.asyncio
    async def test_metadata_records_exec_model_named(self, tmp_path):
        """eval_metadata.json must record the model string when one is specified."""
        skill_path = _make_skill(tmp_path)
        client, _ = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(_eval_item(eid=7, prompt="Add route"),
                              skill_path, tmp_path / "run", False, 5, "gpt-4o")

        meta = json.loads((tmp_path / "run" / "eval_metadata.json").read_text())
        assert meta.get("exec_model") == "gpt-4o"

    @pytest.mark.asyncio
    async def test_metrics_records_model(self, tmp_path):
        """metrics.json must contain the 'model' field."""
        skill_path = _make_skill(tmp_path)
        client, _ = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            metrics = await _run_async(_eval_item(eid=8, prompt="Add route"),
                                       skill_path, tmp_path / "run", False, 5, "claude-3")

        assert metrics.get("model") == "claude-3"


# ---------------------------------------------------------------------------
# _run_async — with_squad (uses existing Orchestrator)
# ---------------------------------------------------------------------------

class TestRunAsyncWithSquad:
    @pytest.mark.asyncio
    async def test_requires_orchestrator_agent(self, tmp_path):
        """with_squad=True without orchestrator_agent must raise ValueError."""
        skill_path = _make_skill(tmp_path)
        client, _ = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            with pytest.raises(ValueError, match="orchestrator_agent is required"):
                await _run_async(_eval_item(), skill_path, tmp_path / "run", True, 5, None)

    @pytest.mark.asyncio
    async def test_only_one_session_created(self, tmp_path):
        """with_squad must use exactly one session (no squad creation phase)."""
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        client, captured_configs = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(
                _eval_item(), skill_path, tmp_path / "run", True, 5, None,
                orchestrator_agent=orchestrator,
            )

        assert len(captured_configs) == 1

    @pytest.mark.asyncio
    async def test_uses_orchestrator_body_as_system_message(self, tmp_path):
        """system_message should contain the Orchestrator agent body."""
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path, body="You are the APA orchestrator.")
        client, captured_configs = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(
                _eval_item(), skill_path, tmp_path / "run", True, 5, None,
                orchestrator_agent=orchestrator,
            )

        sm = captured_configs[0].get("system_message", {})
        assert "APA orchestrator" in sm.get("content", "")

    @pytest.mark.asyncio
    async def test_sends_eval_prompt(self, tmp_path):
        """The eval item's prompt must be sent to the session."""
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        sent_prompts: list[str] = []

        async def fake_create_session(config):
            session = MagicMock()
            session.__aenter__ = AsyncMock(return_value=session)
            session.__aexit__ = AsyncMock(return_value=None)
            registered: list = []
            session.on.side_effect = lambda h: registered.append(h)

            async def send_and_idle(query):
                sent_prompts.append(query.get("prompt", ""))
                idle = MagicMock()
                idle.type.value = "session.idle"
                for h in registered:
                    h(idle)

            session.send = AsyncMock(side_effect=send_and_idle)
            return session

        client = MagicMock()
        client.start = AsyncMock()
        client.stop = AsyncMock()
        client.create_session = AsyncMock(side_effect=fake_create_session)

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(
                _eval_item(prompt="Add GET /api/fornecedores"),
                skill_path, tmp_path / "run", True, 5, None,
                orchestrator_agent=orchestrator,
            )

        assert sent_prompts == ["Add GET /api/fornecedores"]

    @pytest.mark.asyncio
    async def test_writes_captured_to_task_dir(self, tmp_path):
        """Files written during an orchestrated run go to outputs/task/."""
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        write_args = {"filePath": "/src/FornecedorController.java", "content": "class FCtrl {}"}
        client, _ = _make_client_with_hook_support(write_args=write_args)

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            metrics = await _run_async(
                _eval_item(), skill_path, tmp_path / "run", True, 5, None,
                orchestrator_agent=orchestrator,
            )

        assert "FornecedorController.java" in metrics["files_created"]
        assert (tmp_path / "run" / "outputs" / "task" / "FornecedorController.java").exists()

    @pytest.mark.asyncio
    async def test_metadata_records_orchestrator(self, tmp_path):
        """eval_metadata.json must record with_squad=True and orchestrator_agent path."""
        skill_path = _make_skill(tmp_path)
        orchestrator = _make_orchestrator(tmp_path)
        client, _ = _make_client_with_hook_support()

        with patch("scripts.run_exec.CopilotClient", return_value=client):
            await _run_async(
                _eval_item(eid=2, prompt="Add feature"),
                skill_path, tmp_path / "run", True, 5, None,
                orchestrator_agent=orchestrator,
            )

        meta = json.loads((tmp_path / "run" / "eval_metadata.json").read_text())
        assert meta["eval_id"] == 2
        assert meta["with_squad"] is True
        assert str(orchestrator) in meta.get("orchestrator_agent", "")
        assert "squad_suffix" not in meta
        assert "creation_prompt" not in meta


# ---------------------------------------------------------------------------
# Worktree integration (subprocess mocked)
# ---------------------------------------------------------------------------

class TestRunAsyncWorktree:
    @pytest.mark.asyncio
    async def test_worktree_created_and_removed(self, tmp_path):
        """With use_worktree=True, a worktree is created before and removed after the session."""
        skill_path = _make_skill(tmp_path)
        client, _ = _make_client_with_hook_support()

        subprocess_calls: list[list[str]] = []

        def fake_subprocess_run(cmd, **kwargs):
            subprocess_calls.append(list(cmd))
            m = MagicMock()
            if "rev-parse" in cmd:
                m.stdout = str(tmp_path) + "\n"
            else:
                m.stdout = ""
            return m

        with (
            patch("scripts.run_exec.CopilotClient", return_value=client),
            patch("scripts.run_exec.subprocess.run", side_effect=fake_subprocess_run),
        ):
            await _run_async(
                _eval_item(), skill_path, tmp_path / "run", False, 5, None,
                use_worktree=True,
            )

        cmds = [" ".join(c) for c in subprocess_calls]
        assert any("worktree add" in c for c in cmds), "worktree add not called"
        assert any("worktree remove" in c for c in cmds), "worktree remove not called"

    @pytest.mark.asyncio
    async def test_worktree_path_injected_into_prompt(self, tmp_path):
        """With use_worktree=True, the prompt sent to the session mentions the worktree path."""
        skill_path = _make_skill(tmp_path)
        sent_prompts: list[str] = []

        async def fake_create_session(config):
            session = MagicMock()
            session.__aenter__ = AsyncMock(return_value=session)
            session.__aexit__ = AsyncMock(return_value=None)
            registered: list = []
            session.on.side_effect = lambda h: registered.append(h)

            async def send_and_idle(query):
                sent_prompts.append(query.get("prompt", ""))
                idle = MagicMock()
                idle.type.value = "session.idle"
                for h in registered:
                    h(idle)

            session.send = AsyncMock(side_effect=send_and_idle)
            return session

        client = MagicMock()
        client.start = AsyncMock()
        client.stop = AsyncMock()
        client.create_session = AsyncMock(side_effect=fake_create_session)

        def fake_subprocess_run(cmd, **kwargs):
            m = MagicMock()
            m.stdout = str(tmp_path) + "\n" if "rev-parse" in cmd else ""
            return m

        with (
            patch("scripts.run_exec.CopilotClient", return_value=client),
            patch("scripts.run_exec.subprocess.run", side_effect=fake_subprocess_run),
        ):
            await _run_async(
                _eval_item(prompt="Do the task"), skill_path, tmp_path / "run",
                False, 5, None, use_worktree=True,
            )

        assert len(sent_prompts) == 1
        assert "worktree" in sent_prompts[0].lower()
        assert "Do the task" in sent_prompts[0]
