---
name: rebuild-clean
description: Clean and rebuild the solution to ensure a fresh state. Use this when the user faces transient build errors or stale artifacts.
license: MIT
---

To ensure a clean build state and resolve transient compilation issues, follow this process:

1. **Stop Background Processes**
   - Ensure no background `dotnet` processes are running that might lock files.
   - Check for running Aspire instances or test hosts.

2. **Clean Solution**
   - Run `dotnet clean` in the root directory.
   - This removes all intermediate output and binaries.

// turbo
3. **Rebuild**
   - Run `dotnet build` in the root directory to compile the entire solution from scratch.

4. **Verify and Report**
   - Check the output for any persistent errors.
   - If the build succeeds, notify the user: "✅ Workspace is clean and builds successfully."
   - If errors persist, they are likely genuine code issues rather than stale artifacts.
   - Use `grep_search` or `read_file` to investigate the specific error messages.

## When to Use

- Build errors that seem inconsistent
- After major dependency changes
- When switching branches with significant changes
- Stale artifact issues
- After package version updates

## Related Skills

**Used By**:
- `/verify-feature` - May reference this for clean builds

**Next Steps**:
- `/verify-feature` - Full verification after clean rebuild
- `/run-unit-tests` or `/run-integration-tests` - Test the rebuilt solution

**See Also**:
- [getting-started](../../../docs/getting-started.md) - Build instructions
- [aspire-guide](../../../docs/guides/aspire-guide.md) - Running the application
