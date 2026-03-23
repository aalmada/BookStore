---
name: agent-customization
description: >
  **WORKFLOW SKILL** — Create, update, review, fix, or debug VS Code agent customization files
  (.instructions.md, .prompt.md, .agent.md, SKILL.md, copilot-instructions.md, AGENTS.md).
  USE FOR: saving coding preferences; troubleshooting why instructions/skills/agents are ignored
  or not invoked; configuring applyTo patterns; defining tool restrictions; creating custom agent
  modes or specialized workflows; packaging domain knowledge; fixing YAML frontmatter syntax.
  DO NOT USE FOR: general coding questions (use default agent); runtime debugging or error
  diagnosis; MCP server configuration (use MCP docs directly); VS Code extension development.
  INVOKES: file system tools (read/write customization files), ask-questions tool (interview user
  for requirements), subagents for codebase exploration. FOR SINGLE OPERATIONS: For quick YAML
  frontmatter fixes or creating a single file from a known pattern, edit the file directly — no
  skill needed.
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
- `model` — to pin a specific model
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

## Security Considerations

- Grant agents only the tools they actually need (principle of least privilege)
- Read-only agents (`tools: ['search', 'read']`) cannot accidentally modify files
- Agents that run terminal commands (`execute`) should have a narrowly scoped body
- Avoid including secrets or credentials in agent body text
