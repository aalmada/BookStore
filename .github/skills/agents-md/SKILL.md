---
name: agents-md
description: >
  Use this skill for any request to create, update, review, or improve files that guide AI coding
  tools—like AGENTS.md, Copilot context files, or agent instructions. Trigger when users want to
  help AI generate code that matches project conventions, avoids common mistakes, or understands
  non-obvious rules—whether for a whole repo, a subdirectory, or a specific component. Also use
  for queries about setting up Copilot or Cursor context, onboarding AI to team practices, or
  keeping agent guidance up to date—even if AGENTS.md isn't mentioned by name.
---

# AGENTS.md Skill

`AGENTS.md` is the README for AI coding agents — it gives tools like GitHub Copilot, Cursor,
and Claude Code the context they need to produce code that fits your project, without
constant back-and-forth correction.

## The Golden Rule: Only Include Non-Obvious Things

This is the most important principle. Before adding any line, ask:
**"Could an agent figure this out by reading the code or config files?"**

If yes — skip it. Agents can read `package.json`, `pyproject.toml`, `*.csproj`, directory
structures, imports, and existing code. AGENTS.md is for the things that *aren't* visible there:

- Project-specific conventions not enforced by any linter or analyzer
- "Never do X" patterns that *look* reasonable but are wrong in this codebase
- Commands that are non-standard or require project-specific flags
- Domain terminology or architectural choices that need explanation
- Common mistakes agents actually make here (discovered from experience)
- Security constraints and rules that must never be violated

**Redundant content actively degrades quality** — it wastes the agent's context window and
dilutes real signal with noise it already has.

## Two Shapes of AGENTS.md

### Root-level `AGENTS.md` (repo root)

Covers the whole project: stack, layout, cross-cutting rules, major patterns, and a doc index.
Aim for ≤ 150 lines. Link to existing guides rather than duplicating them.

### Component-level `AGENTS.md` (subdirectory)

Scoped to one module, package, or concern (e.g., `src/Api/`, `tests/`).
Create one when:
- The component has non-obvious rules not covered by the root file
- It has its own commands, stack, or lifecycle
- Agents working in that folder would benefit from a tightly-focused cheat sheet

**Don't create a component AGENTS.md just for completeness.** An empty or trivially short
file adds nothing — it just adds noise to the file tree.

---

## Root-Level Structure

```markdown
# <Project Name> — Agent Instructions

## Quick Reference
- **Stack**: List of key technologies
- **Solution/Entry point**: Main file or solution
- **Common commands**: `build`, `test`, `run`, `format` — exact, copy-paste ready
- **Docs**: Links to key guides

## Repository Map
Folder → one-line purpose. Skip folders whose purpose is obvious from the name.

## Major Patterns
Bullet list of architectural decisions agents must know:
- Why a pattern is used (not just that it's used)
- Any non-obvious integration approaches

## Code Rules (MUST follow)
✅/❌ table — see format below.

## Common Mistakes
Concrete anti-patterns agents get wrong in this codebase. Not generic advice.

## Quick Troubleshooting
"Symptom → fix" pairs for problems agents are likely to hit.
```

## Component-Level Structure

```markdown
# <Component Name> — Agent Instructions

## Quick Reference
- **Scope**: `path/to/component/**`
- **Stack**: Techs specific to this component (only if different from root)
- **Commands**: Commands specific to this component

## Key Rules (MUST follow)
✅/❌ table — see format below.

## Common Mistakes
What agents get wrong specifically inside this component.

## Project Layout
Table: path → purpose. Skip obvious paths.

## Quick Troubleshooting
Symptom → fix pairs.

## Documentation Index
Table: topic → guide path. Don't repeat links from root AGENTS.md unless critical.
```

---

## The ✅/❌ Rule Format

Rules work best as a short code block with ✅ for the right pattern and ❌ for the wrong one,
side-by-side. This format is instantly scannable and makes the do/don't unambiguous:

```
✅ Guid.CreateVersion7()          ❌ Guid.NewGuid()
✅ DateTimeOffset.UtcNow          ❌ DateTime.Now
✅ record EventPastTense(...)     ❌ record VerbCommand(...)
✅ [LoggerMessage(...)]           ❌ _logger.LogInformation(...)
```

Rules that belong here:
- API/library choices where the wrong option compiles fine but violates project policy
- Naming conventions that linters don't enforce
- Framework-level gotchas specific to this codebase

Rules to skip:
- Things already caught by error-producing analyzers (the build will stop them anyway)
- Generic best practices any senior dev knows ("don't hardcode secrets", "use async")
- Anything an agent would infer from reading existing code

---

## Content Priority Filter

Run every candidate line through this filter before including it:

| Question | If YES | If NO |
|----------|--------|-------|
| Would an agent deduce this from reading the files? | Skip | Include |
| Does a linter or analyzer already enforce this? | Skip | Include |
| Is this generic advice true of any codebase? | Skip | Include |
| Would an agent without this make a real mistake here? | Include | Skip |
| Is this specific to this codebase's conventions? | Include | Skip |

---

## Single Source of Truth

Don't duplicate content from README, wikis, or existing docs. Link to them:

```markdown
## Documentation Index
| Topic | Guide |
|-------|-------|
| Architecture | `docs/architecture.md` |
| Testing patterns | `docs/guides/testing-guide.md` |
```

This keeps AGENTS.md focused and avoids stale duplication. If two files say the same thing
and one gets updated, they immediately diverge.

---

## Treat It as Living Code

Update AGENTS.md in the **same PR** as the change it documents. If tests now require a
specific flag, a linter is added, or a pattern changes — the AGENTS.md update belongs in
that diff. Reviewers should check AGENTS.md freshness as part of code review.

---

## Checklist Before Saving

- [ ] Every ✅/❌ rule is non-obvious (an agent wouldn't know it without being told)
- [ ] No content duplicated from README or other docs — linked instead
- [ ] All commands are copy-paste ready, wrapped in backticks
- [ ] Root AGENTS.md ≤ 150 lines; component AGENTS.md ≤ 100 lines
- [ ] No generic advice ("write clean code", "handle errors")
- [ ] Each "Common Mistake" names a specific anti-pattern, not a vague category
- [ ] Every section passes the Content Priority Filter
