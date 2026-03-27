#!/usr/bin/env python3
"""Run the full with_squad / without_squad benchmark for copilot-squad.

For each eval in squad-evals.json this script:
  1. Runs the eval prompt with the squad skill injected  (with_squad)
  2. Runs the eval prompt without any skill              (without_squad)
  3. Grades both runs against the eval's assertions
  4. Aggregates all results into benchmark.json

Directory layout produced:
    <output-dir>/
    ├── eval-1/
    │   ├── without_squad/
    │   │   └── run-1/
    │   │       ├── outputs/          ← captured output files
    │   │       ├── eval_metadata.json
    │   │       ├── timing.json
    │   │       └── grading.json      ← written by grade step
    │   ├── with_squad-Orchestrator/  ← one dir per orchestrator
    │   │   └── run-1/
    │   │       └── ... (same structure)
    │   └── with_squad-OtherOrchestrator/
    │       └── run-1/ ...
    ├── eval-2/ ...
    └── benchmark.json                ← aggregated results

Usage:
    python -m scripts.run_benchmark \\
        --eval-set evals/squad-evals.json \
        --skill-path . \\
        --output-dir benchmarks/2026-03-25T00-00-00 \\
        --orchestrators path/to/Orchestrator.agent.md [path/to/Other.agent.md ...] \\
        [--eval-ids 1,2,3] \\
        [--runs-per-config 1] \\
        [--inactivity-timeout 60] \\
        [--model gpt-4.1] \\
        [--verbose]

    # Reuse existing runs (skip exec, only re-grade and re-aggregate):
    python -m scripts.run_benchmark \\
        --eval-set evals/squad-evals.json \
        --skill-path . \\
        --output-dir benchmarks/2026-03-25T00-00-00 \\
        --orchestrators path/to/Orchestrator.agent.md \\
        --grade-only
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

from scripts.aggregate_benchmark import generate_benchmark, print_summary_table
from scripts.grade import grade
from scripts.run_exec import run_exec
from scripts.utils import find_project_root, load_evals, parse_skill_md


def _viewer_script_path() -> Path:
    """Return the path to the squad's eval-viewer/generate_review.py."""
    return Path(__file__).parent.parent / "eval-viewer" / "generate_review.py"


