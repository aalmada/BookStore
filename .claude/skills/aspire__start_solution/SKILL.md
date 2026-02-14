---
name: aspire__start_solution
description: Start the BookStore solution using Aspire CLI in background mode. Use this to run the full application stack (API, Web, PostgreSQL, Redis, Azurite).
---

Follow this guide to start the BookStore solution using Aspire CLI.

## Prerequisites Check

1. **Verify Environment** (optional)
   - Run `/ops__doctor_check` skill to ensure all tools are installed

## Start the Solution

// turbo
2. **Start Aspire in Background**
   ```bash
   cd /home/avazalma/projects/BookStore && aspire run &
   ```
   - Starts: API, Web, PostgreSQL, Redis, Azurite, PgAdmin
   - Dashboard URL: `http://localhost:15888` (check terminal output for actual port)
   - API URL: `http://localhost:5000` (or assigned port)
   - Web URL: `http://localhost:5001` (or assigned port)

3. **Verify Services Are Running**
   - Check Aspire dashboard for green status on all resources
   - Console logs appear in terminal
   - Structured logs available via dashboard → Logs
   - Metrics available via dashboard → Metrics

## Accessing Logs & Metrics

Aspire dashboard provides:
- **Console**: Real-time stdout/stderr from all services
- **Structured Logs**: Filterable logs with correlation IDs
- **Traces**: Distributed tracing across services
- **Metrics**: CPU, memory, HTTP request metrics

## Stopping the Solution

// turbo
4. **Stop Aspire** (when done)
   ```bash
   # Find and kill the Aspire process
   pkill -f "aspire run" || true
   ```

## Related Skills

**Prerequisites**:
- `/ops__doctor_check` - Verify environment (Docker, .NET, Aspire CLI)

**Next Steps**:
- `/aspire__setup_mcp` - Enable MCP access to logs/metrics
- `/test__integration_suite` - Run tests against running instance

**Debugging**:
- `/frontend__debug_sse` - If real-time updates not working
- `/cache__debug_cache` - If caching issues

**See Also**:
- `docs/guides/aspire-guide.md`
- `src/BookStore.AppHost/AGENTS.md`
