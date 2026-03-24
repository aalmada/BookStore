---
name: copilot-custom-agent
description: >
  Use this skill when working with GitHub Copilot custom agent files (.agent.md) in VS Code.
  Use for: creating a new agent persona with a specific role; restricting an agent to only certain
  tools (e.g., making an agent read-only or limiting what it can edit); pinning a model to an agent;
  writing or refining agent body instructions; configuring handoffs or subagent orchestration;
  adding tool sets or MCP tools to an agent's configuration; troubleshooting agents that aren't
  showing up in the @agents picker or aren't being invoked.
  NOT for: designing a full multi-agent squad (use copilot-squad); setting up or configuring an MCP
  server itself; AGENTS.md or .instructions.md files (use agents-md); agents running in GitHub
  Copilot coding agent workflows outside VS Code.
---

# Agent Customization Skill

This skill helps you create and update `.agent.md` custom agent files — specialized AI personas
with their own instructions, tools, and workflows.

Read the reference files as needed:

| File | When to read it |
|---|---|
| `references/frontmatter.md` | All available YAML frontmatter properties |
| `references/tools.md` | Built-in tool names organized by category |
| `references/patterns.md` | Common patterns: orchestration, handoffs, memory, subagents |
| `references/model-selection.md` | Available models, multipliers, profiles, fallback chains, cost tips |

---

## The Golden Rule: Only Include Non-Obvious Things

Before adding any line to an agent body, ask:
**"Could an agent figure this out by reading the code or config files?"**

If yes — skip it. Agents can read directory structures, existing `.agent.md` files, `AGENTS.md`
context files, and existing code. Agent bodies are for things that *aren't* visible there:

- The agent's specific role and boundaries within the squad
- Non-obvious protocol steps the agent must follow every time
- Output contracts (what to write to memory, in what format)
- Tool restrictions that differ from the default

**Do not duplicate content already in `AGENTS.md`.** If a rule belongs in `AGENTS.md` (code
rules, patterns, common mistakes), put it there — not in the agent body. Agent bodies that
re-state `AGENTS.md` create stale divergence when rules change. Defer to `AGENTS.md` instead:

> "Read `<scope>/AGENTS.md` for the project rules that apply to this scope."

**Redundant content actively degrades quality** — it wastes the agent's context window and
dilutes real signal with noise it already has.

---

## When to Create a Custom Agent

Custom agents are the right choice when you need a **persistent persona** with:
- Specific tool restrictions (e.g., read-only planning mode)
- A preferred AI model
- Repeated specialized instructions you'd otherwise paste every time
- Handoffs to guide users from one step to the next
- Orchestration of other agents as subagents

For one-off tasks that don't need tool restrictions, use prompt files instead. For
portable reusable capabilities with scripts, use skills.

---

## File Locations

| Target | Location |
|---|---|
| Workspace (shared with team) | `.github/agents/<name>.agent.md` |
| Workspace (Claude format) | `.claude/agents/<name>.md` |
| User profile (personal, all workspaces) | `~/.copilot/agents/<name>.agent.md` |

VS Code also detects plain `.md` files in `.github/agents/`.

---

## File Structure

```
---
<YAML frontmatter>
---

<Markdown body — agent instructions>
```

The body is prepended to every user prompt when the agent is active. Keep it focused and
purposeful — include only what genuinely changes the agent's behavior.

For the full list of frontmatter properties, read `references/frontmatter.md`.
For all available tool names, read `references/tools.md`.

---

## Workflow: Creating a New Agent

### 1. Interview the user

Ask only what you need:
- What role / persona should this agent play?
- Which tools does it need? (default: all — be explicit only when restricting)
- Should it be user-selectable, or only invoked as a subagent?
- Does it start other agents? If so, which ones?
- Should it hand off to another agent when done?
- Target: VS Code only, GitHub Copilot only, or both?

### 2. Choose a name

The file name (without `.agent.md`) is the default agent name. Use PascalCase for
agent names that other agents will invoke by name (e.g., `BackendDeveloper`), or
kebab-case for user-facing agents (e.g., `security-reviewer`).

### 3. Write the frontmatter

Start with the minimal required fields:

```yaml
---
description: One sentence describing what this agent does and when to use it.
---
```

