# Model Selection Reference

This reference covers all GitHub Copilot-provided models: their IDs, cost multipliers,
context windows, strengths and limitations, fallback chain syntax, and cost optimisation
tips. Use it whenever you need to choose or set the `model:` frontmatter field on any
`.agent.md` file.

> For **role-to-model recommendations** when building a squad, see
> `copilot-squad/references/model-selection.md`.

---

## How Copilot Charges

Copilot bills by *premium requests*, not token volume. Each agent invocation consumes
a number of premium requests equal to the model's multiplier × the number of requests
made.

Active multiplier tiers: **0x** (free), **0.25x**, **0.33x** (reduced), **1x** (standard),
**3x**, **30x** (extreme — avoid in automated agents).

> Always verify current multipliers on the Copilot pricing/settings page — they can
> change when models graduate from preview to GA.

---

## Models Available via GitHub Copilot

| Model ID (frontmatter value) | Family | Multiplier | Context window |
|---|---|---|---|
| `Claude Haiku 4.5 (copilot)` | Anthropic | 0.33x | 200K |
| `Claude Sonnet 4 (copilot)` | Anthropic | 1x | 144K |
| `Claude Sonnet 4.5 (copilot)` | Anthropic | 1x | 200K |
| `Claude Sonnet 4.6 (copilot)` | Anthropic | 1x | 200K |
| `Claude Opus 4.5 (copilot)` | Anthropic | 3x | 200K |
| `Claude Opus 4.6 (copilot)` | Anthropic | 3x | 200K |
| `Claude Opus 4.6 (fast mode) (Preview) (copilot)` | Anthropic | **30x** | 192K |
| `Gemini 2.5 Pro (copilot)` | Google | 1x | 173K |
| `Gemini 3 Flash (Preview) (copilot)` | Google | 0.33x | 173K |
| `Gemini 3 Pro (Preview) (copilot)` ⚠️ | Google | 1x | 200K |
| `Gemini 3.1 Pro (Preview) (copilot)` | Google | 1x | 200K |
| `GPT-4.1 (copilot)` | OpenAI | 0x | 128K |
| `GPT-4o (copilot)` | OpenAI | 0x | 68K |
| `GPT-5 mini (copilot)` | OpenAI | 0x | 192K |
| `GPT-5.1 (copilot)` | OpenAI | 1x | 192K |
| `GPT-5.1-Codex (copilot)` ⚠️ | OpenAI | 1x | 256K |
| `GPT-5.1-Codex-Max (copilot)` ⚠️ | OpenAI | 1x | 256K |
| `GPT-5.1-Codex-Mini (Preview) (copilot)` ⚠️ | OpenAI | 0.33x | 256K |
| `GPT-5.2 (copilot)` | OpenAI | 1x | 400K |
| `GPT-5.2-Codex (copilot)` | OpenAI | 1x | 400K |
| `GPT-5.3-Codex (copilot)` | OpenAI | 1x | 400K |
| `GPT-5.4 (copilot)` | OpenAI | 1x | 400K |
| `Grok Code Fast 1 (copilot)` | xAI | 0.25x | 256K |

> ⚠️ Models marked with this symbol may have limited agent tool support — verify before
> use in tool-heavy agents.

---

## Model Profiles

### Free models — 0x (GPT-4.1, GPT-4o, GPT-5 mini)

**Best for:** Read-only codebase exploration, search, summaries, translations, and any
stateless lookup step where consuming premium requests is wasteful.

- `GPT-4o (copilot)` — 68K context, multimodal, fast all-rounder
- `GPT-4.1 (copilot)` — 128K context, strong instruction following
- `GPT-5 mini (copilot)` — 192K context, solid GPT-5 quality at zero cost

**Limitations:** Lower ceiling on genuinely hard reasoning and production code than
the 1x+ family. Not recommended as the primary model for write-heavy agents.

**Premium multiplier:** 0x — consumes no premium requests; use freely for `Explore`
and read-only sub-agents.

---

### Claude Haiku 4.5 (copilot)

**Best for:** Fast, low-cost Anthropic tasks — quick summaries, lightweight analysis,
high-volume steps where speed and cost matter more than peak quality.

**Strengths:**
- Very fast response time
- Good instruction following at reduced cost
- 200K context — larger than many budget alternatives

**Limitations:**
- Noticeably weaker than Sonnet on complex reasoning and nuanced instructions
- Not suited for primary implementation or review agents

**Premium multiplier:** 0.33x — budget Anthropic option.

---

### Claude Sonnet 4.6 (copilot)

**Best for:** Coordination, planning, reasoning, instruction-following, long documents.

**Strengths:**
- Excellent at following complex, multi-step instructions precisely
- Strong long-context comprehension — ideal for reading large codebases or plans
- Nuanced reasoning: understands intent, not just literal instructions
- Good at asking clarifying questions and surfacing ambiguities
- Reliable tool use and structured output (JSON, YAML, markdown tables)

**Limitations:**
- Slightly less aggressive at code generation than Codex-tuned models
- Not the fastest at pure code output throughput

**Premium multiplier:** 1x.

---

### Claude Opus 4.6 (copilot)

**Best for:** Tasks where you need the absolute best Anthropic reasoning quality and
cost is a secondary concern — complex architectural decisions, nuanced writing.

