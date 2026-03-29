---
name: aspire
description: Use for any request involving Aspire orchestration, AppHost (C# or TypeScript), Aspire CLI, agent integration, MCP server, or distributed app workflows. Always trigger when the user mentions Aspire, AppHost, MCP, agent setup, distributed orchestration, or asks about multi-service, multi-language, or cloud-native .NET/TypeScript solutions—even if not named explicitly.
---

# Aspire Skill

This skill enables AI coding agents to:
- Create, manage, and orchestrate distributed applications using Aspire (C# or TypeScript AppHost)
- Use the Aspire CLI for project creation, orchestration, logs, and resource management
- Integrate with agent workflows (skill files, MCP server, Playwright, etc.)
- Support both .NET and TypeScript/Node.js stacks

## Reference Structure
- [CLI Reference](./references/cli.md)
- [Agent Integration](./references/agent.md)
- [TypeScript AppHost](./references/typescript-apphost.md)
- [Pipelines](./references/pipelines.md)
- [Health Checks](./references/health-checks.md)
- [Resource Model](./references/resource-model.md)
- [Resource Hierarchies](./references/resource-hierarchies.md)
- [Resource API Patterns](./references/resource-api-patterns.md)

## Usage Patterns
- Start/stop AppHost, view resource status, logs, and traces
- Add integrations, search docs, automate workflows
- Configure agent environments with `aspire agent init`

## For More
- See reference files above for details, commands, and examples
- For latest docs, always consult https://aspire.dev and Microsoft Learn

## Example Prompts
- "Create a new Aspire solution with a TypeScript AppHost and set up agent integration."
- "Show me how to start the AppHost and view logs using the Aspire CLI."
- "How do I migrate from AGENTS.md to the new skill file for agents?"
- "Add a Redis cache to my Aspire app and update the agent config."

---

For advanced CLI usage, troubleshooting, and multi-language orchestration, see the reference files in `./references/` and the official Aspire documentation.
