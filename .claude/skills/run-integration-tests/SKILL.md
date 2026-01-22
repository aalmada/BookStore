---
name: run-integration-tests
description: Run the full suite of integration tests for the BookStore application. Use this to verify system-wide behavior.
license: MIT
---

To verify the application's end-to-end functionality using the Aspire AppHost tests:

1. **Navigate** to the test project:
   ```bash
   cd tests/BookStore.AppHost.Tests
   ```

// turbo
2. **Run Integration Tests**
   Run `dotnet test` to execute the full integration suite.

3. **Analyze Results**
   - **Pass**: Confirm to the user that all integration scenarios are working.
   - **Fail**: Look at the failed test names and stack traces.
     - Use `read_file` to read the failing test source code.
     - Check the test logs (often captured in `TestResults` or standard output) for specific error details like assertion failures or timeouts.

## Related Skills

**Used By**:
- `/verify-feature` - Runs all tests including integration tests
- `/scaffold-test` - Creates integration tests that this skill runs

**Related**:
- `/run-unit-tests` - Run unit tests only
- `/verify-feature` - Complete verification workflow

**See Also**:
- [integration-testing-guide](../../../docs/guides/integration-testing-guide.md) - Aspire testing details
- [scaffold-test](../scaffold-test/SKILL.md) - Creating integration tests
- AppHost.Tests AGENTS.md - Integration test patterns
