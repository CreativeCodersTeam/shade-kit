# 06 · Skill foundation

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (template conventions, checklist format) and 2026-10-18 (no router: `shade-kit-next-phase` and entry protocol) |
| Source | Concept §3.2, §3.3, §3.4, §11.4, [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 01, 03, 04, 05 |
| Enables | 07, 08, and every phase package (09–17) |

## Benefit

A developer only has to say "continue" (or start a new feature) and the agent finds the current feature, its phase, and the possible next steps, and starts the phase skill the human chooses. That phase skill runs the gate check and asks for approval itself, so a phase is gated also when its skill is called directly. Every phase skill builds on the same small set of shared skills, so interviews, gate checks, research, and reports work the same way everywhere.

## Scope

### In scope

- Conventions every kit skill follows.
- The version handshake on the skill side.
- The entry protocol every phase skill runs.
- `shade-kit-next-phase`, the entry point that offers the next steps.
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
- Every artifact a skill creates starts from the repository's template in `.shadekit/templates/`, not from text built into the skill, so team edits to templates take effect. **Proposed.** The skill follows the template conventions of package 02: it follows the guidance comments, replaces every placeholder, and removes the guidance comments, except in checklist copies.

### Version handshake

- Before it runs, a kit skill calls `shade-kit version --json` and compares the engine version with its own kit version. **Decided** (concept §9.6).
- Match rule: major and minor must be equal, patch may differ. **Proposed.**
- On mismatch, or if `shade-kit` is not found, the skill stops and tells the human what to update (engine or integration) and how.

### Entry protocol

Every phase skill runs it as its first step, so a direct call such as `/shade-kit-plan` is gated too. **Decided** in the skills interview on 2026-10-17 ([shade-kit-skills.md](../../research/shade-kit-skills.md)); steps 2 and 4 extended on 2026-10-18. `shade-kit-new` has no entry protocol, because `feature new` creates the feature with `new` already in progress (package 04).

1. Version handshake.
2. `shade-kit phase start <phase> --json`. If the transition is not allowed, stop and show the reason. If the phase is already in progress (a new session, another `refine` round), the engine changes nothing and returns no gate (package 04): continue with step 5.
3. If the gate has a checklist, run the checklist check skill on it.
4. Gate decision:
   - Approval on: show the result and ask the human for an explicit approval, also when the check passed. On yes: `phase approve <phase>`. On no: `phase reject <phase> --note "<gaps>"`; the previous phase is in progress again, and the skill hands off to `shade-kit-next-phase` with the reported gaps.
   - Approval off: the engine has already performed the transition (package 05). A failed checklist is reported as a warning and does not block.
5. Load the skills the CLI returned and run the phase.

A phase skill never approves without a human answer in the conversation. **Decided.**

### `shade-kit-next-phase`

The entry point for the human when they say "continue" or ask where a feature stands. It only chooses and hands off: it never runs a gate check and never calls `phase approve`. **Decided** in the skills interview on 2026-10-17.

1. Version handshake.
2. `shade-kit feature current --json`. If not resolved: list features (`feature list --json`), ask which one or offer to create a new one, then `feature set` or hand off to `shade-kit-new` (package 09).
3. Read profile, feature status, current phase, and phase state, and offer the possible next steps:

| State | Offered |
|---|---|
| feature `done` | Report that the feature is finished; offer to pick another feature. |
| current phase `in-progress` | Continue the current phase, or move on to the next phase of the profile. |
| current phase `awaiting-approval` | Repeat the pending transition (the phase skill of the pending phase runs its entry protocol again). |
| any state of an active feature | Reopen an earlier phase, `shade-kit-discuss`, `shade-kit-drop`. |

4. Let the human choose, with the agent's structured question tool where one exists.
5. Start the phase skill of the chosen phase. Its entry protocol performs the transition: forward, re-entry, or reopen (package 04).

### General skills

| Skill | Purpose | Used by |
|---|---|---|
| Interview | Asks open questions one topic at a time, offers its own proposals, records answers in the target artifact. Uses the agent's structured question tool where one exists, plain questions otherwise. | `discuss`, `refine`, `diagnose`, `plan` |
| Checklist check | Checks every item of a feature checklist against the artifacts, sets its `Result` to `pass`, `fail`, or `n/a` (package 05 rule) with the `Evidence`, and adds a line to the "Check runs" log of the checklist copy (format in package 05). | Entry protocol of every phase skill (every gate with a checklist) |
| Research | Runs a set of research focuses, one subagent per focus in parallel where the agent supports it, sequentially otherwise, and merges the results into `researches.md`. | `refine`, `map` |
| Report | Writes a Markdown report from a template and from data the CLI returns. | `verify`, `finish` |

### `researches.md`

- One file per feature, created from the template on the first research run.
- One section per phase run and focus (for example `refine / gaps`, `map / existing architecture`), each with date, findings, and sources (file paths, links).
- A later run adds new sections or replaces the section of the same phase and focus; it does not touch other sections. **Proposed.**

## Acceptance criteria

- **FP06-AC1** With an engine whose minor version differs from the skill's, every kit skill stops before doing any work and names what to update.
- **FP06-AC2** With no resolvable current feature, `shade-kit-next-phase` lists the features and offers to create a new one.
- **FP06-AC3** For a feature whose current phase is `in-progress`, `shade-kit-next-phase` offers to continue it, and the phase skill then continues without a gate.
- **FP06-AC4** The entry protocol asks for approval also when the checklist passed, and calls `phase approve` only after a yes in the conversation.
- **FP06-AC5** On a no, the phase skill calls `phase reject <phase>`, the previous phase is back in `in-progress`, and `shade-kit-next-phase` takes over.
- **FP06-AC6** The checklist check marks an item that refers to an artifact the profile does not have as not applicable with a reason.
- **FP06-AC7** The research skill writes one section per focus into `researches.md` and leaves sections of other phases unchanged.
- **FP06-AC8** No kit skill writes a kit JSON file directly; a review of the skill texts finds only CLI calls for state changes.
- **FP06-AC9** `shade-kit-next-phase` never runs a gate check and never calls `phase approve`.
- **FP06-AC10** A phase skill called directly, without `shade-kit-next-phase`, runs the entry protocol before any phase work.

## Open points

- Exact skill names (concept open question 6).
- A CLI command that returns the possible next steps, so `shade-kit-next-phase` does not derive them from the profile (proposal `phase next --json`, follow-up 1 in [shade-kit-skills.md](../../research/shade-kit-skills.md)). **Open.**
