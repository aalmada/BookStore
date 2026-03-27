#!/usr/bin/env python3
"""Aggregate benchmark results into summary statistics and benchmark.json.

Reads grading.json files from a benchmark directory and produces:
- benchmark.json  — full structured results with mean, stddev, min, max per config
- Printed summary table comparing with_squad vs without_squad

Directory layout expected:
    <benchmark_dir>/
    └── eval-N/
        ├── with_squad/
        │   ├── run-1/grading.json
        │   └── run-2/grading.json
        └── without_squad/
            ├── run-1/grading.json
            └── run-2/grading.json

Usage:
    python -m scripts.aggregate_benchmark <benchmark_dir> [--skill-name copilot-squad]
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path


def calculate_stats(values: list[float]) -> dict:
    """Calculate mean, stddev, min, max for a list of values."""
    if not values:
        return {"mean": 0.0, "stddev": 0.0, "min": 0.0, "max": 0.0}
    n = len(values)
    mean = sum(values) / n
    stddev = math.sqrt(sum((x - mean) ** 2 for x in values) / (n - 1)) if n > 1 else 0.0
    return {
        "mean": round(mean, 4),
        "stddev": round(stddev, 4),
        "min": round(min(values), 4),
        "max": round(max(values), 4),
    }


def load_run_results(benchmark_dir: Path) -> dict[str, list[dict]]:
    """Load all grading.json files; return {config_name: [run_result, ...]}."""
    # Support runs/ subdirectory layout (legacy) or direct eval dirs
    runs_dir = benchmark_dir / "runs"
    search_dir = runs_dir if runs_dir.exists() else benchmark_dir

    results: dict[str, list[dict]] = {}

    for eval_idx, eval_dir in enumerate(sorted(search_dir.glob("eval-*"))):
        # Determine eval_id
        metadata_path = eval_dir / "eval_metadata.json"
        if metadata_path.exists():
            try:
                eval_id = json.loads(metadata_path.read_text()).get("eval_id", eval_idx)
            except (json.JSONDecodeError, OSError):
                eval_id = eval_idx
        else:
            try:
                eval_id = int(eval_dir.name.split("-")[1])
            except (IndexError, ValueError):
                eval_id = eval_idx

        for config_dir in sorted(eval_dir.iterdir()):
            if not config_dir.is_dir():
                continue
            if not list(config_dir.glob("run-*")):
                continue
            config = config_dir.name
            if config not in results:
                results[config] = []

            for run_dir in sorted(config_dir.glob("run-*")):
                try:
                    run_number = int(run_dir.name.split("-")[1])
                except (IndexError, ValueError):
                    run_number = 0

                grading_file = run_dir / "grading.json"
                if not grading_file.exists():
                    print(f"Warning: grading.json not found in {run_dir}", file=sys.stderr)
                    continue

                try:
                    grading = json.loads(grading_file.read_text())
                except json.JSONDecodeError as exc:
                    print(f"Warning: invalid JSON in {grading_file}: {exc}", file=sys.stderr)
                    continue

                summary = grading.get("summary", {})
                timing = grading.get("timing", {})
                time_seconds = timing.get("total_duration_seconds", 0.0)

                # Fallback to timing.json sibling
                if time_seconds == 0.0:
                    timing_file = run_dir / "timing.json"
                    if timing_file.exists():
                        try:
                            td = json.loads(timing_file.read_text())
                            time_seconds = td.get("total_duration_seconds", 0.0)
                        except (json.JSONDecodeError, OSError):
                            pass

                metrics = grading.get("execution_metrics", {})
                # run_exec.py writes token counts to outputs/metrics.json, not grading.json.
                # Read from that file first; fall back to what grading.json may carry.
                metrics_file = run_dir / "outputs" / "metrics.json"
                if metrics_file.exists():
                    try:
                        file_metrics = json.loads(metrics_file.read_text())
                        if not metrics:
                            metrics = file_metrics
                        else:
                            # Merge: prefer non-zero values from outputs/metrics.json
                            for key in ("input_tokens", "output_tokens", "total_tool_calls",
                                        "errors_encountered", "output_chars"):
                                if metrics.get(key, 0) == 0 and file_metrics.get(key, 0) != 0:
                                    metrics[key] = file_metrics[key]
                    except (json.JSONDecodeError, OSError):
                        pass
                # Prefer real LLM token counts; fall back to output_chars for
                # runs captured before token tracking was added.
                real_tokens = metrics.get("input_tokens", 0) + metrics.get("output_tokens", 0)
                token_count = real_tokens if real_tokens > 0 else metrics.get("output_chars", 0)
                result: dict = {
                    "eval_id": eval_id,
                    "run_number": run_number,
                    "pass_rate": summary.get("pass_rate", 0.0),
                    "passed": summary.get("passed", 0),
                    "failed": summary.get("failed", 0),
                    "total": summary.get("total", 0),
                    "time_seconds": time_seconds,
                    "tool_calls": metrics.get("total_tool_calls", 0),
                    "tokens": token_count,
                    "input_tokens": metrics.get("input_tokens", 0),
                    "output_tokens": metrics.get("output_tokens", 0),
                    "errors": metrics.get("errors_encountered", 0),
                    "expectations": grading.get("expectations", []),
                    "notes": (
                        grading.get("user_notes_summary", {}).get("uncertainties", [])
                        + grading.get("user_notes_summary", {}).get("needs_review", [])
                        + grading.get("user_notes_summary", {}).get("workarounds", [])
                    ),
                    "grader_model": grading.get("grader_model", "(default)"),
                }

                # Read exec model from run-level eval_metadata.json
                run_meta_path = run_dir / "eval_metadata.json"
                if run_meta_path.exists():
                    try:
                        run_meta = json.loads(run_meta_path.read_text())
                        result["exec_model"] = run_meta.get("exec_model", "(default)")
                    except (json.JSONDecodeError, OSError):
                        result["exec_model"] = "(default)"
                else:
                    result["exec_model"] = "(default)"
                results[config].append(result)

    return results


def _most_common(values: list[str]) -> str:
    """Return the most frequently occurring value in *values*, or '(default)'."""
    if not values:
        return "(default)"
    return max(set(values), key=values.count)


def aggregate_results(results: dict[str, list[dict]]) -> dict:
    """Return run_summary with stats per config and a delta."""
    run_summary: dict = {}
    configs = list(results.keys())

    for config in configs:
        runs = results[config]
        if not runs:
            run_summary[config] = {
                "pass_rate": {"mean": 0.0, "stddev": 0.0, "min": 0.0, "max": 0.0},
                "time_seconds": {"mean": 0.0, "stddev": 0.0, "min": 0.0, "max": 0.0},
                "tokens": {"mean": 0.0, "stddev": 0.0, "min": 0.0, "max": 0.0},
            }
            continue

        run_summary[config] = {
            "pass_rate": calculate_stats([r["pass_rate"] for r in runs]),
            "time_seconds": calculate_stats([r["time_seconds"] for r in runs]),
            "tokens": calculate_stats([float(r.get("tokens", 0)) for r in runs]),
            "input_tokens": calculate_stats([float(r.get("input_tokens", 0)) for r in runs]),
            "output_tokens": calculate_stats([float(r.get("output_tokens", 0)) for r in runs]),
            "exec_model": _most_common([r.get("exec_model", "(default)") for r in runs]),
            "grader_model": _most_common([r.get("grader_model", "(default)") for r in runs]),
        }

    # Delta: first with_squad-* config vs without_squad (when both present)
    ordered = sorted(
        configs,
        key=lambda c: (0 if c.startswith("with_squad") else 1),
    )
    if len(ordered) >= 2:
        primary = run_summary.get(ordered[0], {})
        baseline = run_summary.get(ordered[1], {})
        delta_pass = primary.get("pass_rate", {}).get("mean", 0) - baseline.get("pass_rate", {}).get("mean", 0)
        delta_time = primary.get("time_seconds", {}).get("mean", 0) - baseline.get("time_seconds", {}).get("mean", 0)
        delta_tok = primary.get("tokens", {}).get("mean", 0) - baseline.get("tokens", {}).get("mean", 0)
        delta_in = primary.get("input_tokens", {}).get("mean", 0) - baseline.get("input_tokens", {}).get("mean", 0)
        delta_out = primary.get("output_tokens", {}).get("mean", 0) - baseline.get("output_tokens", {}).get("mean", 0)
        run_summary["delta"] = {
            "configurations": f"{ordered[0]} vs {ordered[1]}",
            "pass_rate": f"{delta_pass:+.4f}",
            "time_seconds": f"{delta_time:+.1f}",
            "tokens": f"{delta_tok:+.0f}",
            "input_tokens": f"{delta_in:+.0f}",
            "output_tokens": f"{delta_out:+.0f}",
        }

    return run_summary


def generate_benchmark(benchmark_dir: Path, skill_name: str = "", skill_path: str = "") -> dict:
    """Generate a complete benchmark.json from run results."""
    results = load_run_results(benchmark_dir)
    run_summary = aggregate_results(results)

    runs_array = []
    for config, run_list in results.items():
        for r in run_list:
            runs_array.append({
                "eval_id": r["eval_id"],
                "configuration": config,
                "run_number": r["run_number"],
                "result": {
                    "pass_rate": r["pass_rate"],
                    "passed": r["passed"],
                    "failed": r["failed"],
                    "total": r["total"],
                    "time_seconds": r["time_seconds"],
                    "tokens": r.get("tokens", 0),
                    "input_tokens": r.get("input_tokens", 0),
                    "output_tokens": r.get("output_tokens", 0),
                    "tool_calls": r.get("tool_calls", 0),
                    "errors": r.get("errors", 0),
                    "exec_model": r.get("exec_model", "(default)"),
                    "grader_model": r.get("grader_model", "(default)"),
                },
                "expectations": r.get("expectations", []),
                "notes": r.get("notes", []),
            })

    # Determine runs_per_configuration: max run count across any config × eval combination
    runs_per_config = 1
    if results:
        eval_ids_per_config: dict[str, set] = {}
        for config, run_list in results.items():
            for r in run_list:
                eval_ids_per_config.setdefault(config, set()).add(r["eval_id"])
        # number of runs per (config, eval) = total runs / number of unique evals
        for config, run_list in results.items():
            n_evals = len(eval_ids_per_config.get(config, {1}))
            if n_evals > 0:
                runs_per_config = max(runs_per_config, len(run_list) // n_evals)

    benchmark = {
        "metadata": {
            "skill_name": skill_name or "copilot-squad",
            "skill_path": skill_path,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "configurations": list(results.keys()),
            "runs_per_configuration": runs_per_config,
        },
        "run_summary": run_summary,
        "runs": runs_array,
    }
    return benchmark


def print_summary_table(benchmark: dict) -> None:
    """Print a human-readable summary table to stdout."""
    run_summary = benchmark.get("run_summary", {})
    delta = run_summary.get("delta", {})
    configs = [k for k in run_summary if k != "delta"]

    print("\n┌─────────────────────────────────────────────────────────────────┐")
    print(f"│  Benchmark: {benchmark['metadata'].get('skill_name', ''):<50}│")
    print(f"│  {benchmark['metadata'].get('timestamp', ''):<63}│")
    print("├──────────────────────┬────────────────┬──────────┬─────────────────────┐")
    print("│ Config               │ Pass rate (μ)  │ Time (μ) │ Exec model          │")
    print("├──────────────────────┼────────────────┼──────────┼─────────────────────┤")

    for config in configs:
        stats = run_summary[config]
        pr = stats["pass_rate"]["mean"]
        pr_sd = stats["pass_rate"]["stddev"]
        tm = stats["time_seconds"]["mean"]
        exec_m = stats.get("exec_model", "(default)")[:19]
        print(f"│ {config[:20]:<20} │ {pr:.2%} ±{pr_sd:.2%}  │ {tm:7.1f}s │ {exec_m:<19} │")
        grader_m = stats.get("grader_model", "(default)")[:19]
        print(f"│ {'  grader:':<20} │ {'':14} │ {'':8} │ {grader_m:<19} │")

    if delta:
        print("├──────────────────────┼────────────────┼──────────┼─────────────────────┤")
        print(f"│ {'delta':<20} │ {delta.get('pass_rate', 'N/A'):<14} │ {delta.get('time_seconds', 'N/A'):<8} │ {'':19} │")

    print("└──────────────────────┴────────────────┴──────────┴─────────────────────┘\n")


def main():
    parser = argparse.ArgumentParser(
        description="Aggregate benchmark results from eval run directories"
    )
    parser.add_argument("benchmark_dir", help="Directory containing eval-N/ subdirectories")
    parser.add_argument("--skill-name", default="copilot-squad", help="Skill name for metadata")
    parser.add_argument("--skill-path", default="", help="Skill path for metadata")
    parser.add_argument("--output", default=None, help="Output file path (default: <benchmark_dir>/benchmark.json)")
    args = parser.parse_args()

    benchmark_dir = Path(args.benchmark_dir)
    if not benchmark_dir.is_dir():
        print(f"Error: directory not found: {benchmark_dir}", file=sys.stderr)
        sys.exit(1)

    benchmark = generate_benchmark(benchmark_dir, args.skill_name, args.skill_path)

    output_path = Path(args.output) if args.output else benchmark_dir / "benchmark.json"
    output_path.write_text(json.dumps(benchmark, indent=2), encoding="utf-8")
    print(f"Wrote {output_path}", file=sys.stderr)

    print_summary_table(benchmark)


if __name__ == "__main__":
    main()
