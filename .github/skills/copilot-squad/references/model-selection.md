# Model Selection — Squad Role Mapping

This file maps each standard squad role to its recommended Copilot model, with a brief
rationale. For full model profiles, multipliers, context windows, fallback chain syntax,
and cost optimisation tips, read
`copilot-custom-agent/references/model-selection.md`.


---

## Recommended Model by Agent Role

| Role | Recommended model | Rationale |
|---|---|---|
| **Orchestrator** | `Claude Sonnet 4.6 (copilot)` | Needs precise instruction-following, long-context reading of status logs, nuanced clarification |
| **Planner** | `Claude Sonnet 4.6 (copilot)` | Synthesises large codebase explorations into a structured plan; benefits from long context and deep comprehension |
| **BackendDeveloper** | `GPT-5.3-Codex (copilot)` | Writes idiomatic production code — Codex tuning is the decisive advantage here |
| **FrontendDeveloper** | `GPT-5.3-Codex (copilot)` | Same as backend — code generation quality matters most |
| **TestEngineer** | `GPT-5.4 (copilot)` | Strong analytical reasoning finds edge cases the implementation missed; 1x cost makes it the obvious choice over Codex variants |
| **CodeReviewer** | `GPT-5.4 (copilot)` | Best analytical quality at standard 1x cost — no reason to downgrade for review tasks |
| **SecurityReviewer** | `GPT-5.4 (copilot)` | OWASP analysis requires strong reasoning; Codex variants miss subtle security issues; same 1x cost |
| **UiUxDesigner** | `Gemini 3.1 Pro (Preview) (copilot)` | Vision capability for design reference images; 200K context for reading full component trees; 1x cost |
| **DatabaseEngineer** | `GPT-5.3-Codex (copilot)` | Schema and query generation benefits from Codex tuning |
| **DataEngineer** | `GPT-5.3-Codex (copilot)` | Data transformation and pipeline code |
| **DocumentationWriter** | `Claude Sonnet 4.6 (copilot)` | Prose quality and instruction-following produce better docs than code-tuned models |
| **InfraEngineer** | `GPT-5.3-Codex (copilot)` | IaC (Bicep, Terraform, YAML) is code — Codex tuning helps |
| **Explore (sub-agent)** | `GPT-5 mini (copilot)` | 0x free, 192K context, good quality — GPT-4o is an equally valid 0x alternative |


---

For fallback chain syntax, cost optimisation tips, and full model profiles, read
`copilot-custom-agent/references/model-selection.md`.
