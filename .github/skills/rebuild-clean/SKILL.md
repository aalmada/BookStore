---
name: Rebuild Clean
description: Clean and rebuild the solution to ensure a fresh state. Use this when the user faces transient build errors or stale artifacts.
---

To ensure a clean build state and resolve transient compilation issues, follow this process:

1. **Stop Background Processes**
   Ensure no background `dotnet` processes are running that might lock files.

2. **Clean Solution**
   Run `dotnet clean` in the root directory. This removes all intermediate output and binaries.

// turbo
3. **Build Solution**
   Run `dotnet build` in the root directory to compile the entire solution from scratch.

4. **Verify and Report**
   Check the output for any persistent errors.
   - If the build succeeds, notify the user that the workspace is clean.
   - If errors persist, they are likely genuine code issues rather than stale artifacts. Use `grep_search` or `view_file` to investigate the specific error messages.
