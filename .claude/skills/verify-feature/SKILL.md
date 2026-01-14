---
name: Verify Feature
description: Strict verification for new features. Runs compilation, formatting, and all tests to ensure the "Definition of Done".
---

Use this skill to verify that a feature implementation is complete and correct. It enforces code style, compilation, and test passing.

1. **Compilation**
   - Run `dotnet build` in the root directory.
   - **Check**: If the build fails, STOP. Report the errors using `view_file` to show the relevant code.

2. **Code Formatting**
   - Run `dotnet format --verify-no-changes`.
   - **Check**: If this fails, it means the code violates style rules.
   - *Action*: You can offer to fix it for the user by running `dotnet format` (without --verify-no-changes).

3. **Background Services Check**
   - Ensure you are not running tests if the AppHost is already locking ports (if running locally).
   - *Action*: Stop any running `dotnet` processes if needed.

// turbo
4. **Run Tests (Unit & Integration)**
   - Run `dotnet test`.
   - **Check**: All tests must pass.
   - *Action*: If tests fail, analyze the results. Focus on any NEW failures related to the recent changes.

5. **Completion**
   - If all steps pass, report: "✅ Feature verified: Builds, follows style guide, and passes all tests."
