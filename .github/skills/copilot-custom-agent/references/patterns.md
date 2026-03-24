# Common Patterns

---

## Pattern 1: Orchestrator–Specialist Workflow

An orchestrator receives tasks from the user and delegates to specialists. Specialists
are hidden from the user (they're never invoked directly).

### orchestrator.agent.md

```yaml
---
name: Orchestrator
description: Routes tasks to the right specialist agents. Start every task here.
argument-hint: Describe the feature or change you want to make.
target: vscode
user-invocable: true
model: Claude Sonnet 4.6 (copilot)
tools: ['search', 'read', 'vscode/memory', 'agent', 'vscode/askQuestions']
agents: ['Planner', 'Implementer', 'CodeReviewer']
handoffs:
  - label: Start over
    agent: Orchestrator
    prompt: The task is complete. Describe the next feature to work on.
    send: false
---

You are the **Orchestrator**. You coordinate the full team. You do not write code yourself.

## Workflow

1. Use `vscode/askQuestions` to clarify the task if it is ambiguous.
2. Write a task brief to `/memories/session/task-brief.md` via `vscode/memory`.
3. Invoke `Planner` → wait for plan.
4. Invoke `Implementer` → wait for implementation.
5. Invoke `CodeReviewer` → read review findings.
6. If findings are Critical or Major, invoke `Implementer` again to fix them.
7. Repeat review/fix until the review is clean.
```

### planner.agent.md

```yaml
---
name: Planner
description: Researches the codebase and produces a step-by-step implementation plan. Never writes code.
user-invocable: false
target: vscode
tools: ['search', 'read', 'web', 'vscode/memory', 'vscode/askQuestions']
---

You are the **Planner**. You read the task brief and produce a detailed plan.

## Protocol

1. Read `/memories/session/task-brief.md`.
2. Explore the codebase to find analogous patterns.
3. Ask clarifying questions with `vscode/askQuestions` if anything is unclear.
4. Write the plan to `/memories/session/plan.md` via `vscode/memory`.
```

### implementer.agent.md

```yaml
---
name: Implementer
description: Implements the plan written by the Planner. Reads plan from memory, writes code, reports back.
user-invocable: false
target: vscode
tools: ['search', 'read', 'edit', 'execute/runInTerminal', 'vscode/memory']
---

You are the **Implementer**.

## Protocol

1. Read `/memories/session/plan.md`.
2. Implement every step exactly as specified.
3. Run the build after all changes.
4. Write implementation notes to `/memories/session/impl-output.md` via `vscode/memory`.
```

---

## Pattern 2: Using `vscode/memory` for Context Handoff

`vscode/memory` provides a persistent key-value file store at `/memories/`.
It survives across conversations and is the standard way to pass context between agents.

### Writing memory

In the agent body, instruct the agent to write its outputs:

```markdown
After completing your analysis, write your findings to `/memories/session/analysis.md`
using the `vscode/memory` tool. Include:
- Files examined
- Key observations
- Recommended changes
```

### Reading memory

In a downstream agent, instruct it to read first:

```markdown
Before doing anything else, read `/memories/session/analysis.md` using `vscode/memory`.
Treat its contents as your primary instruction source.
```

### Memory path conventions

| Path | Purpose |
|---|---|
| `/memories/session/task-brief.md` | Orchestrator writes the task scope |
| `/memories/session/plan.md` | Planner writes the implementation plan |
| `/memories/session/impl-output.md` | Implementer writes what it changed |
| `/memories/session/review.md` | Code reviewer writes its findings |
| `/memories/` | User preferences, reusable notes (survives sessions) |
| `/memories/repo/` | Repository-scoped facts |

---

## Pattern 3: Using `vscode/askQuestions`

`vscode/askQuestions` displays a structured questions carousel in the VS Code chat UI.
Use it at the start of an agent's workflow when the user's request could be ambiguous.

### In the agent body

```markdown
## Step 1 — Clarify

If anything about the task is ambiguous, use `vscode/askQuestions` to ask only what
you truly need to know. For example:
- Is this change backend-only, frontend-only, or full-stack?
- Should the new endpoint be authenticated?

Do not ask questions whose answers are already apparent from the task description.
```

### Best practices

- Ask at most 3–4 questions at once; batching is fine, interrogating the user is not.
- Only ask what will materially change what you do next.
- Never ask questions that can be answered by reading the codebase.
- Chain `vscode/askQuestions` in the `tools` list with `vscode/memory` if you need to
  preserve answers for downstream agents.

---

## Pattern 4: Handoff Chains

Handoffs create guided sequential workflows. After the agent's response, buttons appear
to let the user move to the next step.

```yaml
handoffs:
  # Offered when planning is done — moves to implementation
  - label: Start Implementation
    agent: Implementer
    prompt: The plan is approved. Implement all changes described in /memories/session/plan.md.
    send: false      # user reviews prompt before it's sent

  # Offered when implementation is done — auto-starts review
  - label: Review Code
    agent: CodeReviewer
    prompt: Review all changed files for correctness and security.
    send: true       # review starts automatically
```

**When to use `send: true`**: safe, non-destructive next steps that the user is
expected to always approve (e.g., review, run tests). Don't use `send: true` for
steps that modify files or deploy anything.

---

## Pattern 5: Subagent with Isolated Context

Use `agent/runSubagent` instead of `agent` when you want the subagent to run in an
isolated context — it gets its own context window, which prevents the main thread
from filling up with the subagent's tool calls.

```markdown
When you need a deep codebase search, invoke the `Explore` subagent via
`agent/runSubagent` rather than searching inline. Pass it a clear description of
what you're looking for and what you need it to return.
```

```yaml
tools: ['search', 'read', 'agent']
agents: ['Explore']
```

---

## Pattern 6: Read-Only Agents

For reviewers, auditors, and planners that should never accidentally modify files:

```yaml
---
name: SecurityAuditor
description: Reviews all files for OWASP Top 10 vulnerabilities. Never edits files.
tools: ['search', 'read']
---
```

The absence of `edit` and `execute` from the `tools` list makes it impossible for
this agent to create, modify, or delete files, or run commands — even if the body
instructions suggest it.

---

## Pattern 7: Parallel Multi-Perspective Review

Run each review lens as a **parallel** subagent so findings stay independent and unbiased.
The coordinator shapes each subagent's focus through its prompt — no extra agent files
needed for a lightweight version.

```yaml
---
name: Thorough Reviewer
description: Reviews code from multiple angles in parallel and synthesizes findings.
tools: ['agent', 'read', 'search']
---
Review the changed files through multiple perspectives simultaneously.
Run these subagents **in parallel** (invoke all in the same turn):
- Correctness: logic errors, edge cases, type issues.
- Code quality: readability, naming, duplication.
- Security: OWASP Top 10 — injection risks, input validation, data exposure.
- Architecture: codebase patterns, design consistency, structural alignment.

After all subagents complete, synthesize findings into a prioritised summary.
Mark issues Critical / Major / Minor. Acknowledge what the code does well.
```

**Why parallel works here:** Each subagent approaches the code fresh, without being
anchored by what other perspectives found. Independence improves coverage.

**Scaling up:** Give each perspective its own `.agent.md` file when it needs special
tools — for example, a security reviewer with a security-focused MCP server, or a
code-quality reviewer with linting CLI tools.

**What the user sees:** Each subagent run appears as a collapsible tool call in chat —
collapsed by default (shows agent name + current tool); expand to see the full transcript,
prompt, and returned result.

---

## Pattern 8: Claude Format (`.claude/agents/`)

If you want to share an agent with Claude Code users, place it in `.claude/agents/`
as a `.md` file. The frontmatter uses Claude-specific keys:

```yaml
---
name: TestEngineer
description: Writes unit and integration tests. Does not modify production code.
tools: Read, Grep, Glob, Bash, Edit
disallowedTools: WebSearch
---
```

VS Code automatically maps Claude tool names to their VS Code equivalents, so the
same file works in both environments. The VS Code `.agent.md` format (with YAML arrays)
is preferred when you only need VS Code support.
