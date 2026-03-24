#!/usr/bin/env python3
"""Run trigger evaluation for a skill description.

Tests whether a skill's description causes the AI client to trigger (read the skill)
for a set of queries. Outputs results as JSON.
"""

from __future__ import annotations

import argparse
import asyncio
import json
import sys
import uuid
from pathlib import Path

from copilot import CopilotClient, PermissionHandler

from scripts.utils import parse_skill_md


def find_project_root() -> Path:
    """Find the project root by walking up from cwd looking for .github/.

    Ensures the temporary skill file created during eval lands under the
    project's .github/skills/ directory, which is where the Copilot CLI
    looks for skill definitions.
    """
    current = Path.cwd()
    for parent in [current, *current.parents]:
        if (parent / ".github").is_dir():
            return parent
    return current


async def run_single_query_async(
    client: CopilotClient,
    query: str,
    skill_name: str,
    skill_description: str,
    timeout: int,
    project_root: str,
    model: str | None = None,
) -> bool:
    """Run a single query and return whether the skill was triggered.

    Injects the skill into the session via system_message and uses
    on_pre_tool_use to detect — and immediately deny — any read_file call
    targeting the skill.  No files are written to the workspace, avoiding
    VS Code file-watcher churn when many queries run in parallel.
    The ``project_root`` parameter is retained for API compatibility but
    is no longer used.
    """
    unique_id = uuid.uuid4().hex[:8]
    clean_name = f"{skill_name}-skill-{unique_id}"
    # Point to /tmp so no workspace files are touched (VS Code watches .github/).
    # The file does not need to exist: detection fires in on_pre_tool_use before
    # the read executes, and we deny the call immediately after.
    skill_file_ref = f"/tmp/copilot-skill-evals/{clean_name}/SKILL.md"
    system_message = {
        "content": (
            "You have access to the following skills. When a user's request matches "
            "a skill's description, invoke the skill by reading its file with the "
            "read_file tool.\n\n"
            f"Skill name: {clean_name}\n"
            f"Description: {skill_description}\n"
            f"File: {skill_file_ref}"
        )
    }

    triggered_flag = [False]
    done = asyncio.Event()

    async def on_pre_tool_use(input_data, invocation):
        tool_args = input_data.get("toolArgs", {})
        if clean_name in str(tool_args):
            triggered_flag[0] = True
            done.set()
            # Deny the read so the CLI does not try to open a non-existent file.
            return {"permissionDecision": "deny", "modifiedArgs": tool_args}
        return {"permissionDecision": "allow", "modifiedArgs": tool_args}

    session_config: dict = {
        "on_permission_request": PermissionHandler.approve_all,
        "system_message": system_message,
        "hooks": {"on_pre_tool_use": on_pre_tool_use},
        "infinite_sessions": {"enabled": False},
    }
    if model:
        session_config["model"] = model

    async with await client.create_session(session_config) as session:
        def on_event(event):
            if event.type.value == "session.idle":
                done.set()

        session.on(on_event)
        await session.send({"prompt": query})
        try:
            await asyncio.wait_for(done.wait(), timeout=timeout)
        except asyncio.TimeoutError:
            pass

    return triggered_flag[0]


def run_single_query(
    query: str,
    skill_name: str,
    skill_description: str,
    timeout: int,
    project_root: str,
    model: str | None = None,
) -> bool:
    """Synchronous wrapper around run_single_query_async for direct use."""
    client = CopilotClient()

    async def _run():
        await client.start()
        try:
            return await run_single_query_async(
                client, query, skill_name, skill_description, timeout, project_root, model
            )
        finally:
            await client.stop()

    return asyncio.run(_run())