**Strengths:**
- Top-tier reasoning and language quality in the Anthropic family
- 200K context with strong long-document coherence

**Limitations:**
- 3× the cost of Sonnet 4.6 for marginal gains on most routine agent tasks
- Slower than Sonnet

**Premium multiplier:** 3x — use when Sonnet is insufficient; avoid for routine steps.

---

### ⚠️ Claude Opus 4.6 (fast mode) (Preview) (copilot)

**Best for:** Almost never for automated agents.

**Warning:** At **30x**, a single agent invocation costs as much as 30 standard
requests. If a user explicitly requests it for a one-off task, note the cost upfront.

**Premium multiplier:** 30x — **do not use in agents by default**.

---

### GPT-5.3-Codex (copilot) / GPT-5.2-Codex (copilot)

**Best for:** Code implementation — writing, editing, and reasoning about code.

**Strengths:**
- Purpose-tuned for code: excels at generating idiomatic, correctly-structured code
- Strong understanding of language-specific conventions (.NET, Python, TypeScript, etc.)
- Handles file-level refactors and multi-file edits well
- 400K context — fits entire feature branches in a single pass

**Limitations:**
- Less capable at abstract reasoning or synthesising long prose documents
- Can be overly literal — may miss design intent if instructions are imprecise

**Premium multiplier:** 1x — standard cost for the best code generation in the lineup.

---

### GPT-5.4 (copilot)

**Best for:** Critical evaluation, security review, final quality gate.

**Strengths:**
- Strong analytical and critical reasoning — great at finding flaws
- Excellent at OWASP-style security analysis and identifying subtle bugs
- More likely to catch edge cases and convention violations than Codex variants
- Good at synthesising multiple inputs (plan + implementation + tests) into a verdict
- 400K context

**Limitations:**
- Slightly slower on complex synthesis tasks

**Premium multiplier:** 1x — high analytical quality at standard cost.

---

### Gemini 3.1 Pro (Preview) (copilot)

**Best for:** Tasks benefiting from multimodal input or very large context requirements.

**Strengths:**
- 200K context with strong multimodal capabilities (vision for UI/UX design agents)
- Competitive code quality with GPT-5 variants
- Good all-rounder when context breadth matters

**Limitations:**
- Preview availability may be inconsistent
- Instruction following for complex multi-step protocols is slightly weaker than Claude Sonnet

**Premium multiplier:** 1x — same cost as Claude Sonnet; preferred for vision tasks.

---

### Grok Code Fast 1 (copilot)

**Best for:** Fast, ultra-low-cost code generation where 0x models are not capable enough.

**Strengths:**
- Near-free at 0.25x — only slightly above free tier
- Fast response, 256K context
- Good for bulk code scaffolding or repetitive file generation

**Limitations:**
- Less tested in complex multi-step agent workflows
- No vision support

**Premium multiplier:** 0.25x — budget code option between free and standard.

---

## Setting the Model in Frontmatter

Use a single string for a fixed model:

```yaml
model: Claude Sonnet 4.6 (copilot)
```

Use an array for a fallback chain when a preferred model may be unavailable (preview,
rate-limited, or temporarily down):

```yaml
model:
  - GPT-5.3-Codex (copilot)
  - GPT-5.2-Codex (copilot)
  - Claude Sonnet 4.6 (copilot)
```

Omitting `model` entirely causes the agent to inherit the user's current selection,
which is a reasonable default for users who manage their own model preferences.

---

## Common Fallback Chains

```yaml
# Code implementation — latest Codex with budget fallback
model:
  - GPT-5.3-Codex (copilot)
  - GPT-5.2-Codex (copilot)
  - Claude Sonnet 4.6 (copilot)

# Review / analysis — GPT-5.4 with Claude fallback
model:
  - GPT-5.4 (copilot)
  - Claude Sonnet 4.6 (copilot)

# Budget code — low-multiplier Codex
model:
  - GPT-5.1-Codex-Mini (Preview) (copilot)
  - GPT-5.1-Codex (copilot)
  - Grok Code Fast 1 (copilot)

# Free exploration — all 0x
model:
  - GPT-5 mini (copilot)
  - GPT-4o (copilot)
  - GPT-4.1 (copilot)
```

---

## Cost Optimisation Tips

- **Use 0x models for all read-only work.** `GPT-5 mini`, `GPT-4o`, and `GPT-4.1` cost
  nothing. Route all exploration and search tasks here.
- **GPT-5.4 is 1x — not expensive.** Use it freely for review, test engineering, and
  security analysis.
- **Budget tiers for high-volume or draft steps.** Claude Haiku 4.5 (0.33x), Gemini 3
  Flash (0.33x), GPT-5.1-Codex-Mini (0.33x), and Grok Code Fast 1 (0.25x) suit steps
  where cost matters and peak quality is not required.
- **Omit `model` in exploratory agents.** Lets users use their own preferred model
  without overriding it with an expensive default.
- **Preview models are not always cheaper.** `Gemini 3.1 Pro (Preview)` is 1x.
  `Claude Opus 4.6 (fast mode) (Preview)` is 30x. Always check before adding a preview
  model.
- **Never use 30x models as defaults.** `Claude Opus 4.6 (fast mode) (Preview)` at 30x
  costs as much as 30 standard requests per invocation.
