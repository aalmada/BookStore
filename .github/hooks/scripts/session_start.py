#!/usr/bin/env python3
"""
SessionStart hook: Inject BookStore project context at the start of every session.

Emits a concise context block with the current git branch, .NET SDK version,
a summary of AGENTS.md code rules, and the memory handoff file map so every
agent begins with full orientation.

Input (stdin): VS Code SessionStart JSON payload
Output (stdout): JSON with hookSpecificOutput.additionalContext
"""

import json
import subprocess
import sys


MEMORY_MAP = """\
Agent memory handoff files (session-scoped — write with vscode/memory):
  /memories/session/task-brief.md    ← Orchestrator writes task scope + routing
  /memories/session/plan.md          ← Planner writes implementation plan
  /memories/session/backend-output.md  ← BackendDeveloper writes implementation notes
  /memories/session/frontend-output.md ← FrontendDeveloper writes implementation notes
  /memories/session/test-output.md   ← TestEngineer writes test coverage notes
  /memories/session/review.md        ← CodeReviewer writes findings"""


def run(cmd: list[str]) -> str:
    try:
        return subprocess.check_output(cmd, stderr=subprocess.DEVNULL, timeout=5).decode().strip()
    except Exception:
        return "unknown"


def main() -> None:
    try:
        json.load(sys.stdin)  # consume stdin
    except Exception:
        pass

    branch = run(["git", "branch", "--show-current"])
    sdk = run(["dotnet", "--version"])

    context = f"""\
BookStore Project — Session Context
====================================
Git branch : {branch}
.NET SDK   : {sdk}
Solution   : BookStore.slnx

Code rules (MUST follow — violations are blocked by hooks):
  ✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
  ✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
  ✅ record EventPastTense(...)     ❌ record VerbCommand(...)  (events are past-tense)
  ✅ namespace BookStore.X;         ❌ namespace BookStore.X {{ }}
  ✅ [LoggerMessage(...)]           ❌ _logger.LogInformation/Warning/Error()
  ✅ MultiTenancyConstants.*        ❌ hardcoded "*DEFAULT*" / "default"
  ✅ Result<T> + ProblemDetails     ❌ throw for validation errors
  ✅ IBookStoreClient (Refit)       ❌ HttpClient called directly

Security rules (violations are blocked by hooks):
  • No hardcoded passwords, API keys, or secrets in source files
  • No string-interpolated SQL queries — use Marten API
  • [AllowAnonymous] requires a '// safe: <reason>' comment above it
  • MarkupString in .razor requires a '// safe: <reason>' comment above it

Test rules (TUnit only):
  ✅ [Test] async Task              ❌ [Fact] / [TestMethod]
  ✅ await Assert.That(...)         ❌ FluentAssertions / Assert.Equal
  ✅ WaitForConditionAsync          ❌ Task.Delay / Thread.Sleep
  ✅ Bogus for test data            ❌ Hand-rolled random data
  ✅ NSubstitute for mocking        ❌ Moq

Run tests : dotnet test -- --maximum-parallel-tests 4
Format    : dotnet format
Check fmt : dotnet format --verify-no-changes

{MEMORY_MAP}

Read AGENTS.md and the relevant docs/guides/ before starting work.
"""

    output = {
        "hookSpecificOutput": {
            "hookEventName": "SessionStart",
            "additionalContext": context,
        }
    }
    print(json.dumps(output))


if __name__ == "__main__":
    main()
