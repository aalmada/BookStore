---
name: wolverine
description: |
  Use this skill for any request involving Wolverine — the .NET command/handler and messaging framework. Trigger for:
  - Implementing, updating, or testing command/handler patterns
  - Message bus, async messaging, or background jobs
  - Transaction management, optimistic concurrency (e.g., ETags), or handler conventions
  - Migrating endpoints to Wolverine, troubleshooting handler discovery, or integrating with Marten
  - Any .NET project using WolverineFx libraries (project-agnostic)
  Always use this skill when the user mentions Wolverine, mediator, message bus, async jobs, or handler patterns — even if not named explicitly.

Note: ETags and optimistic concurrency are recommended best practices for update/delete operations, but are not required by Wolverine itself.
---

# Wolverine Skill

This skill enables robust command/handler and messaging patterns in .NET using Wolverine. It is project-agnostic and references modular guides for clarity and maintainability.

## Reference Structure

- [wolverine-basics.md](references/wolverine-basics.md): Core concepts, command/handler pattern, handler discovery
- [wolverine-advanced.md](references/wolverine-advanced.md): Async messaging, cascading messages, background jobs, advanced configuration
- [wolverine-marten.md](references/wolverine-marten.md): Marten integration, event sourcing, projections
- [wolverine-etag.md](references/wolverine-etag.md): ETag/optimistic concurrency, update/delete patterns
- [wolverine-testing.md](references/wolverine-testing.md): Handler testing, troubleshooting, best practices

## Usage

- For basic command/handler usage, see [wolverine-basics.md](references/wolverine-basics.md)
- For async messaging, background jobs, or advanced scenarios, see [wolverine-advanced.md](references/wolverine-advanced.md)
- For Marten integration and event sourcing, see [wolverine-marten.md](references/wolverine-marten.md)
  - For optimistic concurrency (e.g., ETags), see [wolverine-etag.md](references/wolverine-etag.md)
- For testing and troubleshooting, see [wolverine-testing.md](references/wolverine-testing.md)

## Patterns

- Use immutable `record` types for commands
- Handlers must be `public static` methods named `Handle`
- Endpoints should be thin, routing to `IMessageBus.InvokeAsync()`
- Use ETags or other optimistic concurrency for update/delete operations (recommended, not required)
- Let Wolverine manage transactions (no manual `SaveChangesAsync()`)
- Write unit tests for handlers as pure functions

## See Also
- [Official Wolverine Docs](https://wolverinefx.net/introduction/what-is-wolverine.html)
- [Critter Stack Example](https://jeremydmiller.com/2026/02/02/building-a-greenfield-system-with-the-critter-stack/)
- [Debugging Wolverine](https://jeremydmiller.com/2026/02/24/on-debugging-problems/)
- [Validation in Wolverine](https://jeremydmiller.com/2026/03/15/validation-options-in-wolverine/)
- [BookStore Wolverine Guide](../../../../docs/guides/wolverine-guide.md)

---

For details, read the referenced guides in the `references/` folder. Each guide is modular and can be updated independently as Wolverine evolves.
