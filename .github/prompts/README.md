# Prompts directory — BookStore

Purpose

- Store reusable prompt templates and a compact, canonical system prompt derived from `/.github/copilot-instructions.md`.

Files

- [system_copilot_instructions.md](system_copilot_instructions.md): Compact system prompt summarizing repository rules.

How to use

- Use `system_copilot_instructions.md` as the system-level prompt when invoking an assistant for repo tasks.
- Keep task prompts concise and reference the canonical instructions for policy or style details.

Examples

- Task prompt: "Implement a small C# command handler following repository rules; add XML comments and an integration test in `BookStore.AppHost.Tests`."

Contributing

- If repository rules change, update `/.github/copilot-instructions.md` and refresh this compact summary and examples.

Templates

- See [templates.md](templates.md) for task templates, few-shot examples, and validation prompts.

Project-specific instructions

- `api-service.md`: rules and conventions for domain, aggregates, commands, and handlers.
- `client.md`: guidance for API clients and SDKs (DTOs, serialization, http client usage).
- `web.md`: frontend/web app rules (accessibility, responsiveness, localization).
