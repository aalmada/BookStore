---
name: verify-feature
description: Strict verification for new features. Runs compilation, formatting, and all tests to ensure the "Definition of Done".
---

Use this skill to verify that a feature implementation is complete and correct. It enforces code style, compilation, and test passing.

## Verification Steps

1. **Compilation**
   - Run `dotnet build` in the root directory.
   - **Check**: If the build fails, STOP. Report the errors using `read_file` to show the relevant code.
   - **Tip**: For clean rebuild, use `/rebuild-clean`

2. **Code Formatting**
   - Run `dotnet format --verify-no-changes`.
   - **Check**: If this fails, it means the code violates style rules.
   - *Action*: You can offer to fix it for the user by running `dotnet format` (without --verify-no-changes).

3. **Background Services Check**
   - Ensure you are not running tests if the AppHost is already locking ports (if running locally).
   - *Action*: Stop any running `dotnet` processes if needed.

// turbo
4. **Run Tests (Unit & Integration)**
   - Run `dotnet test` to execute all test suites.
   - **Check**: All tests must pass.
   - *Action*: If tests fail, analyze the results. Focus on any NEW failures related to the recent changes.
   - **Granular Options**:
     - `/run-unit-tests` - Unit tests only
     - `/run-integration-tests` - Integration tests only

5. **Completion**
   - If all steps pass, report: "✅ Feature verified: Builds, follows style guide, and passes all tests."

## Related Skills

**Typically Used After**:
- `/scaffold-write`, `/scaffold-read`, `/scaffold-frontend-feature` - After implementing features
- `/scaffold-test` - After creating tests
- `/debug-sse`, `/debug-cache` - After fixing issues

**Component Skills** (for granular verification):
- `/rebuild-clean` - Clean build if compilation issues
- `/run-unit-tests` - Run only unit tests
- `/run-integration-tests` - Run only integration tests

**See Also**:
- [run-unit-tests](../run-unit-tests/SKILL.md) - Unit test details
- [run-integration-tests](../run-integration-tests/SKILL.md) - Integration test details
- [rebuild-clean](../rebuild-clean/SKILL.md) - Clean build process
- [testing-guide](../../../docs/guides/testing-guide.md) - TUnit testing patterns
- [integration-testing-guide](../../../docs/guides/integration-testing-guide.md) - Aspire testing
