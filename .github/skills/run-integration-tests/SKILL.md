---
name: Run Integration Tests
description: Run the full suite of integration tests for the BookStore application. Use this to verify system-wide behavior.
---

To verify the application's end-to-end functionality using the Aspire AppHost tests:

1. **Navigate to Test Project**
   Go to the `tests/BookStore.AppHost.Tests` directory.

// turbo
2. **Execute Tests**
   Run `dotnet test` to execute the full integration suite.

3. **Analyze Results**
   - **Pass**: Confirm to the user that all integration scenarios are working.
   - **Fail**: Look at the failed test names and stack traces.
     - Use `view_file` to read the failing test source code.
     - Check the test logs (often captured in `TestResults` or standard output) for specific error details like assertion failures or timeouts.
