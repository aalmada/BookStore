#!/usr/bin/env python3
"""Grade a run's outputs against eval assertions using the Copilot SDK.

Reads the output files from a completed run directory, then sends them along
with the eval's assertions to the Copilot model and asks it to decide pass/fail
for each assertion with supporting evidence.

Writes grading.json to <run-dir>/grading.json.

Usage:
    python -m scripts.grade --run-dir /tmp/bench/eval-1/with_squad/run-1 \\
        --eval-set evals/squad-evals.json --eval-id 1
"""

from __future__ import annotations

import argparse
import asyncio
import json
import re
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

from copilot import CopilotClient, PermissionHandler

from scripts.utils import load_evals

# Maximum characters of output content to send to the grader per file.
_MAX_FILE_CHARS = 12_000

# Extensions treated as readable text.
_TEXT_EXTENSIONS = {
    ".txt", ".md", ".json", ".yaml", ".yml", ".py", ".ts", ".tsx", ".js",
    ".jsx", ".java", ".kt", ".cs", ".go", ".rs", ".xml", ".html", ".css",
    ".sh", ".toml", ".csv", ".sql",
}


def _read_outputs(run_dir: Path) -> dict[str, str]:
    """Return {filename: content} for all text output files, excluding internals.

    Prefers outputs/task/ when it exists (two-phase with_squad runs),
    falls back to outputs/ for backward compatibility.
    """
    outputs_dir = run_dir / "outputs"
    task_dir = outputs_dir / "task"
    read_dir = task_dir if task_dir.is_dir() else outputs_dir
    # metrics.json is internal bookkeeping; transcript.md IS included so the
    # grader can assess code embedded in the conversation when no separate
    # output files were written.
    skip = {"metrics.json", "tool_calls_debug.jsonl"}
    result: dict[str, str] = {}
    if not read_dir.is_dir():
        return result
    for f in sorted(read_dir.iterdir()):
        if not f.is_file() or f.name in skip:
            continue
        if f.suffix.lower() in _TEXT_EXTENSIONS:
            try:
                text = f.read_text(encoding="utf-8", errors="replace")
                result[f.name] = text[:_MAX_FILE_CHARS]
            except OSError:
                pass
    return result


def _build_grading_prompt(
    prompt: str,
    assertions: list[dict],
    outputs: dict[str, str],
    with_squad: bool,
) -> str:
    """Build the grading prompt sent to the Copilot model."""
    config_label = "with the squad skill active" if with_squad else "without the squad skill (baseline)"
    assertion_lines = "\n".join(
        f'  {i + 1}. {a.get("text", a.get("id", "?"))}' for i, a in enumerate(assertions)
    )
    if outputs:
        output_section = "\n\n".join(
            f"### {name}\n```\n{content}\n```" for name, content in outputs.items()
        )
    else:
        output_section = "(no output files were captured)"

    # Also include transcript if available
    transcript_path = next(
        (p for p in [
            Path(next(iter(outputs)) if outputs else "").parent,
        ] if False),
        None,
    )

    return f"""You are grading the output of an AI agent task.

The task was run **{config_label}**.

## Original Prompt

{prompt}

## Assertions to Grade

{assertion_lines}

## Agent Outputs

{output_section}

---

## Your Task

For each assertion above, decide whether it PASSED or FAILED based solely on the
outputs shown. Be strict but fair. Cite specific evidence from the outputs.

Return a JSON object in this exact format (no markdown fences, raw JSON only):

{{
  "expectations": [
    {{
      "text": "<assertion text verbatim>",
      "passed": true,
      "evidence": "<one sentence citing specific text from the outputs>"
    }}
  ],
  "summary": {{
    "passed": <int>,
    "failed": <int>,
    "total": <int>,
    "pass_rate": <float 0.0-1.0>
  }}
}}

If the outputs are empty or clearly incomplete, mark all assertions as failed and
note "No outputs captured" as evidence.
"""


