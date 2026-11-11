# 06 · Skill foundation

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (template conventions, checklist format), 2026-10-18 (no router: `shade-kit-next-phase` and entry protocol; findings clarification: `phase next`, pending transition, report and review skills), 2026-10-24 (documentation check: skill names of the general skills, placeholders in checklist copies), 2026-10-25 (code review of the rework of `feature new` in package 03: `request.md` treated as data, not as instructions), 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md): phases start only when the user calls the phase skill, `shade-kit-next-phase` is advisory, next-steps message, active phase asks the human), 2026-11-04 (split into the sub-features 06-1 to 06-9; version in the skill frontmatter, checked by the engine; `shade-kit-report` moved to package 15), 2026-11-06 (06-2: Claude Code does not show the frontmatter to the model), 2026-11-08 (06-3: `shade-kit-next-steps` implemented; 06-4: `shade-kit-gate-check` and `shade-kit gate info` implemented), 2026-11-10 (06-5: `shade-kit-phase-entry` implemented), and 2026-11-11 (06-6: `shade-kit-next-phase` implemented) |
| Source | Concept §3.2, §3.3, §3.4, §11.4, [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 01, 03, 04, 05 |
| Enables | 07, 08, and every phase package (09–17) |

## Benefit

A developer starts every phase by calling its skill (for example `/shade-kit-plan`), so no phase starts behind their back. The skill checks through the CLI whether the phase can start, tells the developer what they can do instead when it cannot, and asks before it continues a phase that is already active. It runs the gate check and asks for approval itself. When the developer does not know where a feature stands, `shade-kit-next-phase` shows the current phase and names the skill to call for each possible next step. Every phase skill builds on the same small set of shared skills, so the entry protocol, the closing message, interviews, gate checks, and research work the same way everywhere.

## Sub-features

Package 06 is built in sub-features. Each one has its own functional description, acceptance criteria (`FP06-n-ACm`), and open points. The number is the build order: every sub-feature depends only on sub-features with a lower number. **Decided** on 2026-11-04.

| # | Sub-feature | Kind | Depends on |
|---|---|---|---|
| 06-1 | [Engine version check (`version --expect`)](06-1-engine-version-check.md) | .NET | 01 |
| 06-2 | [Skill conventions and version metadata](06-2-skill-conventions-and-version.md) | Cross-cutting | 06-1 |
| 06-3 | [Next-steps message (`shade-kit-next-steps`)](06-3-next-steps-skill.md) | Skill (new) | 06-2 |
| 06-4 | [Checklist check (`shade-kit-gate-check`)](06-4-gate-check-skill.md) | Skill and CLI command (`gate info`) | 05, 06-2 |
| 06-5 | [Entry protocol (`shade-kit-phase-entry`)](06-5-phase-entry-skill.md) | Skill (new) | 06-1, 06-3, 06-4 |
| 06-6 | [`shade-kit-next-phase`](06-6-next-phase-skill.md) | Skill | 06-1, 06-3 |
| 06-7 | [Interview (`shade-kit-interview`)](06-7-interview-skill.md) | Skill | 06-2 |
| 06-8 | [Research (`shade-kit-research`) and `researches.md`](06-8-research-skill.md) | Skill and template | 06-2 |
| 06-9 | [Align the skill skeletons](06-9-skill-skeleton-alignment.md) | Cross-cutting | 06-3, 06-5, 06-6 |

## Scope

### In scope

- Conventions every kit skill follows, and the kit version of every skill (06-2).
- The version handshake: the check in the engine (06-1) and its calls in the skills (06-5, 06-6, 06-9).
- The entry protocol every phase skill runs (06-5).
- The next-steps message every skill ends with (06-3).
- `shade-kit-next-phase`, which shows where a feature stands and which skill to call next (06-6).
- The general skills: next-steps message, checklist check, entry protocol, interview, research orchestration (06-3, 06-4, 06-5, 06-7, 06-8).
- The shared format of `researches.md` (06-8).
- Aligning the existing skill skeletons with the general skills (06-9).

### Out of scope

- Phase work of the phase skills (packages 09–17). They use what this package provides.
- `shade-kit-report`: moved to package 15 on 2026-11-04, because its template, its data, and the `report` setting all belong to `finish`. **Decided** on 2026-11-04.
- Installing skills for an agent (packages 07 and 08). During development the skills are loaded from the repository's `skills/` folder.
- Concrete research focuses (packages 10, 11, and 16 define them; 06-8 defines how they are run and merged).

## Decisions of 2026-11-04

- The kit version of a skill is hardcoded in its frontmatter as `metadata.version` and committed, because the skills must be installable as a plugin with their version included. Every skill carries it, all with the same value (06-2).
- The engine compares: `shade-kit version --json --expect <version>`. Major and minor must be equal, patch may differ; a prerelease engine only warns on a mismatch (06-1). New exit code range 50–59 with `50 version-mismatch`.
- The handshake runs in `shade-kit-phase-entry` for the phase skills, and in `shade-kit-next-phase`, `new`, `discuss`, and `drop` with their own call. General skills that are only loaded by another skill do not check (wording of 2026-11-04; clarified on 2026-11-06, 06-2: except `shade-kit-phase-entry`, which checks for the phase skills). FP06-AC1 ("every kit skill stops") therefore holds for every skill the human calls.
- The entry protocol and the next-steps message are general skills of their own (`shade-kit-phase-entry`, `shade-kit-next-steps`).
- `shade-kit-research` runs one general subagent per focus with the instructions of the calling skill; no research agent definitions.
- The skill texts are checked by a review by hand; no automated tests of the skill texts.
- **Decided** from **Proposed**: skills never write kit JSON directly; every artifact starts from the repository's template; the match rule of the version handshake; the merge rule of `researches.md`.
- The refusal table gets rows for 17 and 47 and a fallback row; warnings of `phase start` and `phase next` are always shown (06-3).
- FP06-AC13 (no references to `research/` or `docs/` in shipped skills) is checked when the skills are shipped, as FP07-AC7.

## General skills

| Skill | Purpose | Used by | Owner |
|---|---|---|---|
| Next steps (`shade-kit-next-steps`) | Ends a skill with the steps the human can take and the skill to call for each. **Implemented** (`skills/shade-kit-next-steps/SKILL.md`, 06-3). | Every kit skill that ends | 06-3 |
| Checklist check (`shade-kit-gate-check`) | Checks every item of a feature checklist against the artifacts and logs the run. | Entry protocol (every gate with a checklist) | 06-4 |
| Entry protocol (`shade-kit-phase-entry`) | Version handshake, `phase start`, gate check, and approval. | Every phase skill | 06-5 |
| Interview (`shade-kit-interview`) | Asks open questions one topic at a time, offers its own proposals, records answers in the target artifact. | `discuss`, `refine`, `diagnose`, `plan`, `quick` | 06-7 |
| Research (`shade-kit-research`) | Runs a set of research focuses and merges the results into `researches.md`. | `refine`, `map`, `diagnose` | 06-8 |
| Report (`shade-kit-report`) | Writes `report.md`. Optional. | `finish` | Package 15 |
| Review (`shade-kit-review`) | Independent code review with finding severities. | `implement`, `quick` | Package 13 |

## Acceptance criteria

The acceptance criteria are in the sub-features. The former criteria of this package map to them as follows. **Decided** on 2026-11-04 (new ids per sub-feature).

| Former | Now |
|---|---|
| FP06-AC1 | FP06-1-AC1 (engine), FP06-5-AC1 (phase skills), FP06-6-AC1 (`shade-kit-next-phase`), FP06-9-AC7 (`new`, `discuss`, `drop`) |
| FP06-AC2 | FP06-6-AC2 |
| FP06-AC3 | FP06-6-AC3, FP06-9-AC6 |
| FP06-AC4 | FP06-5-AC2 |
| FP06-AC5 | FP06-5-AC3 |
| FP06-AC6 | FP06-4-AC1 |
| FP06-AC7 | FP06-8-AC1 |
| FP06-AC8 | FP06-9-AC5 |
| FP06-AC9 | FP06-6-AC4 |
| FP06-AC10 | FP06-9-AC1 |
| FP06-AC11 | FP06-5-AC4 |
| FP06-AC12 | FP06-6-AC5 |
| FP06-AC13 | FP07-AC7 (package 07) |
| FP06-AC14 | FP06-3-AC2, FP06-5-AC5 |
| FP06-AC15 | FP06-5-AC6 |
| FP06-AC16 | FP06-9-AC2 |
| FP06-AC17 | FP06-3-AC1, FP06-9-AC3 |
| FP06-AC18 | FP06-9-AC4 |

## Open points

- **Alignment of the skill version with GitVersion skipped for now** (06-2).
- Frontmatter visibility: Claude Code 2.1.318 does not show the frontmatter to the model (checked on 2026-11-06 in 06-2, on project-level skills), so the handshake step states the version in its text. **Unverified** for Copilot and Codex (package 08).
- Until package 07 builds `integrations update`, the mismatch message of 06-1 names updating the skills by hand.
- Exact skill names (concept open question 6).
- Phase skills are restricted to user invocation only by convention (06-2); a technical restriction is a possible later addition.
