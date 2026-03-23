---
name: squad-creator
description: >
  **WORKFLOW SKILL** — Use this skill whenever a user wants to create, design, or scaffold
  a team of AI agents that work together. Triggers for: building multi-agent workflows,
  creating specialist agents for a project (backend, frontend, reviewer, tester, etc.),
  setting up agents that hand off work to each other, adding a new agent role to an
  existing team, or any request like "I want agents for my X project" even when phrased
  as wanting separate agents for separate concerns. The key signal is **multiple agents
  cooperating** — not just one agent. Produces ready-to-use `.agent.md` files with an
  Orchestrator, Planner, and project-tailored specialists. Does NOT trigger for:
  configuring a single agent, fixing agent instructions, setting up MCP servers, or
  questions about Copilot settings.
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
| `references/model-selection.md` | Which Copilot model to assign to each agent role |

Also read the **agent-customization SKILL.md** (`.github/skills/agent-customization/SKILL.md`)
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

Use the **agent-customization skill** as your guide for valid frontmatter and tool names.
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
- [ ] All `tools` values are real tool names (check `agent-customization/references/tools.md`)
- [ ] Memory file paths are consistent across all agents (same path for the same file)
- [ ] Status protocol is present in every agent body
- [ ] No agent other than the Orchestrator has `user-invocable: true`

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
- [ ] User was shown the final squad design and confirmed it
