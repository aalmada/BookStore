# GitHub Copilot Instructions

This file provides repository-level instructions for GitHub Copilot.

For detailed agent instructions, see [AGENTS.md](../AGENTS.md).

For skills, see [.claude/skills/](../.claude/skills/).

## Quick Rules

- **Stack**: .NET 10, C# 14, Marten, Wolverine, HybridCache, Aspire
- **TUnit** for tests (not xUnit/NUnit)
- Use `record` types for DTOs/Commands/Events
- Events in past tense (`BookAdded`, not `AddBook`)
- Use `Guid.CreateVersion7()` and `DateTimeOffset.UtcNow`
- File-scoped namespaces: `namespace BookStore.X;`
- See `docs/guides/analyzer-rules.md` for BS1xxx-BS4xxx rules

## Key Rules (MUST follow)
```
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ record BookAdded(...)          ❌ record AddBook(...)
✅ namespace BookStore.X;         ❌ namespace BookStore.X { }
✅ [Test] async Task (TUnit)      ❌ [Fact] (xUnit)
```

## Common Mistakes
- ❌ Business logic in endpoints → Put in aggregates/handlers
- ❌ Forgetting SSE notification → Add to `MartenCommitListener`
- ❌ Missing cache invalidation → Call `RemoveByTagAsync` after mutations
- ❌ Using xUnit/NUnit → Use TUnit with `await Assert.That(...)`
