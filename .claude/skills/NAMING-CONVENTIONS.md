# Claude Skill Naming Instructions

## Purpose
These conventions keep the `.claude/skills` catalog searchable by technology domain. Prefixes capture the stack area first (meta work, language references, Marten artifacts, etc.), then the slug describes the concrete workflow. Follow these instructions whenever you add or rename a skill so that Claude skill best practices (frontmatter, numbered steps, local templates) remain discoverable.

## Naming Format
1. **Shape**: `prefix__slug`. The double underscore clearly separates taxonomy from action.
2. **Slug style**: lower snake case (e.g., `add_projection`, `create_skill`). Avoid hyphens—they break slash-command completion.
3. **Canonical ID**: The directory name, the `name:` field in `SKILL.md`, the slash command shown in `AGENTS.md`, and any cross-links must match exactly.
4. **Frontmatter alignment**: Update `aliases:` when you rename a skill so legacy commands (e.g., `/scaffold-aggregate`) still resolve while teams transition.

## Approved Prefixes
Use the prefix that matches the dominant technology or artifact the skill targets. Derive new prefixes only when a new stack area emerges and at least two skills will share it.

| Prefix | Stack Focus | Use For | Example Rename |
| --- | --- | --- | --- |
| `meta__` | Skill/AGENTS.md authoring, catalogs, governance | Maintaining the cheat sheet, creating new skills, reviewing naming lint tools | `cheat-sheet → meta__cheat_sheet`, `scaffold-skill → meta__create_skill`
| `lang__` | Language or framework primers (C#, Razor, TUnit, HybridCache APIs) | Syntax refreshers, analyzer checklists, code-style codification | `write-documentation-guide → lang__docfx_guide`
| `marten__` | Event sourcing, projections, document storage | Aggregate scaffolding, single/multi-stream projections, Marten troubleshooting | `scaffold-single-stream-projection → marten__single_stream_projection`, `scaffold-aggregate → marten__aggregate_scaffold`
| `wolverine__` | Wolverine messaging, command handlers, Wolverine diagnostics | Handler templates, Wolverine-specific debugging | (future) `wolverine__handler_scaffold`
| `aspire__` | Aspire orchestration, AppHost utilities, MCP integration | Starting the solution, setup scripts, pipeline hooks | `start-solution → aspire__start_solution`, `setup-aspire-mcp → aspire__setup_mcp`
| `cache__` | HybridCache + Redis tuning | Cache debugging, invalidation playbooks | `debug-cache → cache__troubleshoot`
| `frontend__` | Blazor features, ReactiveQuery patterns, SSE UX | UI scaffolding, optimistic update flows | `scaffold-frontend-feature → frontend__feature_scaffold`
| `ops__` | Local runbooks unrelated to a single stack (doctor, rebuild) | Environment health checks, clean rebuilds | `doctor → ops__doctor_check`, `rebuild-clean → ops__rebuild_clean`
| `deploy__` | Azure/Kubernetes shipping plus rollbacks | `aspire run` deployment helpers, rollback procedures | `deploy-to-azure → deploy__azure_container_apps`, `deploy-kubernetes → deploy__kubernetes_cluster`, `rollback-deployment → deploy__rollback`
| `test__` | Unit/integration verification, feature validation | Running suites, verify-feature workflows | `run-unit-tests → test__unit_suite`, `verify-feature → test__verify_feature`
| `doc__` | Documentation production aimed at end users | Publishing architecture notes, guides, onboarding docs | `write-end-user-guide → doc__end_user_guide`

> Tech-stack mapping example: `scaffold-event-projection` touches Marten, so the new name becomes `marten__event_projection`. `run-integration-tests` touches Aspire-hosted test harnesses more than Marten, so prefer `test__integration_suite`.

## How to Name a New Skill
1. **Identify the dominant stack** the instructions rely on. If it is Marten, you should land under `marten__`. If it primarily ensures AGENTS.md hygiene, use `meta__`.
2. **Describe the outcome** using a short slug. Prefer `verb_object` or `object_action` depending on clarity (e.g., `marten__aggregate_scaffold`, `aspire__start_solution`).
3. **Populate `SKILL.md`** with the new name in YAML frontmatter, keep the description action-oriented, list prerequisites, and document numbered steps per Claude skill guidelines.
4. **Update references**: `AGENTS.md`, `.claude/skills/README.md`, docs, and any slash-command cheat sheets must reflect the new name immediately.
5. **Add aliases**: Under `aliases:` include any retired names (`scaffold-aggregate`, `run-integration-tests`) until teams finish migrating their slash-command muscle memory.

## Renaming Existing Skills
1. Rename the folder to the new `prefix__slug`.
2. Adjust `name:` and `aliases:` inside `SKILL.md` and fix any relative template paths.
3. Search the repo for the previous command (e.g., `/scaffold-aggregate`) and update every reference.
4. Mention the prefix rationale in the PR description so reviewers can confirm the stack alignment.
5. Remove the alias once telemetry (or team confirmation) shows nobody uses the legacy command.

## Quality Gate
- Skills that do not match `^(meta|lang|marten|wolverine|aspire|cache|frontend|ops|deploy|test|doc)__[a-z0-9_]+$` should fail CI or reviewer checks.
- Any new prefix proposal must cite at least two upcoming skills and explain why existing prefixes cannot express the domain.
- Keep instructions self-contained, with numbered procedures, prerequisites, and clear success criteria to stay in line with Claude skill best practices.

Following these instructions keeps the catalog predictable: the prefix tells contributors which slice of the BookStore stack they are touching, and the slug tells them exactly what the workflow accomplishes.
