# Quickstart

Use this flow when you need working hooks quickly.

## 1. Choose where hooks live

1. Repository-level JSON hooks: use `.github/hooks/*.json`.
2. Agent-scoped hooks: add a `hooks:` block in `.github/agents/<name>.agent.md` frontmatter.
3. Mixed model: keep org or repo guardrails in JSON, agent-specific behavior in frontmatter.

## 2. Start with low-risk events

1. `SessionStart` or `UserPromptSubmitted` for logging/context.
2. `PostToolUse` for metrics/audit.
3. Add `PreToolUse` only after payload parsing is tested.

## 3. Keep scripts predictable

1. Read JSON from stdin.
2. Parse safely.
3. Use one `command` entry.
4. Prefer `python3` for cross-platform script execution.
5. Return no output for allow/ignore path.
6. Return decision JSON only when intentionally blocking.

## 4. Add a safety timeout

Set `timeout` (or legacy `timeoutSec` where required) so hooks fail fast.

## 5. Test sequence

1. Trigger an event with harmless input.
2. Verify script can parse payload.
3. Verify deny behavior only for matching conditions.
4. Verify user-facing reason text is actionable.
