# Memory Conventions

All squads scaffolded by the squad-creator skill follow these memory-file conventions.
Consistent paths let agents find each other's output without hard-coded coordination.

---

## File Layout

```
/memories/
└── session/
    ├── task-brief.md       ← Orchestrator writes before invoking any agent
    ├── plan.md             ← Planner writes after codebase research
    ├── status.md           ← All agents append progress lines (shared log)
    ├── <agent>-output.md   ← Each specialist writes its own output file
    └── review.md           ← CodeReviewer writes findings
```

### Naming convention for output files

Use the agent's display name, lowercased, with hyphens:
- `BackendDeveloper` → `backend-developer-output.md`
- `FrontendDeveloper` → `frontend-developer-output.md`
- `TestEngineer` → `test-output.md`
- `DataEngineer` → `data-engineer-output.md`

---

## Status Log Format (`status.md`)

The status log is **append-only**. Every agent appends a line when it starts and
when it finishes (or hits a blocker). The Orchestrator reads it to report progress
to the user.

### Status line format

```
⏳ <AgentName> — started — <brief task description>
✅ <AgentName> — done — <one sentence summary of output>
🚫 <AgentName> — blocked — <reason>
```

### Example status.md

```
⏳ Orchestrator — started — coordinating: add currency conversion feature
⏳ Planner — started — planning: add currency conversion feature
✅ Planner — done — plan written to /memories/session/plan.md
⏳ BackendDeveloper — started — implementing: CurrencyConverter service
⏳ FrontendDeveloper — started — implementing: currency selector UI
✅ BackendDeveloper — done — CurrencyConverter service and API endpoint created
✅ FrontendDeveloper — done — currency selector component added to checkout page
⏳ TestEngineer — started — writing tests
✅ TestEngineer — done — 12 tests written, all passing
⏳ CodeReviewer — started — reviewing changes
✅ CodeReviewer — done — PASS: 2 minor suggestions
✅ Orchestrator — done — all agents finished
```

---

## Task Brief Format (`task-brief.md`)

The Orchestrator writes this before invoking any agent. Structure:

```markdown
## Task Summary
<1–2 sentences describing exactly what must be delivered>

## Scope
<what is in scope / out of scope>

## Agents Required
- Planner (always)
- <SpecialistA> (reason)
- <SpecialistB> (reason)
- CodeReviewer (always)

## Key Constraints
<project-specific coding rules or conventions that apply to this task>

## Open Questions / Answers
<clarifications from the user, or "none">
```

---

## Plan Format (`plan.md`)

The Planner writes this after researching the codebase. Structure:

```markdown
## Task Summary
## Files to Create / Modify
<full paths, one per line>

## Implementation Steps
### <Step group name (e.g., "Backend", "Frontend", "Database")>
- [ ] <Concrete action with file path and what to write>

## Test Steps
- [ ] <Test scenario with file and expected behaviour>

## Open Questions / Blockers
<Any unresolved decisions or missing prerequisites>
```

---

## Output File Format (`<agent>-output.md`)

Each specialist writes this when it finishes. Minimum required sections:

```markdown
## Summary
<One paragraph: what was done>

## Files Created / Modified
- `<full path>` — <one-line description of change>

## Testing Required
- <Scenario the TestEngineer should cover>

## Deviations from Plan
<Any divergences from plan.md, with reasons — or "none">
```

---

## Review Format (`review.md`)

The CodeReviewer writes this after examining all changes.

```markdown
## Review Summary
PASS ✅ | NEEDS FIXES ⚠️ | FAIL ❌

## Findings
### <Finding title> — Critical | Major | Minor | Suggestion
- **File**: `<path#line>`
- **Issue**: <description>
- **Suggested fix**: <what to change>
```

Return **PASS** when there are no Critical or Major findings.
Return **NEEDS FIXES** when there are Major but no Critical findings.
Return **FAIL** when there are Critical findings.

---

## Parallel Agent Pattern

When two specialists are independent (read the same plan, write to different output files),
the Orchestrator invokes them in the same turn. In the Orchestrator body, annotate:

```markdown
Invoke BackendDeveloper and FrontendDeveloper **in the same turn** — they both read
`/memories/session/plan.md` and write to separate output files, so they are independent.
After both complete, read their output files before proceeding to the TestEngineer.
```

This is the standard parallel pattern for full-stack squads where backend and frontend
changes do not depend on each other within a single feature.