async def _run_eval_async(
    eval_set: list[dict],
    skill_name: str,
    description: str,
    num_workers: int,
    timeout: int,
    project_root: Path,
    runs_per_query: int = 1,
    trigger_threshold: float = 0.5,
    model: str | None = None,
) -> dict:
    """Async implementation: runs all queries concurrently via the Copilot SDK."""
    client = CopilotClient()
    await client.start()
    sem = asyncio.Semaphore(num_workers)

    async def bounded_query(item: dict, index: int) -> tuple[dict, bool]:
        # Stagger the first wave so sessions don't all open at once.
        if index < num_workers:
            await asyncio.sleep(index * 0.3)
        async with sem:
            result = await run_single_query_async(
                client, item["query"], skill_name, description,
                timeout, str(project_root), model,
            )
            return item, result

    all_items = [item for item in eval_set for _ in range(runs_per_query)]
    tasks = [bounded_query(item, idx) for idx, item in enumerate(all_items)]

    query_triggers: dict[str, list[bool]] = {}
    query_items: dict[str, dict] = {}

    raw_results = await asyncio.gather(*tasks, return_exceptions=True)
    for raw in raw_results:
        if isinstance(raw, Exception):
            print(f"Warning: query failed: {raw}", file=sys.stderr)
            continue
        item, triggered = raw
        query = item["query"]
        query_items[query] = item
        query_triggers.setdefault(query, []).append(triggered)

    await client.stop()

    results = []
    for query, triggers in query_triggers.items():
        item = query_items[query]
        trigger_rate = sum(triggers) / len(triggers)
        should_trigger = item["should_trigger"]
        if should_trigger:
            did_pass = trigger_rate >= trigger_threshold
        else:
            did_pass = trigger_rate < trigger_threshold
        results.append({
            "query": query,
            "should_trigger": should_trigger,
            "trigger_rate": trigger_rate,
            "triggers": sum(triggers),
            "runs": len(triggers),
            "pass": did_pass,
        })

    passed = sum(1 for r in results if r["pass"])
    total = len(results)
    return {
        "skill_name": skill_name,
        "description": description,
        "results": results,
        "summary": {
            "total": total,
            "passed": passed,
            "failed": total - passed,
        },
    }


def run_eval(
    eval_set: list[dict],
    skill_name: str,
    description: str,
    num_workers: int,
    timeout: int,
    project_root: Path,
    runs_per_query: int = 1,
    trigger_threshold: float = 0.5,
    model: str | None = None,
) -> dict:
    """Run the full eval set and return results."""
    return asyncio.run(_run_eval_async(
        eval_set=eval_set,
        skill_name=skill_name,
        description=description,
        num_workers=num_workers,
        timeout=timeout,
        project_root=project_root,
        runs_per_query=runs_per_query,
        trigger_threshold=trigger_threshold,
        model=model,
    ))


def main():
    parser = argparse.ArgumentParser(description="Run trigger evaluation for a skill description")
    parser.add_argument("--eval-set", required=True, help="Path to eval set JSON file")
    parser.add_argument("--skill-path", required=True, help="Path to skill directory")
    parser.add_argument("--description", default=None, help="Override description to test")
    parser.add_argument("--num-workers", type=int, default=4, help="Number of parallel workers")
    parser.add_argument("--timeout", type=int, default=30, help="Timeout per query in seconds")
    parser.add_argument("--runs-per-query", type=int, default=3, help="Number of runs per query")
    parser.add_argument("--trigger-threshold", type=float, default=0.5, help="Trigger rate threshold")
    parser.add_argument("--model", default=None, help="Model to use via the Copilot CLI (default: user's configured model)")
    parser.add_argument("--verbose", action="store_true", help="Print progress to stderr")
    args = parser.parse_args()

    eval_set = json.loads(Path(args.eval_set).read_text())
    skill_path = Path(args.skill_path)

    if not (skill_path / "SKILL.md").exists():
        print(f"Error: No SKILL.md found at {skill_path}", file=sys.stderr)
        sys.exit(1)

    name, original_description, content = parse_skill_md(skill_path)
    description = args.description or original_description
    project_root = find_project_root()

    if args.verbose:
        print(f"Evaluating: {description}", file=sys.stderr)

    output = run_eval(
        eval_set=eval_set,
        skill_name=name,
        description=description,
        num_workers=args.num_workers,
        timeout=args.timeout,
        project_root=project_root,
        runs_per_query=args.runs_per_query,
        trigger_threshold=args.trigger_threshold,
        model=args.model,
    )

    if args.verbose:
        summary = output["summary"]
        print(f"Results: {summary['passed']}/{summary['total']} passed", file=sys.stderr)
        for r in output["results"]:
            status = "PASS" if r["pass"] else "FAIL"
            rate_str = f"{r['triggers']}/{r['runs']}"
            print(f"  [{status}] rate={rate_str} expected={r['should_trigger']}: {r['query'][:70]}", file=sys.stderr)

    print(json.dumps(output, indent=2))


if __name__ == "__main__":
    main()
