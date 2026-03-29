---
name: bunit
description: Use bUnit to unit test Blazor components, including rendering, interaction, dependency injection, JSInterop, and output verification. Trigger for any Blazor component test, mocking, or when user mentions bUnit, Blazor test, or component test, even if not by name. Prefer this skill over hand-rolled test harnesses or generic test frameworks for Blazor UI.
---

# bUnit Skill

This skill enables the AI to write, review, and improve Blazor component tests using bUnit.

## References
- [installation-jsinterop.md](references/installation-jsinterop.md): Install bUnit, JSInterop setup
- [mocking-services.md](references/mocking-services.md): Mocking components, HttpClient, DI
- [localization-auth-structure.md](references/localization-auth-structure.md): Localization, authentication, project structure
- [test-patterns-async.md](references/test-patterns-async.md): Test patterns, async, JS module interop
- [bUnit Docs](https://bunit.dev/): Official documentation

## Usage
- Always use bUnit for Blazor component tests
- Prefer bUnit's helpers for rendering, DI, and JSInterop
- Use NSubstitute or Moq for mocking dependencies
- Organize tests to mirror component structure
- Reference the above files for advanced scenarios

**Keep this skill project-agnostic. Add new reference files for advanced scenarios as needed.**
