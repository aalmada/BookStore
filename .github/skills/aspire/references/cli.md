# Aspire CLI Reference

## Installation & Upgrade
- Install: `curl -sSL https://aspire.dev/install.sh | bash`
- Upgrade: `aspire update --self`
- Validate: `aspire --version`
- Docs: https://aspire.dev/get-started/install-cli/

## Key CLI Enhancements (13.2+)
- **Language-aware templates:** `aspire new` and `aspire init` support C#, TypeScript, Python, and multi-language setups.
- **Detached/background mode:** `aspire run --detach` or `aspire start` runs the AppHost in the background. Use `aspire ps` to list, `aspire stop` to stop.
- **Isolated mode:** `--isolated` flag runs parallel apphosts with isolated ports/secrets for agent/CI/test workflows.
- **Resource commands:** Unified `aspire resource <name> <command>` (start/stop/restart/rebuild) for fine-grained control.
- **Resource monitoring:** `aspire describe` and `aspire describe --follow` stream resource state and config.
- **Environment diagnostics:** `aspire doctor` checks certs, containers, SDKs, agent config, and more.
- **Fuzzy search for integrations:** `aspire add` supports fuzzy search for packages.
- **Secrets/certs management:** `aspire certs` and `aspire secret` for dev certs and user secrets (no .NET CLI needed).
- **Wait/export:** `aspire wait` blocks until resource is healthy; `aspire export` captures telemetry and config.
- **Agent integration:** `aspire agent init` sets up agent skill files and MCP; `aspire agent mcp` starts the MCP server.
- **Docs from CLI:** `aspire docs list|search|get` brings official docs into the terminal (supports `--format Json`).
- **Unified config:** `aspire.config.json` replaces legacy config files; auto-migrates on first run.
- **Option standardization:** `-v` for version, `--format` for output, `--log-level`/`-l` for log filtering.
- **Other:** `aspire run --no-build`, improved JSON output, better error/logging, and multi-language support.

## Agent-Friendly Features
- All commands support `--format Json` for structured output
- Non-interactive and automation-ready
- CLI exposes all dashboard/apphost data for scripts, editors, and agents
- See: https://aspire.dev/get-started/ai-coding-agents/

## More
- Full CLI docs: https://aspire.dev/reference/cli/overview/
- Troubleshooting: https://aspire.dev/get-started/troubleshooting/
- What's new: https://aspire.dev/whats-new/aspire-13-2/#%EF%B8%8F-cli-enhancements
