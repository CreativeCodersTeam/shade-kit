# 06-9 · Align the skill skeletons

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06), updated 2026-11-13 (implementation: the eight phase skills load `shade-kit-phase-entry` and `shade-kit-next-steps`; `new`, `discuss`, and `drop` run the handshake with `--expect 0.7.0` and end with `shade-kit-next-steps`; `phase next` returns no `continue` step in `new`; the planned-commands sentence removed from 12 skeletons). AC1 to AC8 implemented. |
| Source | [06 Skill foundation](06-skill-foundation.md), open points in [README](README.md#open-questions) (skill skeletons not aligned with the built `phase` commands) |
| Kind | Cross-cutting (all skill skeletons), with one engine change (`WorkflowPhase.CanContinue`, package 04) |
| Depends on | 06-3, 06-5, 06-6 |
| Enables | Every phase package (09–17) |

## Benefit

Every skill the human calls is gated and ends the same way before its phase is worked out: the phase packages 09–17 build on skeletons that already use the entry protocol and the next-steps message.

## Scope

### In scope

- The 8 phase skills: `refine`, `map`, `plan`, `implement`, `verify`, `finish`, `diagnose`, `quick`.
- `new`, `discuss`, and `drop`, which the human calls but which have no entry protocol.
- The review by hand of all skill texts (06-2).
- `phase next` without a `continue` step for `new` (package 04), because the next-steps message of `new` would otherwise offer "Continue new: /shade-kit-new", which creates another feature. **Decided** on 2026-11-13.

### Out of scope

- The phase work of each skill (packages 09–17).
- Removing the source comments that point to `docs/` and `research/`: they stay until a skill is worked out; FP07-AC7 checks them when the skills are shipped. **Decided** on 2026-11-04.
- `shade-kit-report` (package 15) and `shade-kit-review` (package 13), apart from the review by hand and the removal of the planned-commands sentence in `shade-kit-report` (FP06-9-AC8 applies to it). **Decided** on 2026-11-13.

## Functional description

**Implemented** on 2026-11-13 in the skills under `skills/`, written with skill-creator and proven by scenario runs (see [acceptance criteria](#acceptance-criteria)).

### Phase skills

- Step 1 of every phase skill loads `shade-kit-phase-entry` (06-5) with its phase and continues only as the entry protocol says. **Implemented**: on "ended" the phase skill stops and writes nothing more; on "run the phase" it keeps the feature and the warnings of the return.
- The last step of every phase skill loads `shade-kit-next-steps` (06-3). **Implemented**: every way the skill ends after the entry protocol ran the phase loads it with **end of work** (one line of what happened) or **refusal** (the failure envelope of the skill's own CLI call, for example `phase complete` in `finish`), and passes the warnings of the entry protocol's return and of the skill's own CLI calls.
- The description of every phase skill says that it runs only when the user calls it by name (06-2). **Implemented** (already true of all eight descriptions; checked).
- Step 1 and the Ending section have the same text in all eight phase skills, only the phase name differs, so they do not drift. **Decided** on 2026-11-13.
- A general skill that the return of the entry protocol names is loaded in the step that uses it, not in step 1. `finish` loads `shade-kit-report` only when a report is written. **Decided** on 2026-11-13.
- The gate warning of the entry protocol (approval off and a failed checklist, or suspicious instructions in `request.md`) is shown once right after the entry protocol, before any phase work; the CLI warnings are shown at the end by the closing message. **Decided** on 2026-11-13.
- The report of a phase skill names a recommendation and its reason, but a skill only when the closing message does not list it (that message lists the steps of `phase next`, `/shade-kit-discuss`, and `/shade-kit-drop`). Example: `verify` recommends reopening `implement` without naming `/shade-kit-implement`; `quick` names `/shade-kit-new` when the change is too big, because the closing message does not list it while the feature is active. **Decided** on 2026-11-13.

### `new`, `discuss`, `drop`

- Step 1 is the version handshake with the version stated in the text of the step, as 06-2 sets out (the frontmatter is the source, but the model does not see it): `shade-kit version --json --expect <version>` (06-1, 06-2). **Decided** on 2026-11-04, the version in the text on 2026-11-06. **Implemented** (`--expect 0.7.0`, with the same text as the handshake step of `shade-kit-next-phase`).
- The last step loads `shade-kit-next-steps` (06-3). **Implemented**: every way of ending, a failed CLI call included, loads it with **refusal** or **end of work**.
- A failure envelope of `feature current` ends `discuss` with **refusal** before the discussion (for example `kit-root-not-found`, because the notes live in `.shadekit/`), and `new` likewise. **Decided** on 2026-11-13.
- The descriptions of `discuss` and `drop` also say that they run only when the user calls them by name or asks for them. **Decided** on 2026-11-13.
- `drop` asks for the confirmation with the structured question tool and gets the profile and phase of a feature named by the human from `feature show`. **Implemented**.

### Clean-up

- The sentence in the source comments of the skeletons that every command other than `version`, `init`, and `feature …` is planned is removed or corrected (12 skeletons on 2026-11-13; the count of 13 from 2026-11-04 was wrong). **Decided** on 2026-11-04. **Implemented**: removed from `refine`, `map`, `plan`, `implement`, `verify`, `finish`, `diagnose`, `quick`, `new`, `discuss`, `drop`, and `report`; the rest of each source comment stays.
- Skeletons that show CLI results tell the human about the warnings (through `shade-kit-next-steps`). **Implemented**.
- The skeletons of `finish` and `report` keep their expectation of the `report` setting; package 15 owns it (FP15-AC7). **Implemented**: `finish` expects the setting from the result of `phase start finish`, which the entry protocol passes on, and treats it as `ask` when the return carries none; without an answer to the report question it does not complete the feature. **Decided** on 2026-11-13.
- Facts that the built code contradicts are corrected: `new` no longer says that `feature new` writes no phase fields until package 04 exists, and `finish` calls `phase complete --json`. **Decided** on 2026-11-13.

### `phase next` in `new`

`WorkflowPhase` has a flag `CanContinue` (default `true`), `false` for `new` in every built-in profile; `phase next` returns `continue: null` for a phase that cannot be continued. The engine stays free of phase names (`PhaseEngineArchitectureTests`). `shade-kit-next-steps` and `shade-kit-next-phase` say that `continue` is `null` in `new`. **Decided** on 2026-11-13. **Implemented** (`WorkflowPhase`, `BuiltInWorkflowProfiles`, `PhaseEngine.GetNext`; tests in `WorkflowPhaseTests`, `BuiltInWorkflowProfilesTests`, `PhaseEngineNextTests`, `PhaseCommandsEndToEndTests`; package 04 updated).

### Review of the skill texts

All skill texts under `skills/` are reviewed by hand against the conventions of 06-2. **Decided** on 2026-11-04. **Implemented** on 2026-11-13: no skill starts a phase skill or hands off to one; `phase start` appears only for the skill's own phase (in the phase skills, through the entry protocol) and in the general skills that describe it; no skill reads or writes `feature.json`, `gates.json`, or `config.json`; no skill calls a built command planned; `quick_validate.py` reports all 19 frontmatters valid; the handshake text is the same in `new`, `discuss`, `drop`, and `shade-kit-next-phase`.

## Acceptance criteria

Status as of 2026-11-13. There are no automated tests of the skill texts (06-2), so each criterion names how it is proven: the review by hand above and scenario runs. In each run a subagent loads the skill from a skills folder, follows it with canned CLI envelopes and canned answers, and records every command, skill load, question, read, write, and message as a timeline; a script checks the timelines. The baseline is the skeletons before this change. The scenarios, timelines, and script were kept outside the repository.

| # | Scenario | New text, iteration 1 | Iteration 2 | Skeletons (baseline) |
|---|---|---|---|---|
| 1 | `refine`, transition requested and approved | 14 of 14 | | 6 of 14 |
| 2 | `map`, `phase start` refused (`transition-not-allowed`) | 13 of 13 | | 9 of 13 |
| 3 | `plan`, already in progress, human continues | 14 of 14 | | 10 of 14 |
| 4 | `verify`, two failing acceptance criteria | 14 of 14 | | 9 of 14 |
| 5 | `quick`, change too big | 14 of 14 | | 8 of 14 |
| 6 | `finish`, no `report` setting in the return, human wants no report | 14 of 14 | 14 of 14 | 6 of 14 |
| 7 | `new`, version mismatch | 11 of 11 | | 8 of 11 |
| 8 | `new`, feature created with a branch | 14 of 14 | | 8 of 14 |
| 9 | `discuss` without `.shadekit/` | 12 of 12 | | 8 of 12 |
| 10 | `drop` confirmed, prerelease engine warning | 12 of 12 | | 5 of 12 |
| 11 | `drop` not confirmed | 11 of 11 | 11 of 11 | 6 of 11 |
| | Total | 143 of 143 | 25 of 25 | 83 of 143 |

Iteration 2 reran the scenarios of the two skills changed after iteration 1 (the structured question tool for the confirmation of `drop` and the report question of `finish`, and the rule for no answer in `finish`). After iteration 2, two wording points from its runners were clarified without another run: the Ending says "when the report makes a recommendation" (no recommendation is required), and `drop` takes profile and phase for the confirmation from step 2.

Checks in every scenario: the first `shade-kit` command is the handshake with `--expect 0.7.0`; every `shade-kit` command has `--json`; `shade-kit-next-steps` is loaded exactly once and last; no kit JSON file is read or written; a phase skill runs `phase start` only for its own phase and loads `shade-kit-phase-entry` first; `new`, `discuss`, and `drop` run no `phase start`, `approve`, `reject`, or `complete`; no other phase skill is loaded; a closing message is written.

- **FP06-9-AC1** A phase skill called directly, without `shade-kit-next-phase`, runs the entry protocol before any phase work. **Implemented.** Proven in scenarios 1 to 6: `shade-kit-phase-entry` is the first skill loaded, before any read; in scenario 2 the refused start ends without any read or write.
- **FP06-9-AC2** No skill starts a phase skill, and no phase skill calls `phase start` for a phase other than its own; a review of the skill texts finds no hand-off. **Implemented.** Proven by the review by hand and in all scenarios (common checks).
- **FP06-9-AC3** Every phase skill ends with the next-steps message, built from `phase next --json` when a current feature exists. **Implemented.** Proven in scenarios 1 to 6: `shade-kit-next-steps` loaded once and last; `phase next` run exactly once where the message needs steps (scenarios 1, 2, 3, 4, 6).
- **FP06-9-AC4** The description of every phase skill says that it runs only when the user calls it by name. **Implemented.** Proven by the review by hand (all eight descriptions, and those of `new`, `discuss`, and `drop`).
- **FP06-9-AC5** No kit skill writes a kit JSON file directly; a review of the skill texts finds only CLI calls for state changes. **Implemented.** Proven by the review by hand and in all scenarios (common check).
- **FP06-9-AC6** For a feature whose current phase is `in-progress`, the phase skill of that phase, called by the human, runs no gate. **Implemented.** Proven in scenario 3: `shade-kit-gate-check` not loaded, no `phase approve` or `phase reject`, the continue question before any read (the entry protocol of 06-5; FP06-5-AC7).
- **FP06-9-AC7** `new`, `discuss`, and `drop` stop before any other work when the engine's minor version differs from their own, and end with the next-steps message. **Implemented.** Proven in scenario 7 (no further command, no question, the message names what to update); scenarios 8 to 11 run the handshake first; scenario 10 shows the prerelease warning in the closing message. `discuss` and `drop` take the same handshake text; their mismatch has no scenario run.
- **FP06-9-AC8** No skill says that a built `phase` command is planned. **Implemented.** Proven by the review by hand: the sentence is gone from all 12 skeletons.

## Open points

- The **Proposed** defaults of 06-5 and 06-3 that the phase skills now rely on (the return format of the entry protocol, the refusal table).
- The `report` setting cannot reach `finish` until package 15 adds it to the result of `phase start finish` and to the return of `shade-kit-phase-entry` (FP15-AC7); until then `finish` always asks.
- No scenario run for a failing `phase complete` in `finish`, a failing `feature new`, a mismatch in `discuss` or `drop`, `transition-pending`, or a gate warning with approval off; the texts cover each case.
- Points for the phase packages found by the runners (not in scope here): resuming an artifact that an earlier session left incomplete (`plan`, `verify`: "write from the template" would overwrite it, 12, 14); the template paths and the feature title are not named (10, 12, 14); `quick` loses the interview answers when it ends because the change is too big, and the closing message still offers `finish` (17); the coverage threshold is not used in `verify` (14); who records the human's approval of the quality commands in `plan` (12).
