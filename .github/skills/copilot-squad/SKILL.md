---
name: copilot-squad
description: >
  Use this skill when a user wants to create or expand a set of AI agents for their project
  — any time the request involves two or more agents, a "team" or "squad" of agents, or
  agents with distinct roles (like backend, frontend, reviewer, tester) that coordinate on
  shared work. Triggers for: "I want agents for my project", "set up a squad of agents",
  "create agents that work together", "add a new specialist agent to my team", or any
  request where multiple agent roles are implied even if the word "team" isn't used. The key
  signal is plurality — more than one agent, or adding to an existing multi-agent setup.
  Produces ready-to-use `.agent.md` files. Does NOT trigger for: configuring or fixing a
  single existing agent, editing AGENTS.md, setting up MCP servers, or Copilot settings
  questions.
---

# Squad Creator Skill

This skill scaffolds complete multi-agent squads — sets of cooperating `.agent.md` files
where every role is clearly defined and agents hand off context through `vscode/memory`.

When this skill applies, **load it immediately** and follow the workflow below. Always
produce working files, not just descriptions.

Refer to supplementary references when needed:

| File | When to read |
|---|---|
| `references/agent-templates.md` | Templates for each standard agent role |
| `references/memory-conventions.md` | Memory file layout and status-log format |
| `references/model-selection.md` | Role-to-model mapping for squad roles |
| `copilot-custom-agent/references/frontmatter.md` | All frontmatter properties: `user-invocable`, `disable-model-invocation`, `agents`, model fallback chains |
| `copilot-custom-agent/references/patterns.md` | Orchestrator, memory handoff, parallel review, subagent isolation patterns |
| `copilot-custom-agent/references/model-selection.md` | Full model catalog: profiles, multipliers, fallback chains, cost tips |

Also read the **copilot-custom-agent SKILL.md** (`.github/skills/copilot-custom-agent/SKILL.md`)
before writing any `.agent.md` files — it governs valid frontmatter, tool names, and file
locations.

---

## What a Squad Is

A squad is a set of `.agent.md` files that cooperate on a shared task:

- **Orchestrator** — The single user-facing entry point. Coordinates the team. Never
  writes code, never makes implementation decisions. Invokes all other agents.
- **Planner** — Always second. Researches the codebase or domain and writes a concrete
  plan to memory. Never writes production code.
- **Specialists** — Do the actual work (implement, test, review, design, …). Each has a
  narrowly scoped role, reads the plan from memory, and appends a status update when done.

All agents share context through `vscode/memory`. A shared status log lets the Orchestrator
(and the user) track progress without querying each agent.

---

## Workflow

### Step 1 — Capture Intent

Start by understanding what the squad needs to accomplish. If the user didn't already say:

1. **What is the project / domain?** (programming language, framework, existing structure?)
2. **What task type will the squad handle?** (feature implementation, code review, data
   processing, documentation, migrations, …)
3. **Which specialists are needed?** The user may name them, or you can derive them by
   exploring the project (see Step 2).
4. **Output location?** Default: `.github/agents/` in the workspace root.

Use `vscode/askQuestions` to gather missing answers in one round. Don't ask questions
you can answer by looking at the project yourself.

### Step 2 — Analyse the Project (when specialists are not specified)

If the user did not specify which specialists are needed, explore the project to infer them:

- Read `AGENTS.md` / `README.md` / `docs/` for architecture overview
- List `src/` or equivalent source directories
- Check for existing agent files in `.github/agents/` to avoid duplication
- Identify the major concerns: backend, frontend, testing, infrastructure, data, UX, etc.

Derive a minimal specialist set — prefer fewer, broader agents over many narrow ones.
A squad of 4–6 agents is usually right. More than 8 becomes unwieldy.

**Common specialist patterns by domain:**

| Project type | Likely specialists |
|---|---|
| Web app (full-stack) | BackendDeveloper, FrontendDeveloper, TestEngineer, CodeReviewer |
| API service | ApiDeveloper, DatabaseEngineer, TestEngineer, CodeReviewer |
| CLI / tooling | Developer, TestEngineer, DocumentationWriter |
| Data pipeline | DataEngineer, QualityChecker, DocumentationWriter |
| Infrastructure | InfraEngineer, SecurityReviewer, DocumentationWriter |
| Multi-service | ServiceDeveloper, IntegrationEngineer, TestEngineer, CodeReviewer |

### Step 3 — Design the Squad

Produce a squad design before writing any files. Present it to the user and ask for
confirmation (or use `vscode/askQuestions` if there are open choices):

