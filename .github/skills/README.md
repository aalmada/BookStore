# Skills — BookStore agent capabilities

Purpose

- Describe concise, repository-relevant capabilities (skills) that an assistant may use when working in this repo.

Skills (examples)

- `file-editing`: create, update, and format C# source files following repository conventions.
- `test-run`: run unit/integration tests and report failures (CI invocation or local `dotnet test`).
- `search-repo`: find files, symbols, and usages to inform changes.
- `scaffold-test`: add minimal integration tests to `BookStore.AppHost.Tests` following naming and assertion rules.

Usage

- Keep skill descriptions short and include one-line examples of input/output shapes.
- When writing task prompts, reference a skill when the task relies on specific capabilities (for example: "Use `test-run` to validate the new handler").

Maintainers

- Keep this file updated when adding new agent capabilities or tool integrations.