Add optional fields only when they add real value:
- `name` — if the file name isn't the right display name
- `tools` — to restrict from the default (all tools)
- `model` — to pin a specific model (see `references/model-selection.md`; omitting inherits the user's current selection, which is often preferable)
- `user-invocable: false` — for subagent-only agents
- `argument-hint` — to guide users on what to type
- `handoffs` — to suggest next steps
- `agents` — to restrict which subagents can be invoked

### 4. Write the body

Structure the body to tell the agent:
1. **What it is** — one-line identity statement
2. **Its protocol** — numbered steps it follows every time
3. **What it must not do** — explicit constraints
4. **Output format** — if it writes to files or memory

Apply the Golden Rule to every line: if the agent can read it from `AGENTS.md` or existing
code, skip it and link to `AGENTS.md` instead.

Keep the body under 100 lines for agents that need to stay lean. Longer bodies are
fine for complex orchestrators or specialists with rich domain knowledge.

### 5. Validate

Before saving, check:
- [ ] `description` is present and accurate
- [ ] `tools` list contains only real tool names (see `references/tools.md`)
- [ ] `agents` list matches the names in actual `.agent.md` files
- [ ] `model` value is a valid model name if specified
- [ ] YAML frontmatter is valid (no tabs, proper quoting)
- [ ] The body doesn't contradict the tool list (e.g., says "edit files" but `tools` has no `edit`)

---

## Workflow: Updating an Existing Agent

1. Read the current file
2. Understand why the change is needed — what behavior is missing or wrong?
3. Make the minimal change that fixes the problem
4. Don't rewrite sections that aren't broken

---

## Using `vscode/memory` and `vscode/askQuestions`

These two built-in tools are very commonly used together in multi-agent workflows.

**`vscode/memory`** — persistent key-value store that survives across chat sessions.
Use it for agents that need to hand off context to other agents (e.g., a Planner
writing its plan so a BackendDeveloper can read it).

**`vscode/askQuestions`** — displays a structured questions carousel to the user
instead of asking questions in prose. Use when an agent needs to resolve ambiguity
before proceeding.

Both tools must appear in the agent's `tools` list to be usable. See
`references/patterns.md` for worked examples of both.

---

## Starting Other Agents (Orchestration)

To allow an agent to invoke other agents:
1. Add `agent` to the `tools` list (this is the builtin `agent` tool set)
2. Add an `agents` list with the names of permitted subagents
3. In the body, specify exactly when and how to invoke each subagent

The `agents` list acts as an allowlist. Use `agents: ['*']` to allow all, or
`agents: []` to block subagent invocation entirely.

See `references/patterns.md` for an orchestrator pattern.

---

## Multi-Step Protocols and Sub-Agents

When an agent body has a numbered protocol with **distinct, independent steps**, each
step should be delegated to a separate sub-agent invocation rather than executed in a
single context. This prevents context window bloat, reduces interference between steps,
and allows independent steps to run in parallel.

### When to apply this pattern

Use sub-agents for individual protocol steps when **all** of these are true:
- The agent has 3 or more protocol steps
- Each step has a clearly different focus (e.g., explore, implement, verify)
- Steps do not need to carry the full accumulated context of earlier steps

### How to write the protocol

Instead of describing steps as inline instructions, tell the agent to invoke a
sub-agent for each step:

````markdown
## Protocol

This agent executes each step in a separate sub-agent to keep contexts lean.

### Step 1 — <name>
Invoke a sub-agent with: "<instruction for step 1 only>"
Output: write result to `/memories/session/<step1>.md`

### Step 2 — <name>
Read `/memories/session/<step1>.md`, then invoke a sub-agent with: "<instruction>"
Output: write result to `/memories/session/<step2>.md`

### Step 3 — <name> (parallel with Step 2 if independent)
Invoke a sub-agent in the **same turn** as Step 2 with: "<instruction>"
Output: write result to `/memories/session/<step3>.md`
````

### Parallel steps

When two steps don't depend on each other's output, invoke their sub-agents **in the
same turn**. Annotate this clearly in the protocol:

```
② Step A  ┐  invoke in the same turn
② Step B  ┘
③ Step C     (depends on A and B — invoke after both finish)
```

### Frontmatter requirements

An agent that delegates steps to sub-agents must have `agent` in its `tools` list.
Use `agents: ['*']` if the sub-agents are ad-hoc (no named `.agent.md` files). Use a
named list when the sub-agents are fixed specialist agents.

```yaml
tools: ['search', 'read', 'vscode/memory', 'agent']
agents: ['*']
```

### Memory handoff between steps

Sub-agents communicate exclusively through `vscode/memory`. The parent agent writes
a brief for each step before invoking it, and the sub-agent writes its output to a
dedicated memory file when done. The parent reads that file before invoking the next step.

---

## Security Considerations

- Grant agents only the tools they actually need (principle of least privilege)
- Read-only agents (`tools: ['search', 'read']`) cannot accidentally modify files
- Agents that run terminal commands (`execute`) should have a narrowly scoped body
- Avoid including secrets or credentials in agent body text
