# Built-in Tools Reference

Tools in `.agent.md` are specified in the `tools` list. You can use **tool set names**
(a category of related tools) or **individual tool names** (a single tool within a set).

Enabling a **tool set** enables all tools in that category. Enabling an individual tool
enables only that one.

---

## Tool Sets and Their Individual Tools

### `search` — Find files and code in the workspace

| Tool name | Description |
|---|---|
| `search` | *(set)* All search tools |
| `search/codebase` | Semantic code search — finds conceptually relevant code |
| `search/fileSearch` | Find files by glob pattern, returns paths |
| `search/textSearch` | Find text in files (like grep) |
| `search/listDirectory` | List files in a directory |
| `search/usages` | Find all references, implementations, and definitions of a symbol |
| `search/changes` | List current source control changes |

### `read` — Read files and workspace state

| Tool name | Description |
|---|---|
| `read` | *(set)* All read tools |
| `read/readFile` | Read the content of a file |
| `read/problems` | Get workspace errors and warnings from the Problems panel |
| `read/terminalLastCommand` | Get the last run terminal command and its output |
| `read/terminalSelection` | Get the currently selected text in the terminal |
| `read/getNotebookSummary` | Get cell list and details for a Jupyter notebook |
| `read/readNotebookCellOutput` | Read the output from a notebook cell |

### `edit` — Create and modify files

| Tool name | Description |
|---|---|
| `edit` | *(set)* All edit tools |
| `edit/editFiles` | Apply edits to files in the workspace |
| `edit/createFile` | Create a new file |
| `edit/createDirectory` | Create a new directory |
| `edit/editNotebook` | Edit a Jupyter notebook |

### `execute` — Run code and commands

| Tool name | Description |
|---|---|
| `execute` | *(set)* All execute tools |
| `execute/runInTerminal` | Run a shell command in the integrated terminal |
| `execute/getTerminalOutput` | Get output from a running terminal command |
| `execute/createAndRunTask` | Create and run a VS Code task |
| `execute/runNotebookCell` | Run a notebook cell |
| `execute/testFailure` | Get unit test failure details — useful for diagnosing test runs |

### `web` — Access external URLs

| Tool name | Description |
|---|---|
| `web` | *(set)* All web tools |
| `web/fetch` | Fetch the content of a URL |

### `agent` — Invoke other agents as subagents

| Tool name | Description |
|---|---|
| `agent` | *(set)* All agent invocation tools |
| `agent/runSubagent` | Run a task in an isolated subagent context with separate context window |

> The `agent` tool set must be included for any agent that invokes other agents.
> Also add an `agents` list in the frontmatter to control which agents can be invoked.

### `browser` — Interact with the integrated browser *(Experimental)*

Requires `workbench.browser.enableChatTools` to be enabled.

| Tool name | Description |
|---|---|
| `browser` | *(set)* All browser tools |

---

## VS Code Special Tools

These tools are named with the `vscode/` prefix and are not part of a tool set.
Each must be listed individually.

| Tool name | Description |
|---|---|
| `vscode/askQuestions` | Display an interactive questions carousel to the user. Enables structured clarification before the agent proceeds. Blocks until the user answers. |
| `vscode/memory` | Read and write persistent notes that survive across chat sessions. Used for agent-to-agent handoffs in multi-agent workflows. |
| `vscode/extensions` | Search for and ask about VS Code extensions |
| `vscode/installExtension` | Install a VS Code extension |
| `vscode/runCommand` | Run a VS Code command |
| `vscode/getProjectSetupInfo` | Get project scaffolding instructions |
| `vscode/VSCodeAPI` | Ask about VS Code extension development APIs |

---

## Standalone Tools (No Set)

| Tool name | Description |
|---|---|
| `#todos` | Structured todo list to track task progress |
| `#selection` | Get the current editor selection (active only when text is selected) |
| `#newWorkspace` | Create a new VS Code workspace |

---

## MCP Server Tools

Tools from MCP servers follow the `<server-name>/<tool-name>` pattern.

```yaml
# Enable all tools from a specific MCP server
tools: ['my-mcp-server/*']

# Enable a single tool from an MCP server
tools: ['github/create_pull_request']

# Out-of-the-box servers for GitHub Copilot coding agent
tools: ['github/*', 'playwright/*']
```

---

## Extension Tools

Tools contributed by VS Code extensions use the `<extension-id>/<tool-name>` pattern.

```yaml
tools: ['azure.some-extension/some-tool']
```

---

## Quick Reference: Common Tool Combinations

| Agent type | Typical `tools` value |
|---|---|
| Read-only analyst / reviewer | `['search', 'read']` |
| Planner (reads, asks, delegates) | `['search', 'read', 'web', 'vscode/memory', 'vscode/askQuestions', 'agent']` |
| Implementer (writes code) | `['search', 'read', 'edit', 'execute/runInTerminal', 'vscode/memory']` |
| Orchestrator (delegates everything) | `['search', 'read', 'vscode/memory', 'agent', 'vscode/askQuestions']` |
| Full-access agent | omit `tools` entirely |
| No tools | `[]` |