```
## Squad: <Name>
### Agents
1. **Orchestrator** — User-facing entry point. Coordinates the full workflow.
2. **Planner** — Researches the codebase and writes an implementation plan to memory.
3. **<SpecialistA>** — <one sentence: what it does and when it runs>
4. **<SpecialistB>** — …

### Workflow Sequence
① Plan (always serial — every specialist depends on the plan)
② <SpecialistA>  ┐ parallel (if independent of each other)
② <SpecialistB>  ┘
③ <SpecialistC>  (serial — depends on ② output)
④ CodeReviewer   (serial — always last before handoff)

### Memory Files
- /memories/session/task-brief.md    ← Orchestrator writes
- /memories/session/plan.md          ← Planner writes
- /memories/session/status.md        ← All agents append status
- /memories/session/<agent>-output.md ← Each specialist writes
```

Get confirmation before proceeding to file creation.

### Step 4 — Create the Agent Files

Use the **copilot-custom-agent skill** as your guide for valid frontmatter and tool names.
Read **`references/model-selection.md`** to choose the right `model:` value for each role.

Create files in this order:
1. Orchestrator (must reference the full agent list and workflow)
2. Planner
3. Specialists (alphabetical order)

**For every agent, ensure:**

- `tools` includes `vscode/memory` (all agents read/write memory)
- `tools` includes `vscode/askQuestions` for agents that may need user input
- Orchestrator has `agent` in `tools` and an `agents` list covering all specialists
- Specialists have `user-invocable: false`
- Planner has `user-invocable: false` and no `edit` tool
- Each agent body includes the **status-append protocol** (see below)

See `references/agent-templates.md` for copy-paste starting templates for each role.

### Step 5 — Set Up the Status Memory Convention

The status log (`/memories/session/status.md`) is the squad's shared progress tracker.
Every agent appends to it when it starts and when it finishes. The Orchestrator reads it
to report progress to the user.

Embed this protocol in every agent's body:

```markdown
## Status Protocol
When you **start** work, append to `/memories/session/status.md` via `vscode/memory`:
`⏳ <AgentName> — started — <brief task description> — <timestamp>`

When you **finish** work, append:
`✅ <AgentName> — done — <one sentence summary of what was produced>`

If you **encounter a blocker**, append:
`🚫 <AgentName> — blocked — <reason>`
Then stop and surface the blocker to the Orchestrator.
```

### Step 6 — Validate

After creating all files, check each one:

