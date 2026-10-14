# 06 · Skill foundation

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §3.2, §3.3, §3.4, §11.4; [ADR 0001](../adr/0001-shadekit-version-drift.md) decision 6 |
| Depends on | 01, 03, 04, 05 |
| Enables | 07, 08, and every phase package (09–17) |

## Benefit

A developer only has to say "continue" (or start a new feature) and the agent finds the current feature, its phase, and the next step, runs the gate check, asks for approval, and hands off to the right phase skill. Every phase skill builds on the same small set of shared skills, so interviews, gate checks, research, and reports work the same way everywhere.

## Scope

### In scope

- Conventions every kit skill follows.
- The version handshake on the skill side.
- The router skill.
- The general skills: interview, checklist check, research orchestration, report writing.
- The shared format of `researches.md`.

### Out of scope

- Phase skills (packages 09–17). They use what this package provides.
- Installing skills for an agent (packages 07 and 08). During development the skills are loaded from the repository's `skills/` folder.
- Concrete research focus agents (packages 10 and 11 define them; this package defines how they are run and merged).

## Functional description

### Skill conventions

- Skills are written in the shared `SKILL.md` format and the plugin layout of `skills/`. Claude-only features (for example subagents) are only used where an agent-neutral way would lose function, and each such use is named in the skill. **Decided** (constraint), **Unverified** (format compatibility, resolved in package 08).
- Skills never read or write `feature.json`, `gates.json`, or `config.json` directly. They use the CLI with `--json`. Skills read and write Markdown artifacts in the feature folder directly. **Proposed.** This keeps every state change behind the engine's version check (package 01).
- Every artifact a skill creates starts from the repository's template in `.shadekit/templates/`, not from text built into the skill, so team edits to templates take effect. **Proposed.**

### Version handshake

- Before it runs, a kit skill calls `shade-kit version --json` and compares the engine version with its own kit version. **Decided** (ADR 0001).
- Match rule: major and minor must be equal, patch may differ. **Proposed.**
- On mismatch, or if `shade-kit` is not found, the skill stops and tells the human what to update (engine or integration) and how.

### Router skill

The entry point for the human (`shade-kit`, **Proposed** name).

1. Version handshake.
2. `shade-kit feature current --json`. If not resolved: list features (`feature list --json`), ask which one or offer to create a new one, then `feature set` or hand off to `new` (package 09).
3. Read profile, current phase, and phase state, and decide:

| State of the current phase | Router action |
|---|---|
| `in-progress` | Hand off to the phase skill of the current phase. |
| work of the phase is done, human wants to continue | `phase start <next> --json`, then the gate dialogue below. |
| `awaiting-approval` | Repeat the gate dialogue for the pending transition. |
| feature `done` | Report that the feature is finished; offer to pick another feature. |

4. **Gate dialogue:** if the gate's approval is off, the engine has already performed the transition (package 05); the router runs the checklist check if the gate has a checklist, reports the result as a warning, and hands off. Otherwise: if the gate has a checklist, run the checklist check skill on it. Show the result. Ask the human for an explicit approval, also when the check passed. On yes: `phase approve <next>`, then load the returned skills and hand off. On no: `phase start <current>` (resume) and continue in the current phase with the reported gaps.
5. Also handles "reopen phase X", "drop this feature" (`feature drop`, after explicit confirmation), and "discuss" (package 09).

The router never approves without a human answer in the conversation. **Decided.**

### General skills

| Skill | Purpose | Used by |
|---|---|---|
| Interview | Asks open questions one topic at a time, offers its own proposals, records answers in the target artifact. Uses the agent's structured question tool where one exists, plain questions otherwise. | `discuss`, `refine`, `diagnose`, `plan` |
| Checklist check | Checks every item of a feature checklist against the artifacts, marks each item passed, failed (with reason), or not applicable (package 05 rule), and writes the result into the checklist copy. | Router (every gate with a checklist) |
| Research | Runs a set of research focuses, one subagent per focus in parallel where the agent supports it, sequentially otherwise, and merges the results into `researches.md`. | `refine`, `map` |
| Report | Writes a Markdown report from a template and from data the CLI returns. | `verify`, `finish` |

### `researches.md`

- One file per feature, created from the template on the first research run.
- One section per phase run and focus (for example `refine / gaps`, `map / existing architecture`), each with date, findings, and sources (file paths, links).
- A later run adds new sections or replaces the section of the same phase and focus; it does not touch other sections. **Proposed.**

## Acceptance criteria

- **FP06-AC1** With an engine whose minor version differs from the skill's, every kit skill stops before doing any work and names what to update.
- **FP06-AC2** With no resolvable current feature, the router lists the features and offers to create a new one.
- **FP06-AC3** For a feature in `in-progress`, the router hands off to the phase skill that `phase skills` returns.
- **FP06-AC4** The router asks for approval also when the checklist passed, and calls `phase approve` only after a yes in the conversation.
- **FP06-AC5** On a no, the router calls `phase start <current>` and the feature is back in `in-progress`.
- **FP06-AC6** The checklist check marks an item that refers to an artifact the profile does not have as not applicable with a reason.
- **FP06-AC7** The research skill writes one section per focus into `researches.md` and leaves sections of other phases unchanged.
- **FP06-AC8** No kit skill writes a kit JSON file directly; a review of the skill texts finds only CLI calls for state changes.

## Open points

- Exact skill names (concept open question 6).
