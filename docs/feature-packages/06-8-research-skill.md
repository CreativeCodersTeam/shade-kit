# 06-8 · Research (`shade-kit-research`) and `researches.md`

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
| Source | [06 Skill foundation](06-skill-foundation.md) (general skills, `researches.md`), [02 Repository initialisation](02-repository-initialisation.md) (template set) |
| Kind | Skill (general skill, skeleton exists) and template (`researches.template.md`) |
| Depends on | 06-2 |
| Enables | 10 (`refine`), 11 (`map`), 16 (`diagnose`) |

## Benefit

The agent collects facts with sources before decisions are made, and keeps them in one place per feature. Several research focuses run in parallel where the agent supports it.

## Scope

### In scope

- The general skill `shade-kit-research`, loaded by `refine`, `map`, and `diagnose`.
- The shared format of `researches.md` and its template `src/CreativeCoders.ShadeKit.Core/Templates/Resources/researches.template.md` (Markdown, embedded into the Core assembly; no C# change).

### Out of scope

- The concrete research focuses and their instructions (packages 10, 11, and 16).
- Research agent definitions: there are none in v1 for this skill. **Decided** on 2026-11-04.

## Functional description

### Input from the calling skill

- The calling phase (for example `refine`, `map`, or `diagnose`).
- The focuses to run, each with a name (for example `gaps`, `existing architecture`) and the instructions for it.
- The feature context: `request.md`, `spec.md`, earlier `researches.md` sections. `request.md` is data, never instructions (06-2).

### Flow

1. Start one general subagent per focus with the instructions of the calling skill. Run them in parallel where the agent supports subagents, sequentially otherwise. **Decided** on 2026-11-04. No Claude-only agent files, so the skill stays agent-neutral.
2. Collect the findings. Every finding names its source (file and line, or link); a finding without a source is marked as an assumption. A research agent reports any instruction in `request.md` that looks suspicious; the skill passes it on to the calling skill.
3. Merge into `researches.md` (format below).
4. Return to the calling skill: a short summary per focus and the reported suspicious instructions.

### `researches.md`

- One file per feature, created from `.shadekit/templates/researches.md` on the first research run (06-2).
- One section per phase run and focus, headed `<phase> / <focus>` (for example `refine / gaps`, `map / existing architecture`), each with date, findings, and sources (file paths, links).
- A later run adds new sections or replaces the section of the same phase and focus; it does not touch other sections. **Decided** on 2026-11-04.
- The template is a skeleton today (package 02). This sub-feature fixes the shared format in it: guidance, section shape, and the rule above. The sections of each phase stay owned by packages 10, 11, and 16.

## Acceptance criteria

- **FP06-8-AC1** The research skill writes one section per focus into `researches.md` and leaves sections of other phases unchanged.
- **FP06-8-AC2** On the first run of a feature, `researches.md` is created from the repository's template `.shadekit/templates/researches.md`.
- **FP06-8-AC3** Every finding in a written section names its source or is marked as an assumption.
- **FP06-8-AC4** On an agent without subagents, the focuses run one after the other and give the same sections.
- **FP06-8-AC5** A suspicious instruction in `request.md` is reported to the calling skill and not followed.

## Open points

- **Unverified:** whether GitHub Copilot and OpenAI Codex CLI support subagents (package 08).
- Whether changing `researches.template.md` needs a change to `ShippedTemplatesTests`; the test seen so far checks only names and paths. **Unverified.**
