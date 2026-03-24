# Frontmatter Properties Reference

All properties are optional unless marked **required**. Unrecognized properties are ignored.

---

## Core identity

### `description` *(required)*

What the agent does and when to invoke it. Shown as placeholder text in the chat input when
the agent is selected. Also used by AI to decide whether to invoke it as a subagent.

```yaml
description: Reviews code changes for security vulnerabilities and convention violations.
```

### `name`

Display name shown in the agents picker. Defaults to the file name without `.agent.md`.
Use when the file name and the display name need to differ.

```yaml
name: Security Reviewer
```

### `argument-hint`

Short hint shown in the chat input field to guide users on what to type.

```yaml
argument-hint: Describe what to review, or say "review all" to check every changed file.
```

---

## Tools

### `tools`

List of tools the agent can use. Omit to enable all tools. Use `[]` to disable all tools.
Values can be tool set names (e.g., `read`), individual tool names (e.g., `read/readFile`),
MCP tool names (e.g., `github/*`), or extension tools (e.g., `azure.some-ext/some-tool`).

```yaml
# All tools (default when omitted)
tools: ['*']

# Specific categories
tools: ['search', 'read', 'edit', 'execute', 'web']

# Individual tools from a category
tools: ['search/codebase', 'read/readFile', 'edit/editFiles']

# Mix of sets and individual tools
tools: ['search', 'read', 'vscode/memory', 'vscode/askQuestions']

# Disable all tools
tools: []
```

For the full list of available tool names, see `tools.md`.

### `agents`

List of agent names that can be invoked as subagents. Requires `agent` in `tools`.
- Omit → no subagent restriction (all agents usable)
- `['*']` → explicitly allow all
- `[]` → block all subagent invocation
- Named list → allowlist of permitted subagents

```yaml
agents: ['Planner', 'BackendDeveloper', 'TestEngineer']
```

---

## Model

### `model`

The AI model to use. Inherits the user's current selection if omitted.
Specify a qualified name (`Model Name (vendor)`) or just the model name.
An array of models can be given — the system tries each in order until one is available.

```yaml
# Single model
model: Claude Sonnet 4.6 (copilot)

# Prioritized list (fallback chain)
model:
  - GPT-5.3-Codex (copilot)
  - Claude Sonnet 4.6 (copilot)
```

---

## Visibility and invocation

### `user-invocable`

Controls whether the agent appears in the agents dropdown. Defaults to `true`.
Set to `false` for subagent-only agents (helper agents always invoked by other agents,
never selected directly by the user).

```yaml
user-invocable: false
```

### `disable-model-invocation`

When `true`, prevents other agents from automatically invoking this agent as a subagent
based on task context. The agent can still be manually selected. Defaults to `false`.

```yaml
disable-model-invocation: true
```

**Override rule:** Explicitly naming an agent in a coordinator's `agents` list overrides
`disable-model-invocation: true`. This lets you protect a specialist from general use
while still allowing one specific coordinator to invoke it.

```yaml
# Specialist — protected from general subagent use
---
name: DatabaseMigrator
user-invocable: false
disable-model-invocation: true
---

# Coordinator — explicit list overrides the protection above
---
agents: ['DatabaseMigrator', 'SchemaReviewer']
---
```

### `infer` *(deprecated)*

Replaced by `user-invocable` and `disable-model-invocation`. If both old and new properties
are set, the new ones take precedence.

---

## Targeting

### `target`

Where the agent is available. Defaults to both.

| Value | Meaning |
|---|---|
| `vscode` | VS Code and other IDEs only |
| `github-copilot` | GitHub Copilot coding agent on GitHub.com only |
| *(omit)* | Both environments |

```yaml
target: vscode
```

---

## Handoffs

### `handoffs`

List of suggested next actions shown as buttons after the agent's response. Each handoff
switches to another agent and optionally pre-fills a prompt.

```yaml
handoffs:
  - label: Start Implementation
    agent: BackendDeveloper
    prompt: "The plan is ready. Implement the backend changes described in /memories/session/plan.md."
    send: false

  - label: Review Changes
    agent: CodeReviewer
    prompt: "Review all changes made in this session."
    send: true
    model: GPT-5.4 (copilot)
```

**Handoff properties:**

| Property | Type | Required | Description |
|---|---|---|---|
| `label` | string | yes | Button text shown to the user |
| `agent` | string | yes | Target agent identifier (file name without `.agent.md`) |
| `prompt` | string | no | Pre-filled prompt for the target agent |
| `send` | boolean | no | Auto-submit the prompt immediately (default: `false`) |
| `model` | string | no | Model to use when the handoff executes |

---

## Scoped hooks (Preview)

### `hooks`

Hook commands scoped to this agent — they run only when this agent is active, either
directly or as a subagent. Uses the same format as hook configuration files.
Requires `chat.useCustomAgentHooks` to be enabled.

```yaml
hooks:
  onFileEdit:
    - command: dotnet format --include {{file}}
```

---

## GitHub Copilot coding agent only

These properties apply only when `target: github-copilot` (or when target is unset and
the agent is used on GitHub.com). They are ignored in VS Code.

### `mcp-servers`

MCP servers to make available to this agent. YAML representation of the MCP JSON config format.

```yaml
mcp-servers:
  my-server:
    type: local
    command: npx
    args: ['-y', 'my-mcp-package']
    tools: ['*']
    env:
      API_KEY: ${{ secrets.MY_API_KEY }}
```

### `metadata`

Key-value annotations for the agent. Not used by VS Code.

```yaml
metadata:
  team: platform
  version: "2"
```

---

## GitHub Copilot coding agent — tool aliases

When `target` is unset or `github-copilot`, these aliases can be used in the `tools` list:

| Alias | Maps to | Description |
|---|---|---|
| `execute` | shell, Bash, powershell | Run shell commands |
| `read` | Read, NotebookRead, view | Read files |
| `edit` | Edit, MultiEdit, Write, NotebookEdit | Edit files |
| `search` | Grep, Glob | Search in files |
| `agent` | Task, custom-agent | Invoke another agent |
| `web` | WebSearch, WebFetch | Fetch URLs, web search |
| `todo` | TodoWrite | Structured task lists |

---

## Minimal complete examples

### Read-only planner (VS Code)

```yaml
---
name: Planner
description: Researches the codebase and produces a detailed implementation plan. Does not write code.
argument-hint: Describe the feature to plan.
target: vscode
user-invocable: false
model: Claude Sonnet 4.6 (copilot)
tools: ['search', 'read', 'web', 'vscode/memory', 'vscode/askQuestions', 'agent']
agents: ['Explore']
---
```

### Orchestrator with handoffs (VS Code)

```yaml
---
name: Orchestrator
description: Routes tasks to the right specialist agents. Always start here.
argument-hint: Describe the feature or task to deliver.
target: vscode
user-invocable: true
model: Claude Sonnet 4.6 (copilot)
tools: ['search', 'read', 'vscode/memory', 'agent', 'vscode/askQuestions']
agents: ['Planner', 'BackendDeveloper', 'TestEngineer', 'CodeReviewer']
handoffs:
  - label: Run tests
    agent: TestEngineer
    prompt: Run the full test suite and report results.
    send: true
---
```

### Locked-down reviewer (VS Code + GitHub)

```yaml
---
name: security-reviewer
description: Reviews code for OWASP Top 10 vulnerabilities. Read-only — never edits files.
tools: ['search', 'read']
model: GPT-5.4 (copilot)
---
```
