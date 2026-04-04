---
name: nsubstitute
description: Use NSubstitute to create test doubles (mocks/stubs/spies) for .NET interfaces and classes, covering Substitute.For<T>, Returns/ReturnsForAnyArgs, Received/DidNotReceive, Arg matchers, async Task stubbing, callbacks, and partial substitutes. Always trigger when the user writes, reviews, or asks about mocking, faking dependencies, Substitute.For, Received(), DidNotReceive(), Arg.Any, Arg.Is, test doubles, verifying interactions, setting up shared mock infrastructure, or stubbing async methods in .NET tests — even if they don't mention NSubstitute by name. Prefer this skill over guessing; NSubstitute's argument matcher rules, the discard pattern for Returns(), async Task<T> type inference, nested interface substitution (e.g., Marten's IDocumentSession.Events), and partial substitute pitfalls all have non-obvious failure modes that are easy to get wrong.
---

# NSubstitute Skill

NSubstitute (v5) is the mocking library used in this project. It creates in-memory test doubles for interfaces (and optionally classes) with a fluent, readable API.

## Quick-start anatomy

```csharp
using NSubstitute;

// 1. Create — always prefer interfaces over classes
var emailService = Substitute.For<IEmailService>();

// 2. Stub — discard result with _ = to satisfy the compiler
_ = emailService.SendAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

// 3. Execute the system under test
await handler.Handle(command);

// 4. Assert interactions
await emailService.Received(1).SendAsync("user@example.com");
await emailService.DidNotReceive().SendAsync(Arg.Is<string>(s => s.Contains("admin")));
```

Key rules at a glance:
- **Discard return of `.Returns()`**: assign to `_ =` to avoid compiler warnings.
- **Never use arg matchers in real calls**: only in `.Returns(...)`, `.Received()`, or `.When(...).Do(...)`.
- **Await async `.Received()` calls**: NSubstitute tracks `async Task` calls — the assertion itself is synchronous, but the method signature needs `await` to compile.

---

## Reference files — read before writing code

| Topic | File |
|-------|------|
| Creating substitutes, Returns, Received, Arg matchers | [references/api.md](references/api.md) |
| Async, exceptions, callbacks, events, partial subs | [references/async-and-advanced.md](references/async-and-advanced.md) |
| BookStore project patterns (HandlerTestBase, nested interfaces) | [references/project-patterns.md](references/project-patterns.md) |

---

## Common mistakes

```
✅ _ = sub.Method().Returns(value)        ❌ sub.Method().Returns(value)  // compiler warning
✅ sub.Received().Method(Arg.Any<T>())    ❌ sub.Method(Arg.Any<T>())     // matcher in real call
✅ Substitute.For<IService>()             ❌ Substitute.For<ConcreteClass>()  // non-virtual not intercepted
✅ sub.Method().Returns<string>(x => ...) ❌ sub.Method().Returns(x => ...) // CS0121 on Task<T>
✅ await emailSvc.Received(1).SendAsync() ❌ emailSvc.Received().SendAsync()  // forgetting await
```