async def _grade_async(
    eval_item: dict,
    run_dir: Path,
    model: str | None,
) -> dict:
    """Call the Copilot model to grade all assertions; return grading dict."""
    assertions = eval_item.get("assertions", [])
    prompt = eval_item.get("prompt", "")
    outputs = _read_outputs(run_dir)

    # Read eval_metadata to know which config this run used
    metadata_path = run_dir / "eval_metadata.json"
    with_squad = False
    if metadata_path.exists():
        try:
            meta = json.loads(metadata_path.read_text())
            with_squad = meta.get("with_squad", False)
        except (json.JSONDecodeError, OSError):
            pass

    if not assertions:
        # Nothing to grade — write an empty grading.json
        result = {
            "expectations": [],
            "summary": {"passed": 0, "failed": 0, "total": 0, "pass_rate": 1.0},
            "timing": {"grader_duration_seconds": 0.0, "total_duration_seconds": 0.0},
        }
        return result

    grading_prompt = _build_grading_prompt(prompt, assertions, outputs, with_squad)

    session_config: dict = {
        "on_permission_request": PermissionHandler.approve_all,
        "infinite_sessions": {"enabled": False},
    }
    if model:
        session_config["model"] = model

    response_text: list[str] = []
    done = asyncio.Event()

    def on_event(event):
        etype = event.type.value
        if etype == "assistant.message":
            response_text.append(event.data.content)
            done.set()
        elif etype == "session.idle":
            done.set()

    client = CopilotClient()
    await client.start()
    t0 = time.monotonic()
    try:
        async with await client.create_session(**session_config) as session:
            session.on(on_event)
            await session.send(grading_prompt)
            await asyncio.wait_for(done.wait(), timeout=120)
    finally:
        await client.stop()

    elapsed = round(time.monotonic() - t0, 2)

    raw = response_text[0] if response_text else ""

    # Extract JSON — strip optional markdown code fences
    match = re.search(r"\{[\s\S]+\}", raw)
    if not match:
        # Fallback: all assertions failed
        expectations = [
            {"text": a.get("text", str(a.get("id", "?"))), "passed": False,
             "evidence": "Grader returned no parseable JSON."}
            for a in assertions
        ]
        total = len(expectations)
        return {
            "expectations": expectations,
            "summary": {"passed": 0, "failed": total, "total": total, "pass_rate": 0.0},
            "timing": {"grader_duration_seconds": elapsed, "total_duration_seconds": elapsed},
            "grader_model": model or "(default)",
        }

    try:
        grading = json.loads(match.group(0))
    except json.JSONDecodeError:
        expectations = [
            {"text": a.get("text", str(a.get("id", "?"))), "passed": False,
             "evidence": "Grader JSON parse error."}
            for a in assertions
        ]
        total = len(expectations)
        return {
            "expectations": expectations,
            "summary": {"passed": 0, "failed": total, "total": total, "pass_rate": 0.0},
            "timing": {"grader_duration_seconds": elapsed, "total_duration_seconds": elapsed},
            "grader_model": model or "(default)",
        }

    # Ensure summary is consistent
    exps = grading.get("expectations", [])
    passed = sum(1 for e in exps if e.get("passed"))
    total = len(exps)
    grading["summary"] = {
        "passed": passed,
        "failed": total - passed,
        "total": total,
        "pass_rate": round(passed / total, 4) if total > 0 else 1.0,
    }
    grading["timing"] = {"grader_duration_seconds": elapsed, "total_duration_seconds": elapsed}
    grading["grader_model"] = model or "(default)"
    return grading


def grade(
    eval_item: dict,
    run_dir: Path,
    model: str | None = None,
) -> dict:
    """Synchronous entry point. Returns and writes grading dict."""
    grading = asyncio.run(_grade_async(eval_item, run_dir, model))
    (run_dir / "grading.json").write_text(json.dumps(grading, indent=2), encoding="utf-8")
    return grading


def main():
    parser = argparse.ArgumentParser(
        description="Grade a run's outputs against eval assertions"
    )
    parser.add_argument("--run-dir", required=True, help="Run directory containing outputs/")
    parser.add_argument("--eval-set", required=True, help="Path to squad-evals.json")
    parser.add_argument("--eval-id", type=int, required=True, help="Eval ID from squad-evals.json")
    parser.add_argument("--model", default=None, help="Model to use for grading (alias for --grader-model)")
    parser.add_argument("--grader-model", default=None, dest="grader_model",
                        help="Model to use for grading (overrides --model)")
    parser.add_argument("--verbose", action="store_true", help="Print progress to stderr")
    args = parser.parse_args()

    evals = load_evals(Path(args.eval_set))
    matching = [e for e in evals if e.get("id") == args.eval_id]
    if not matching:
        print(f"Error: eval id {args.eval_id} not found", file=sys.stderr)
        sys.exit(1)
    eval_item = matching[0]

    run_dir = Path(args.run_dir)
    if not run_dir.is_dir():
        print(f"Error: run directory not found: {run_dir}", file=sys.stderr)
        sys.exit(1)

    if args.verbose:
        print(f"Grading {run_dir} …", file=sys.stderr)

    effective_model = args.grader_model or args.model
    grading = grade(eval_item=eval_item, run_dir=run_dir, model=effective_model)
    summary = grading.get("summary", {})

    if args.verbose:
        print(
            f"Grading done: {summary.get('passed')}/{summary.get('total')} passed "
            f"(pass_rate={summary.get('pass_rate', 0):.0%})",
            file=sys.stderr,
        )

    print(json.dumps(grading, indent=2))


if __name__ == "__main__":
    main()
