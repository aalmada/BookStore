#!/usr/bin/env python3
"""Execute a single eval prompt via the Copilot SDK.

Two evaluation modes:
  --with-squad    : Runs the eval task prompt through an existing Orchestrator
                    agent (required --orchestrator argument). The squad must be
                    created separately before running the benchmark.
  --without-squad : Runs the eval task prompt directly against the default agent.

Output layout:
    <output-dir>/
        outputs/
            task/         # Files written by the model during the task
            metrics.json  # Aggregate metrics
        eval_metadata.json
        timing.json

Usage:
    python -m scripts.run_exec --eval-id 1 --eval-set evals/squad-evals.json \\
        --skill-path . --output-dir /tmp/bench/eval-1/with_squad/run-1 --with-squad \\
        [--inactivity-timeout 60]
    python -m scripts.run_exec --eval-id 1 --eval-set evals/squad-evals.json \\
        --skill-path . --output-dir /tmp/bench/eval-1/without_squad/run-1 --without-squad

Inactivity timeout (--inactivity-timeout):
    The session ends after this many seconds with no tool call or model event.
    There is no hard wall-clock limit.

Worktree isolation:
    Creates a fresh `git worktree` for each run so the model writes into an
    isolated copy of the repository.  After the session the worktree is diffed
    against HEAD and changed files are copied to outputs/task/.  The worktree
    is then removed.  This prevents earlier runs from polluting later ones.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

from copilot import CopilotClient, PermissionHandler

from scripts.utils import load_evals, parse_skill_md

# Tool call key patterns that indicate a file-write operation.
_WRITE_TOOL_NAMES = frozenset({
    "write_file", "create_file", "str_replace_editor", "edit_file",
    "replace_string_in_file", "multi_replace_string_in_file",
    "write", "WriteFile", "CreateFile",
    # VS Code Copilot SDK built-in tools
    "create",
})


def _parse_tool_args(raw: object) -> dict:
    """Normalise toolArgs to a dict — the SDK may pass it as a JSON string."""
    if isinstance(raw, dict):
        return raw
    if isinstance(raw, str):
        try:
            parsed = json.loads(raw)
            return parsed if isinstance(parsed, dict) else {}
        except (json.JSONDecodeError, ValueError):
            return {}
    return {}


def _is_write_call(tool_name: str, tool_args: dict) -> bool:
    """Return True if this tool call is writing file content."""
    if tool_name in _WRITE_TOOL_NAMES:
        return True
    keys = set(tool_args.keys())
    has_path = bool(keys & {"path", "filePath", "file_path", "filename"})
    has_content = bool(keys & {"content", "newString", "new_string", "file_text"})
    return has_path and has_content


def _extract_writes(tool_args: dict) -> list[tuple[str, str]]:
    """Extract (filepath, content) pairs from write tool args."""
    results: list[tuple[str, str]] = []

    if "replacements" in tool_args and isinstance(tool_args["replacements"], list):
        for rep in tool_args["replacements"]:
            path = rep.get("filePath") or rep.get("path", "")
            new = rep.get("newString") or rep.get("new_string", "")
            if path:
                results.append((path, new))
        return results

    path = (
        tool_args.get("filePath")
        or tool_args.get("file_path")
        or tool_args.get("path")
        or tool_args.get("filename")
        or ""
    )
    content = (
        tool_args.get("content")
        or tool_args.get("newString")
        or tool_args.get("new_string")
        or tool_args.get("file_text")
        or ""
    )
    if path:
        results.append((path, content))
    return results


def _parse_agent_body(agent_path: Path) -> str:
    """Read an .agent.md file and return only the body (YAML frontmatter stripped)."""
    content = agent_path.read_text(encoding="utf-8")
    if not content.startswith("---"):
        return content
    end = content.find("---", 3)
    if end == -1:
        return content
    return content[end + 3:].lstrip("\n")


# ---------------------------------------------------------------------------
# Inactivity watcher
# ---------------------------------------------------------------------------

async def _inactivity_watcher(
    done: asyncio.Event,
    last_activity: list[float],
    inactivity_timeout: float,
    errors_ref: list[int],
) -> None:
    """Set *done* when no tool call or model event has occurred for *inactivity_timeout* seconds."""
    poll = min(5.0, inactivity_timeout / 4)
    while not done.is_set():
        await asyncio.sleep(poll)
        if done.is_set():
            break
        if time.monotonic() - last_activity[0] > inactivity_timeout:
            print(f"  [timeout] No activity for {inactivity_timeout}s — marking done.", flush=True)
            errors_ref[0] += 1
            done.set()
            break


# ---------------------------------------------------------------------------
# Git worktree helpers
# ---------------------------------------------------------------------------

def _git_repo_root(path: Path) -> Path:
    """Return the root of the git repository that contains *path*."""
    result = subprocess.run(
        ["git", "rev-parse", "--show-toplevel"],
        cwd=path.resolve(),
        capture_output=True,
        text=True,
        check=True,
    )
    return Path(result.stdout.strip())


def _create_worktree(repo_root: Path, worktree_path: Path) -> None:
    """Create a detached git worktree at *worktree_path* from HEAD."""
    subprocess.run(
        ["git", "worktree", "add", "--detach", str(worktree_path), "HEAD"],
        cwd=repo_root,
        capture_output=True,
        check=True,
    )


def _remove_worktree(repo_root: Path, worktree_path: Path) -> None:
    """Remove a git worktree, falling back to shutil.rmtree on failure."""
    try:
        subprocess.run(
            ["git", "worktree", "remove", "--force", str(worktree_path)],
            cwd=repo_root,
            capture_output=True,
            check=True,
        )
    except subprocess.CalledProcessError:
        shutil.rmtree(worktree_path, ignore_errors=True)


def _collect_worktree_outputs(worktree_path: Path, task_dir: Path) -> list[str]:
    """Diff the worktree against HEAD and copy changed files to *task_dir*.

    Returns the list of captured filenames (basename only).
    Both tracked modifications and new untracked files are collected.
    """
    modified = subprocess.run(
        ["git", "diff", "HEAD", "--name-only"],
        cwd=worktree_path,
        capture_output=True,
        text=True,
    ).stdout.strip()

    untracked = subprocess.run(
        ["git", "ls-files", "--others", "--exclude-standard"],
        cwd=worktree_path,
        capture_output=True,
        text=True,
    ).stdout.strip()

    rel_paths = list(dict.fromkeys(
        p.strip()
        for p in (modified + "\n" + untracked).split("\n")
        if p.strip()
    ))

    captured: list[str] = []
    for rel in rel_paths:
        src = worktree_path / rel
        if not src.is_file():
            continue
        safe_name = Path(rel).name or "output.txt"
        dest = task_dir / safe_name
        counter = 1
        while dest.exists():
            stem = Path(safe_name).stem
            sfx = Path(safe_name).suffix
            dest = task_dir / f"{stem}_{counter}{sfx}"
            counter += 1
        shutil.copy2(src, dest)
        captured.append(safe_name)
    return captured


# ---------------------------------------------------------------------------
# Event handler and pre-tool-use hook
# ---------------------------------------------------------------------------

def _make_event_handler(
    done: asyncio.Event,
    transcript_parts: list[str],
    errors_ref: list[int],
    last_activity: list[float],
    input_tokens_ref: list[int],
    output_tokens_ref: list[int],
):
    def handler(event):
        last_activity[0] = time.monotonic()  # any event resets the inactivity clock
        etype = event.type.value
        if etype == "assistant.message":
            # Collect every message but do NOT set done yet — the model may
            # emit an early empty turn-start message before writing files.
            transcript_parts.append(event.data.content)
            snippet = event.data.content[:80].replace("\n", " ")
            print(f"  [msg] {snippet}{'…' if len(event.data.content) > 80 else ''}", flush=True)
        elif etype == "assistant.usage":
            # Accumulate real token counts from the LLM API response.
            d = event.data
            input_tokens_ref[0] += int(d.input_tokens or 0)
            output_tokens_ref[0] += int(d.output_tokens or 0)
            print(
                f"  [usage] input={int(d.input_tokens or 0)} output={int(d.output_tokens or 0)}",
                flush=True,
            )
        elif etype == "session.idle":
            # Only mark done when the session is fully idle (all tool calls
            # and messages have completed).
            print("  [idle] Session idle — done.", flush=True)
            done.set()
        elif etype == "session.error":
            print(f"  [error] Session error: {getattr(event, 'data', event)}", flush=True)
            errors_ref[0] += 1
            done.set()
    return handler


def _capture_hook(
    task_dir: Path,
    tool_call_counts: dict,
    total_ref: list[int],
    captured_files: list[str],
    output_chars_ref: list[int],
    last_activity: list[float],
):
    """Return an on_pre_tool_use hook that saves written files to task_dir."""
    async def hook(input_data: dict, invocation) -> dict:
        last_activity[0] = time.monotonic()  # any tool call resets the inactivity clock
        tool_name = input_data.get("toolName", "")
        # The SDK may pass toolArgs as a JSON-serialised string; normalise to dict.
        tool_args = _parse_tool_args(input_data.get("toolArgs", {}))
        tool_call_counts[tool_name] = tool_call_counts.get(tool_name, 0) + 1
        total_ref[0] += 1
        print(f"  [tool #{total_ref[0]}] {tool_name}", flush=True)

        # Log all tool calls with their args to a debug file for diagnostics.
        debug_entry = {"tool": tool_name, "args": tool_args}
        debug_file = task_dir / "tool_calls_debug.jsonl"
        with debug_file.open("a", encoding="utf-8") as f:
            f.write(json.dumps(debug_entry) + "\n")

        if _is_write_call(tool_name, tool_args):
            for file_path, content in _extract_writes(tool_args):
                safe_name = Path(file_path).name or "output.txt"
                dest = task_dir / safe_name
                counter = 1
                while dest.exists():
                    stem = Path(safe_name).stem
                    sfx = Path(safe_name).suffix
                    dest = task_dir / f"{stem}_{counter}{sfx}"
                    counter += 1
                dest.write_text(content, encoding="utf-8")
                captured_files.append(safe_name)
                output_chars_ref[0] += len(content)
                print(f"  [file] Captured {safe_name} ({len(content)} chars)", flush=True)

        return {"permissionDecision": "allow", "modifiedArgs": tool_args}

    return hook


async def _run_session_async(
    eval_item: dict,
    skill_path: Path,
    output_dir: Path,
    inactivity_timeout: int,
    model: str | None,
    system_message_content: str | None,
    with_squad: bool,
    orchestrator_agent: Path | None,
    use_worktree: bool,
) -> dict:
    """Shared session runner for both with_squad and without_squad modes."""
    eval_id = eval_item.get("id", "?")
    mode = "with_squad" if with_squad else "without_squad"
    label = f"[eval-{eval_id}/{mode}]"

    task_dir = output_dir / "outputs" / "task"
    task_dir.mkdir(parents=True, exist_ok=True)

    prompt = eval_item.get("prompt", "")

    # --- worktree setup ---
    repo_root: Path | None = None
    worktree_path: Path | None = None
    if use_worktree:
        repo_root = _git_repo_root(skill_path)
        worktree_path = (output_dir / "worktree").resolve()
        print(f"{label} Creating worktree at {worktree_path} …", flush=True)
        _create_worktree(repo_root, worktree_path)
        print(f"{label} Worktree ready.", flush=True)
        prompt = (
            f"IMPORTANT: For this task, work exclusively in the git worktree at "
            f"`{worktree_path}`. All file reads and writes must use paths under "
            f"`{worktree_path}` instead of `{repo_root}`. For example, "
            f"`{repo_root}/src/BookStore.ApiService/` becomes "
            f"`{worktree_path}/src/BookStore.ApiService/`.\n\n" + prompt
        )

    last_activity: list[float] = [time.monotonic()]
    tool_call_counts: dict[str, int] = {}
    total_ref = [0]
    captured_files: list[str] = []
    output_chars_ref = [0]
    input_tokens_ref = [0]
    output_tokens_ref = [0]
    errors_ref = [0]
    transcript_parts: list[str] = []
    done = asyncio.Event()

    session_config: dict = {
        "on_permission_request": PermissionHandler.approve_all,
        # Exclude VS Code workflow tools that create plan tasks instead of
        # writing files directly.  Without this exclusion the model calls
        # `report_intent` → `task` and produces an empty response, because
        # the VS Code task-based workflow requires a human to accept the diff.
        "excluded_tools": ["report_intent", "task"],
        "hooks": {
            "on_pre_tool_use": _capture_hook(
                task_dir, tool_call_counts, total_ref, captured_files,
                output_chars_ref, last_activity,
            )
        },
    }
    if system_message_content:
        session_config["system_message"] = {"mode": "append", "content": system_message_content}
    if model:
        session_config["model"] = model

    print(f"{label} Starting Copilot session (model={model or '(default)'}) …", flush=True)
    client = CopilotClient()
    await client.start()
    start_ts = datetime.now(timezone.utc).isoformat()
    t0 = time.monotonic()

    watcher: asyncio.Task | None = None
    try:
        async with await client.create_session(**session_config) as session:
            print(f"{label} Session open. Sending prompt …", flush=True)
            session.on(_make_event_handler(
                done, transcript_parts, errors_ref, last_activity,
                input_tokens_ref, output_tokens_ref,
            ))
            await session.send(prompt)
            print(f"{label} Prompt sent. Waiting for session.idle (inactivity timeout={inactivity_timeout}s) …", flush=True)
            watcher = asyncio.create_task(
                _inactivity_watcher(done, last_activity, inactivity_timeout, errors_ref)
            )
            try:
                await done.wait()
            except asyncio.CancelledError:
                print(f"{label} Cancelled — stopping session.", flush=True)
                raise
            finally:
                if watcher and not watcher.done():
                    watcher.cancel()
        print(f"{label} Session closed. Tool calls={total_ref[0]}, files={len(captured_files)}.", flush=True)
    finally:
        try:
            await client.stop()
        except Exception as exc:  # noqa: BLE001
            print(f"{label} Warning: client.stop() raised {exc!r}", flush=True)

    elapsed = time.monotonic() - t0
    end_ts = datetime.now(timezone.utc).isoformat()
    print(f"{label} Elapsed: {elapsed:.1f}s", flush=True)

    # --- worktree output collection ---
    # git diff catches files written via bash commands that the hook can't see.
    if use_worktree and worktree_path is not None and repo_root is not None:
        try:
            print(f"{label} Collecting worktree diff outputs …", flush=True)
            wt_files = _collect_worktree_outputs(worktree_path, task_dir)
            # Merge: hook-captured names first (already written), then any
            # additional files found only via git diff.  Deduplicate by name.
            existing = set(captured_files)
            for name in wt_files:
                if name not in existing:
                    captured_files.append(name)
                    existing.add(name)
            print(f"{label} Worktree diff captured {len(wt_files)} file(s).", flush=True)
        finally:
            print(f"{label} Removing worktree …", flush=True)
            _remove_worktree(repo_root, worktree_path)
            print(f"{label} Worktree removed.", flush=True)

    prompt_label = "Task Prompt" if with_squad else "Eval Prompt"
    original_prompt = eval_item.get("prompt", "")
    transcript = "\n\n".join(transcript_parts)
    (task_dir / "transcript.md").write_text(
        f"## {prompt_label}\n\n{original_prompt}\n\n## Response\n\n{transcript}\n",
        encoding="utf-8",
    )

    metrics = {
        "tool_calls": tool_call_counts,
        "total_tool_calls": total_ref[0],
        "total_steps": len(transcript_parts),
        "files_created": captured_files,
        "errors_encountered": errors_ref[0],
        "output_chars": output_chars_ref[0],
        "input_tokens": input_tokens_ref[0],
        "output_tokens": output_tokens_ref[0],
        "transcript_chars": len(transcript),
        "model": model or "(default)",
    }
    (output_dir / "outputs" / "metrics.json").write_text(
        json.dumps(metrics, indent=2), encoding="utf-8"
    )

    timing = {
        "executor_start": start_ts,
        "executor_end": end_ts,
        "executor_duration_seconds": round(elapsed, 2),
        "total_duration_seconds": round(elapsed, 2),
    }
    (output_dir / "timing.json").write_text(json.dumps(timing, indent=2), encoding="utf-8")

    meta: dict = {
        "eval_id": eval_item.get("id"),
        "eval_name": eval_item.get("eval_name", f"eval-{eval_item.get('id', 0)}"),
        "task_prompt": original_prompt,
        "with_squad": with_squad,
        "exec_model": model or "(default)",
        "assertions": eval_item.get("assertions", []),
    }
    if orchestrator_agent is not None:
        meta["orchestrator_agent"] = str(orchestrator_agent)
    (output_dir / "eval_metadata.json").write_text(
        json.dumps(meta, indent=2), encoding="utf-8"
    )

    return metrics


async def _run_async(
    eval_item: dict,
    skill_path: Path,
    output_dir: Path,
    with_squad: bool,
    inactivity_timeout: int,
    model: str | None,
    orchestrator_agent: Path | None = None,
    use_worktree: bool = False,
) -> dict:
    """Dispatch to _run_session_async with the appropriate system message."""
    system_message_content: str | None = None
    if with_squad:
        if orchestrator_agent is None:
            raise ValueError("orchestrator_agent is required when with_squad=True")
        system_message_content = _parse_agent_body(orchestrator_agent)
    return await _run_session_async(
        eval_item, skill_path, output_dir, inactivity_timeout, model,
        system_message_content, with_squad, orchestrator_agent, use_worktree,
    )


def run_exec(
    eval_item: dict,
    skill_path: Path,
    output_dir: Path,
    with_squad: bool,
    inactivity_timeout: int = 60,
    model: str | None = None,
    orchestrator_agent: Path | None = None,
    use_worktree: bool = False,
) -> dict:
    """Synchronous entry point. Returns the metrics dict."""
    return asyncio.run(  # type: ignore[return-value]
        _run_async(
            eval_item=eval_item,
            skill_path=skill_path,
            output_dir=output_dir,
            with_squad=with_squad,
            inactivity_timeout=inactivity_timeout,
            model=model,
            orchestrator_agent=orchestrator_agent,
            use_worktree=use_worktree,
        )
    )


def main():
    parser = argparse.ArgumentParser(
        description="Execute a single eval prompt with or without a squad Orchestrator"
    )
    parser.add_argument("--eval-id", type=int, required=True, help="Eval ID from squad-evals.json")
    parser.add_argument("--eval-set", required=True, help="Path to squad-evals.json")
    parser.add_argument(
        "--skill-path", required=True, help="Path to skill directory (contains SKILL.md)"
    )
    parser.add_argument("--output-dir", required=True, help="Directory to write run artifacts")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument(
        "--with-squad",
        action="store_true",
        help="Run eval through an Orchestrator agent (requires --orchestrator)",
    )
    group.add_argument(
        "--without-squad",
        action="store_true",
        help="Run eval task directly without a squad",
    )
    parser.add_argument(
        "--inactivity-timeout",
        type=int,
        default=60,
        help="Session ends after this many seconds with no tool call or model event (default: 60)",
    )
    parser.add_argument("--model", default=None, help="Model to use")
    parser.add_argument(
        "--orchestrator",
        default=None,
        help="Path to the Orchestrator .agent.md file. Required with --with-squad.",
    )
    parser.add_argument("--verbose", action="store_true", help="Print progress to stderr")
    args = parser.parse_args()

    if args.with_squad and not args.orchestrator:
        print("Error: --orchestrator is required when using --with-squad", file=sys.stderr)
        sys.exit(1)

    skill_path = Path(args.skill_path)
    if not (skill_path / "SKILL.md").exists():
        print(f"Error: No SKILL.md found at {skill_path}", file=sys.stderr)
        sys.exit(1)

    orchestrator_agent: Path | None = None
    if args.orchestrator:
        orchestrator_agent = Path(args.orchestrator)
        if not orchestrator_agent.exists():
            print(f"Error: orchestrator file not found: {orchestrator_agent}", file=sys.stderr)
            sys.exit(1)

    evals = load_evals(Path(args.eval_set))
    matching = [e for e in evals if e.get("id") == args.eval_id]
    if not matching:
        print(f"Error: eval id {args.eval_id} not found in {args.eval_set}", file=sys.stderr)
        sys.exit(1)
    eval_item = matching[0]

    output_dir = Path(args.output_dir)
    metrics = run_exec(
        eval_item=eval_item,
        skill_path=skill_path,
        output_dir=output_dir,
        with_squad=args.with_squad,
        inactivity_timeout=args.inactivity_timeout,
        model=args.model,
        orchestrator_agent=orchestrator_agent,
        use_worktree=True,
    )

    if args.verbose:
        print(json.dumps(metrics, indent=2), file=sys.stderr)


if __name__ == "__main__":
    main()