def launch_viewer(
    output_dir: Path,
    benchmark_path: Path,
    previous_dir: Path | None = None,
    port: int = 3117,
) -> int | None:
    """Launch the eval viewer in the background.

    Returns the PID of the spawned process, or None if the viewer script is
    not found.

    Headless detection: if DISPLAY is unset (common in CI / remote shells)
    the viewer is written as a standalone HTML file instead of starting a
    server.  The path to the HTML file is printed to stdout.
    """
    viewer = _viewer_script_path()
    if not viewer.exists():
        print(
            f"Warning: viewer not found at {viewer} — skipping auto-launch.",
            file=sys.stderr,
        )
        return None

    cmd = [
        sys.executable, str(viewer),
        str(output_dir),
        "--skill-name", "copilot-squad",
        "--benchmark", str(benchmark_path),
    ]
    if previous_dir is not None:
        cmd += ["--previous-workspace", str(previous_dir)]

    # Headless: no DISPLAY on Linux → write static HTML instead of serving
    headless = sys.platform != "win32" and not os.environ.get("DISPLAY", "").strip()
    if headless:
        static_path = output_dir / "review.html"
        cmd += ["--static", str(static_path)]
        subprocess.run(cmd, check=False)
        print(f"\n  Results page written to: {static_path}\n")
        return None

    # Interactive: launch the server in the background
    proc = subprocess.Popen(
        cmd,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    print(f"\n  Eval Viewer  →  http://localhost:{port}")
    print(f"  (PID {proc.pid} — kill with: kill {proc.pid})\n")
    return proc.pid

def _run_one(
    eval_item: dict,
    skill_path: Path,
    run_dir: Path,
    config_label: str,
    orchestrator_agent: Path | None,
    inactivity_timeout: int,
    model: str | None,
    grader_model: str | None,
    verbose: bool,
    use_worktree: bool = False,
) -> None:
    """Execute one eval run and grade it. Creates run_dir if needed."""
    with_squad = orchestrator_agent is not None
    print(
        f"\n{'='*60}\n"
        f"  eval-{eval_item['id']}  [{config_label}]  run={run_dir.name}\n"
        f"{'='*60}",
        flush=True,
    )
    if verbose:
        print(
            f"  → exec  eval-{eval_item['id']} [{config_label}] …",
            file=sys.stderr,
        )
    run_exec(
        eval_item=eval_item,
        skill_path=skill_path,
        output_dir=run_dir,
        with_squad=with_squad,
        inactivity_timeout=inactivity_timeout,
        model=model,
        orchestrator_agent=orchestrator_agent,
        use_worktree=use_worktree,
    )
    if verbose:
        print(
            f"  → grade eval-{eval_item['id']} [{config_label}] …",
            file=sys.stderr,
        )
    grading = grade(eval_item=eval_item, run_dir=run_dir, model=grader_model)
    summary = grading.get("summary", {})
    print(
        f"  ✓ graded  pass_rate={summary.get('pass_rate', 0):.0%} "
        f"({summary.get('passed')}/{summary.get('total')})",
        flush=True,
    )
    if verbose:
        print(
            f"       pass_rate={summary.get('pass_rate', 0):.0%} "
            f"({summary.get('passed')}/{summary.get('total')})",
            file=sys.stderr,
        )


def run_benchmark(
    evals: list[dict],
    skill_path: Path,
    output_dir: Path,
    orchestrator_agents: list[Path],
    runs_per_config: int = 1,
    inactivity_timeout: int = 60,
    model: str | None = None,
    grader_model: str | None = None,
    eval_ids: list[int] | None = None,
    grade_only: bool = False,
    verbose: bool = False,
    no_viewer: bool = False,
    previous_dir: Path | None = None,
    use_worktree: bool = False,
) -> dict:
    """Run the full benchmark. Returns the benchmark dict."""
    output_dir.mkdir(parents=True, exist_ok=True)

    if eval_ids:
        evals = [e for e in evals if e.get("id") in eval_ids]
    if not evals:
        print("Error: no evals to run (check --eval-ids)", file=sys.stderr)
        sys.exit(1)

    configs = [("without_squad", None)] + [
        (f"with_squad-{o.stem}", o) for o in orchestrator_agents
    ]

    if verbose:
        print(
            f"\nBenchmark: {len(evals)} eval(s) × {len(configs)} configs × "
            f"{runs_per_config} run(s) = "
            f"{len(evals) * len(configs) * runs_per_config} total runs\n",
            file=sys.stderr,
        )

    for eval_item in evals:
        eid = eval_item.get("id", 0)
        eval_dir = output_dir / f"eval-{eid}"
        eval_dir.mkdir(parents=True, exist_ok=True)

        # Write eval_metadata once at the eval level for the viewer
        meta_path = eval_dir / "eval_metadata.json"
        if not meta_path.exists():
            meta_path.write_text(
                json.dumps({
                    "eval_id": eid,
                    "eval_name": eval_item.get("eval_name", f"eval-{eid}"),
                    "prompt": eval_item.get("prompt", ""),
                    "assertions": eval_item.get("assertions", []),
                }, indent=2),
                encoding="utf-8",
            )

        for config_label, orchestrator in configs:
            config_dir = eval_dir / config_label
            config_dir.mkdir(parents=True, exist_ok=True)

            for run_n in range(1, runs_per_config + 1):
                run_dir = config_dir / f"run-{run_n}"

                if grade_only:
                    # Only (re-)grade; skip exec if run dir is missing
                    if not run_dir.is_dir():
                        if verbose:
                            print(
                                f"  [skip] {run_dir} does not exist, skipping",
                                file=sys.stderr,
                            )
                        continue
                    if verbose:
                        print(
                            f"  → grade eval-{eid} [{config_label}] run-{run_n} …",
                            file=sys.stderr,
                        )
                    grade(eval_item=eval_item, run_dir=run_dir, model=grader_model or model)
                else:
                    _run_one(
                        eval_item=eval_item,
                        skill_path=skill_path,
                        run_dir=run_dir,
                        config_label=config_label,
                        orchestrator_agent=orchestrator,
                        inactivity_timeout=inactivity_timeout,
                        model=model,
                        grader_model=grader_model or model,
                        verbose=verbose,
                        use_worktree=use_worktree,
                    )

    if verbose:
        print("\nAggregating results …", file=sys.stderr)

    name, _, _ = parse_skill_md(skill_path)
    benchmark = generate_benchmark(
        benchmark_dir=output_dir,
        skill_name=name,
        skill_path=str(skill_path.resolve()),
    )
    benchmark_path = output_dir / "benchmark.json"
    benchmark_path.write_text(json.dumps(benchmark, indent=2), encoding="utf-8")

    if verbose:
        print(f"Wrote {benchmark_path}", file=sys.stderr)

    print_summary_table(benchmark)

    if not no_viewer:
        launch_viewer(
            output_dir=output_dir,
            benchmark_path=benchmark_path,
            previous_dir=previous_dir,
        )

    return benchmark


def main():
    parser = argparse.ArgumentParser(
        description="Run the with_squad / without_squad benchmark for copilot-squad"
    )
    parser.add_argument("--eval-set", required=True, help="Path to squad-evals.json")
    parser.add_argument("--skill-path", required=True, help="Path to skill directory (contains SKILL.md)")
    parser.add_argument(
        "--output-dir",
        default=None,
        help="Output directory (default: benchmarks/<ISO-timestamp>)",
    )
    parser.add_argument(
        "--eval-ids",
        default=None,
        help="Comma-separated eval IDs to run (default: all)",
    )
    parser.add_argument(
        "--runs-per-config",
        type=int,
        default=1,
        help="Number of runs per configuration per eval (default: 1)",
    )
    parser.add_argument(
        "--inactivity-timeout",
        type=int,
        default=60,
        help="Session ends after this many seconds with no tool call or model event (default: 60)",
    )
    parser.add_argument("--model", default=None,
                        help="Model for the exec session (default: user's configured model)")
    parser.add_argument("--grader-model", default=None, dest="grader_model",
                        help="Model used by the grader (defaults to --model if omitted)")
    parser.add_argument(
        "--orchestrators",
        nargs="+",
        required=True,
        metavar="AGENT_FILE",
        help="One or more .agent.md files used for with_squad runs (one config per orchestrator).",
    )
    parser.add_argument(
        "--grade-only",
        action="store_true",
        help="Skip exec; only (re-)grade existing run directories",
    )
    parser.add_argument(
        "--no-viewer",
        action="store_true",
        help="Do not auto-launch the eval viewer after the benchmark completes (useful in CI)",
    )
    parser.add_argument(
        "--previous-workspace",
        default=None,
        metavar="DIR",
        help="Previous benchmark output directory; shown as context in the viewer",
    )
    parser.add_argument("--verbose", action="store_true", help="Print progress to stderr")
    args = parser.parse_args()

    skill_path = Path(args.skill_path)
    if not (skill_path / "SKILL.md").exists():
        print(f"Error: No SKILL.md found at {skill_path}", file=sys.stderr)
        sys.exit(1)

    evals = load_evals(Path(args.eval_set))

    if args.output_dir:
        output_dir = Path(args.output_dir)
    else:
        ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H-%M-%S")
        output_dir = Path("benchmarks") / ts

    eval_ids = None
    if args.eval_ids:
        try:
            eval_ids = [int(x.strip()) for x in args.eval_ids.split(",")]
        except ValueError:
            print("Error: --eval-ids must be comma-separated integers", file=sys.stderr)
            sys.exit(1)

    orchestrator_agents: list[Path] = []
    for raw in args.orchestrators:
        p = Path(raw)
        if not p.exists():
            print(f"Error: orchestrator file not found: {p}", file=sys.stderr)
            sys.exit(1)
        orchestrator_agents.append(p)

    previous_dir = Path(args.previous_workspace) if args.previous_workspace else None

    run_benchmark(
        evals=evals,
        skill_path=skill_path,
        output_dir=output_dir,
        orchestrator_agents=orchestrator_agents,
        runs_per_config=args.runs_per_config,
        inactivity_timeout=args.inactivity_timeout,
        model=args.model,
        grader_model=args.grader_model,
        eval_ids=eval_ids,
        grade_only=args.grade_only,
        verbose=args.verbose,
        no_viewer=args.no_viewer,
        previous_dir=previous_dir,
        use_worktree=True,
    )


if __name__ == "__main__":
    main()