- [ ] Orchestrator workflow matches the agreed squad design
- [ ] Every agent listed in Orchestrator's `agents:` has a corresponding `.agent.md` file
- [ ] All `tools` values are real tool names (check `copilot-custom-agent/references/tools.md`)
- [ ] Memory file paths are consistent across all agents (same path for the same file)
- [ ] Status protocol is present in every agent body
- [ ] No agent other than the Orchestrator has `user-invocable: true`
- [ ] Specialists have `disable-model-invocation: true` to protect them from external invocation (Orchestrator's explicit `agents` list still reaches them)
- [ ] `infer` property not used — deprecated in favour of `user-invocable` / `disable-model-invocation`

---

## Orchestrator Design Rules

The Orchestrator is the most critical agent in the squad. It must:

1. **Be the sole user entry point.** `user-invocable: true`. All other agents are hidden.
2. **Never implement.** Its `tools` must not include `edit` or `execute`. It can only
   `search`, `read`, invoke agents, read/write memory, and ask questions.
3. **Own the workflow.** The body must describe the exact sequence — which agents run
   in parallel, which are serial, and when to loop (e.g., re-run after a review finding).
4. **Write the task brief first.** Before invoking any agent, the Orchestrator writes
   `/memories/session/task-brief.md` with: task summary, scope, agents required, and
   key constraints from the project's coding rules.
5. **Report progress.** Announce each phase to the user before invoking the agent
   (e.g., `⏳ Planning…`, `⏳ Implementing backend…`).
6. **Clarify before acting.** Use `vscode/askQuestions` if the scope is ambiguous.
   Ask only what is essential — one round, not a dialogue.

### Parallel Execution

When agents are logically independent (they don't read each other's output), tell the
Orchestrator to invoke them in the same turn. Annotate the workflow with a visual:

```
② SpecialistA  ┐
② SpecialistB  ┘  invoke both in the same turn
```

In the body, instruct: *"Invoke SpecialistA and SpecialistB in the same turn — they
read the same plan and their output files are independent."*

When a later step depends on multiple earlier outputs, instruct the Orchestrator to
wait and read all outputs before proceeding.

---

## Planner Design Rules

The Planner is always serial (nothing runs before it; everything runs after it):

1. **Read `task-brief.md` first** and surface any ambiguities via `vscode/askQuestions`.
2. **Explore the codebase** to find analogous patterns before writing the plan.
3. **Write `plan.md`** with enough detail that every specialist can work independently
   without making architectural decisions.
4. **Never write production code.** The Planner's `tools` must not include `edit`.
5. **Surface blockers.** If something is unclear or a prerequisite is missing, the plan
   must say so explicitly rather than proceeding with assumptions.

---

## Specialist Design Rules

Each specialist should:

1. **Read its inputs from memory first** (`plan.md`, `task-brief.md`).
2. **Do exactly one thing well.** Resist scope creep — a reviewer shouldn't fix bugs.
3. **Write output to a dedicated memory file** (e.g., `/memories/session/backend-output.md`).
4. **Ask questions sparingly.** Use `vscode/askQuestions` only when genuinely blocked —
   not to validate routine decisions. A good plan should eliminate most questions.
5. **Run verification** where applicable (build, tests, lint) and report results.
6. **Include an authentication-failure protocol** if the agent calls external services:
   stop and report to the Orchestrator rather than retrying silently.
7. **Protect from accidental external invocation.** Set `user-invocable: false` and
   `disable-model-invocation: true` on every specialist. The Orchestrator's explicit
   `agents` list is the only thing that grants access — it overrides the protection
   intentionally. Without this, other agents in the workspace can invoke a specialist
   out of context, wasting tokens and producing irrelevant output.
8. **Decompose multi-step protocols into sub-agents.** If the specialist has 3 or more
   distinct protocol steps (e.g., explore, implement, verify), write the body so that
   each step is delegated to a separate sub-agent invocation rather than executed in a
   single context. Add `agent` to the specialist's `tools` and use `agents: ['*']` for
   ad-hoc sub-agents. Independent steps should be invoked in the same turn (parallel).
   See **Multi-Step Protocols and Sub-Agents** in `copilot-custom-agent/SKILL.md`.

---

## Updating an Existing Squad

If the project already has agents in `.github/agents/`:

1. **Read existing files** before changing anything.
2. **Add new specialists** without touching agents that already work.
3. **Update the Orchestrator** — add the new agent to `agents:`, update the workflow
   sequence, and add the agent to `task-brief.md` template.
4. **Keep memory paths consistent** — new agents must use the same conventions as
   existing ones.

---

## Output Checklist

Before handing back to the user, confirm:

- [ ] All `.agent.md` files created in the agreed location
- [ ] Orchestrator references every specialist by exact name
- [ ] Status protocol embedded in every agent
- [ ] Memory paths are consistent across the squad
- [ ] Parallel sections clearly annotated in Orchestrator workflow
- [ ] `vscode/askQuestions` included in tools for agents that may need user input
- [ ] Specialists with 3+ protocol steps use sub-agents per step (`agent` in tools, steps invoke sub-agents)
- [ ] Specialists have `disable-model-invocation: true`; Orchestrator lists them explicitly in `agents:`
- [ ] User was shown the final squad design and confirmed it

---

## Evaluating Squad Performance

The skill ships a benchmark harness in `scripts/` that measures whether a squad improves
output quality compared to running without any squad. Run it after creating or changing
agents to validate the change objectively.

### How It Works

For every eval in `evals/squad-evals.json` the harness:
1. Runs the task prompt with **no agent** (`without_squad`)
2. Runs the same prompt through **each provided orchestrator** (`with_squad-<stem>`)
3. Grades every run's output against the eval assertions using the Copilot SDK as grader
4. Aggregates all results into `benchmark.json` with mean/stddev pass-rate per config

The squad **must exist before calling the benchmark** — the harness does not create agents.

**Mandatory isolation rule:** Every benchmark run must execute with git worktree isolation.
Do not run or document any benchmark flow that bypasses worktrees.

### Directory Layout Produced

```
<output-dir>/
├── eval-1/
│   ├── without_squad/
│   │   └── run-1/
│   │       ├── outputs/task/       # files written by the model
│   │       ├── eval_metadata.json
│   │       ├── timing.json
│   │       └── grading.json
│   ├── with_squad-Orchestrator/    # one directory per orchestrator
│   │   └── run-1/ ...
│   └── with_squad-OtherOrch/
│       └── run-1/ ...
├── eval-2/ ...
├── benchmark.json                  # aggregated stats + printed summary table
└── review.html                     # standalone viewer (headless environments only)
```

### Running the Benchmark

Before running any benchmark command, verify the workspace is a git repository (required
because the harness always creates a git worktree per eval run):

```bash
git -C <workspace-root> rev-parse --is-inside-work-tree
```

If this check fails, stop and initialize git first. Do not continue without worktrees.

```bash
cd .github/skills/copilot-squad

# Single orchestrator — compare squad vs no squad
python -m scripts.run_benchmark \
  --eval-set evals/squad-evals.json \
  --skill-path . \
  --orchestrators .github/agents/Orchestrator.agent.md

# Compare multiple orchestrators against each other and against no squad
python -m scripts.run_benchmark \
  --eval-set evals/squad-evals.json \
  --skill-path . \
  --orchestrators .github/agents/Orchestrator-v1.agent.md \
                  .github/agents/Orchestrator-v2.agent.md

# Useful flags
  --output-dir benchmarks/my-run      # default: benchmarks/<ISO-timestamp>
  --eval-ids 1,2                       # run only specific eval IDs
  --runs-per-config 3                  # multiple runs for variance analysis
  --model gpt-4.1                      # pin model for all runs
  --grade-only                         # skip exec, re-grade existing runs
  --previous-workspace benchmarks/old  # show previous iteration in viewer
  --no-viewer                          # skip auto-launch (CI environments)
  --verbose                            # print progress to stderr
```

### Viewing Results

After the benchmark completes, the viewer opens automatically:

- **Display available**: A local HTTP server starts and your browser opens at
  `http://localhost:3117`. The page updates on refresh — you can re-run individual
  evals and refresh without restarting the server.
- **Headless / CI**: A standalone `review.html` is written to the output directory
  instead. Open it in any browser to review results.

To **re-open the viewer** after the fact (or to point `--previous-workspace` at an
earlier run for comparison):

```bash
python eval-viewer/generate_review.py benchmarks/my-run \
  --skill-name copilot-squad \
  --benchmark benchmarks/my-run/benchmark.json \
  [--previous-workspace benchmarks/older-run]
```

#### What the user sees

The viewer has two tabs:

**Outputs tab** — one card per test-case run:
- **Prompt**: the task that was sent to the agent
- **Config badge**: `with squad` (blue) or `without squad` (amber) — use this to compare
  the two configs side by side while navigating
- **Output**: files written during the run, rendered inline (text, code, images)
- **Previous Output** (when `--previous-workspace` is supplied): collapsed section showing
  the prior iteration's output for direct comparison
- **Formal Grades**: collapsed pass/fail per assertion with supporting evidence
- **Feedback**: text box that auto-saves as you type

**Benchmark tab** — quantitative summary:
- Pass rate, time, and token usage for each configuration (mean ± stddev)
- Delta column shows the squad's improvement over baseline
- Per-eval breakdown with per-assertion pass/fail across all runs

When finished reviewing, the user clicks **Submit All Reviews**. This saves all feedback to
`feedback.json` in the output directory.

Tell the user something like: "I've opened the results in your browser. The **Outputs** tab
lets you click through each test case and leave notes; the **Benchmark** tab shows the
quantitative comparison. When you're done, come back and let me know."

#### Reading feedback

After the user returns, read `feedback.json`:

```json
{
  "reviews": [
    {"run_id": "eval-1-with_squad-Orchestrator-run-1", "feedback": "missing file header", "timestamp": "..."},
    {"run_id": "eval-2-with_squad-Orchestrator-run-1", "feedback": "", "timestamp": "..."},
  ],
  "status": "complete"
}
```

Empty feedback means the user was satisfied. Focus improvements on runs where the user
left specific complaints.

#### Iterative improvement loop

```
run_benchmark → review in viewer → improve agents → re-run with --previous-workspace
```

Repeat until:
- All feedback is empty (everything looks good), or
- The user says they're happy

Keep going — one iteration of improvement rarely gets you to the finish line.

### Eval File Format (`evals/squad-evals.json`)

```jsonc
{
  "skill_name": "copilot-squad",
  "evals": [
    {
      "id": 1,
      "prompt": "Task description sent verbatim to the agent",
      "expected_output": "Human-readable description of ideal output (used as grader context)",
      "assertions": [
        { "id": "creates-entity", "text": "A JPA entity class is created" },
        { "id": "uses-annotations", "text": "Spring annotations are used correctly" }
      ]
    }
  ]
}
```

Write `assertion.text` as **observable facts** that can be verified from the output files —
e.g. "A file named X is created", "The endpoint returns Y". Avoid vague criteria like
"the code is correct".

### Scripts Reference

| Script | Purpose |
|---|---|
| `scripts/run_benchmark.py` | Orchestrates the full benchmark loop (exec → grade → aggregate) |
| `scripts/run_exec.py` | Runs a single eval prompt via the Copilot SDK; captures file writes to `outputs/task/` |
| `scripts/grade.py` | Grades one run's `outputs/task/` against assertions; writes `grading.json` |
| `scripts/aggregate_benchmark.py` | Reads all `grading.json` files; produces `benchmark.json` and prints summary table |

### Running the Test Suite

```bash
cd .github/skills/copilot-squad
python -m pytest tests/ -v   # 45 tests, no external dependencies required
```
