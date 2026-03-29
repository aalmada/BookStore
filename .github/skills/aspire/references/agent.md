# Aspire Agent Integration

## Why Aspire for Agents
- Agents get the same visibility as developers: resource data, logs, traces
- MCP server and CLI expose all runtime info
- Skill file (`aspire agent init`) teaches agents CLI patterns and workflows
- Agent-native CLI: All commands are non-interactive, support `--format Json`, and are designed for automation and agent workflows
- **Detached mode (`--detach`)**: Agents (and users) can run the AppHost in the background using `aspire run --detach` or `aspire start`. This frees up the terminal and enables parallel, non-blocking workflows. Use `aspire ps` to list running apphosts and `aspire stop` to terminate them. Detached mode is ideal for agent-driven automation, CI, and multi-app scenarios.
- Agents can also use isolated mode (`--isolated`) for parallel, conflict-free runs, and wait for resource health.

## MCP Setup
1. Run `aspire agent init` in your project directory
2. Select:
   - Aspire skill file (recommended)
   - Playwright CLI (for browser automation)
   - Aspire MCP server (for runtime access)
3. Use `aspire agent mcp` to start the MCP server for agent communication
