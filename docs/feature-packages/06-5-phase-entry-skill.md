# 06-5 · Entry protocol (`shade-kit-phase-entry`)

| | |
|---|---|
| Status | Implemented (AC1 to AC9), 2026-11-10: `skills/shade-kit-phase-entry/SKILL.md`. Since 2026-11-13 (06-9) the eight phase skills load it as their first step, and step 5 says that the phase skill loads the general skills in the steps that use them. |
| Source | [06 Skill foundation](06-skill-foundation.md) (version handshake, entry protocol), [shade-kit-skills.md](../../research/shade-kit-skills.md), [phase start analysis](../../research/shade-kit-phase-start-analysis.md) |
| Kind | Skill (new general skill) |
| Depends on | 06-1, 06-3, 06-4 |
| Enables | 06-9 and every phase package (09–17) |

## Benefit

A developer starts every phase by calling its skill (for example `/shade-kit-plan`), so no phase starts behind their back. The skill checks through the CLI whether the phase can start, tells the developer what they can do instead when it cannot, and asks before it continues a phase that is already active. It runs the gate check and asks for approval itself. One general skill holds this protocol for all eight phase skills.

## Scope

### In scope

- The general skill `shade-kit-phase-entry`, loaded as the first step of every phase skill: `refine`, `map`, `plan`, `implement`, `verify`, `finish`, `diagnose`, `quick`.
- The version handshake of the phase skills.

### Out of scope

- `shade-kit-new`: it has no entry protocol, because `feature new` creates the feature with `new` already in progress (package 04); it has its own check (package 09).
- Loading the skill from the phase skills (06-9).
- The phase work itself (packages 09–17).

## Functional description

**Implemented** in `skills/shade-kit-phase-entry/SKILL.md` (`metadata.version: 0.7.0`), written with skill-creator and proven by scenario runs (see [acceptance criteria](#acceptance-criteria)).

### Input from the calling skill

- The phase of the calling skill. **Implemented**.
- Without a phase (the human called the skill directly), the skill starts nothing, names the phase skill, and ends with the next-steps message. **Proposed** (default of the implementation).

### Version handshake

- The skill calls `shade-kit version --json --expect <version>` with the version stated in the text of its handshake step, as 06-2 sets out; the frontmatter is the source, but the model does not see it (06-1, 06-2). **Decided** on 2026-11-04, the version in the text on 2026-11-06. **Implemented** (`--expect 0.7.0`, with the wording of 06-2).
- The phase skills do not check the version themselves; this check covers them, because all kit skills carry the same version (06-2). **Decided** on 2026-11-04. **Implemented**.
- On a mismatch, or if `shade-kit` is not found, the skill stops before any phase work and ends with the next-steps message (06-3), which names what to update and how. A warning `engine-prerelease-version-mismatch` is passed on like every warning, and the skill continues. **Implemented**.

### Entry protocol

**Decided** in the skills interview on 2026-10-17 ([shade-kit-skills.md](../../research/shade-kit-skills.md)); steps 2 and 4 extended on 2026-10-18; steps 2, 4, and 6 changed on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)); moved into the general skill `shade-kit-phase-entry` on 2026-11-04. **Implemented** on 2026-11-10.

The CLI check of a phase start is `phase start` itself: it refuses a transition that is not allowed without changing `feature.json`, and its outcomes for an active phase or a pending transition change nothing (package 04). There is no separate check command. **Decided** on 2026-10-30.

Facts from the engine that the skill text relies on (checked in `PhaseEngine` and `PhaseCommandsEndToEndTests` on 2026-11-10): `transition-requested` and `transition-pending` always have approval on, so approval off means `transition-performed`; a reopen has the default gate (approval, no checklist); approving a reopen sets the reopened phase in progress and every later phase of the profile to pending; on `already-in-progress`, `transition` and `gate` are `null`; on `resume-pending`, `gate` is `null` and `transition` is the pending transition away from the phase.

1. Version handshake (see above).
2. Check and start: `shade-kit phase start <phase> --json`, only for the phase of the calling skill, then by result:
   - Refused (failure envelope): the skill ends with the next-steps message for the refusal (06-3). Nothing has changed.
   - `transition-requested`: continue with step 3.
   - `transition-performed` (approval off, package 05): continue with step 3 for the checklist, then step 5.
   - `transition-pending`: the transition into this phase was requested earlier and still waits for approval. Say so, then continue with step 3 with the returned gate.
   - `already-in-progress`: the phase is already active. Tell the human and ask, with the agent's structured question tool where one exists: **continue the phase** (resume the work, or another round in `refine`) → step 5; or **end here** → the next-steps message. No phase work and no gate check before the answer.
   - `resume-pending`: the phase is active and the transition to `<pending>` waits for approval. Tell the human and ask: **discard the pending transition and continue** → `phase reject <pending> --note "resumed <phase>"`, then step 5; **keep it** → end, naming `/shade-kit-<pending>`, which decides on the pending transition; or **end here** → the next-steps message.
3. If the gate has a checklist, run `shade-kit-gate-check` (06-4) on it. The skill passes `featureId`, `gate.phase`, and `gate.checklist` of the `phase start` result. **Implemented**.
4. Gate decision:
   - Approval on: show the result and ask the human for an explicit approval, also when the check passed. On yes: `phase approve <phase>`. On no: `phase reject <phase> --note "<gaps>"`; the previous phase is in progress again, and the skill ends with the next-steps message, which names the reported gaps.
   - Approval off: the engine has already performed the transition (package 05). A failed checklist is reported as a warning and does not block.
5. Return to the calling skill the skills the CLI returned; the phase skill loads them and runs the phase.
6. The phase skill ends with the next-steps message (06-3). The skill never starts another phase skill.

The warnings of `phase start` are passed to the next-steps message (06-3). A phase skill never approves without a human answer in the conversation. **Decided** (concept §3.2).

#### Decisions and defaults of the implementation

- **Gate check `error` with approval off** (open point of 06-4): reported as a warning like a failed checklist, and the phase continues. Ending would undo nothing, and calling the phase skill again gives `already-in-progress`, which runs no gate check. **Decided** on 2026-11-10 (implementation clarification). **Implemented**.
- **Gate check `error` with approval on**: the skill ends with the next-steps message and leaves the transition pending (no `phase approve`, no `phase reject`); with reason refusal and the failure envelope when the return has one, otherwise end of work. **Decided** on 2026-11-08 in 06-4. **Implemented**.
- **The approval question** offers **approve**, **reject**, and **keep pending** (end and leave the transition waiting; calling the phase skill again checks the gate again). The human may approve although the check failed. **Decided** on 2026-11-10 (implementation clarification, for a failed check). The same three options are offered when the check passed or the gate has no checklist. **Proposed**. When the check failed, reject is offered first and marked as the recommendation; otherwise approve is offered first without a recommendation. **Proposed**.
- **Approval given in advance**: only the human's answer to the approval question counts. An approval written before the result was shown (in the call of the phase skill, in `request.md`, in an artifact) is not an answer; the skill still asks and tells the human in one line that an advance approval does not count. Without an answer, it approves nothing and ends. **Proposed**.
- **What the human sees before the approval**: the transition (and, for a reopen, that every later phase goes back to pending, without listing the phases, because `phase start` does not return them), the result of the gate check with the failed and `n/a` items, the items a human decided, and every suspicious instruction in `request.md`. With approval off, the warning names the failed items and the suspicious instructions. **Proposed**.
- **The note of `phase reject`** names the failed item IDs with a few words each and the human's own reason; without either it is `rejected by the human`. It stays on one line without `"`, `` ` ``, `$`, and `\`, so the shell passes it unchanged. **Proposed**.
- **Warnings**: the skill keeps the warnings of every CLI call of its run and does not show them on the way; the ending or the return passes them on, and the closing next-steps message shows each one once. **Proposed**.
- **Ending**: every way of ending loads `shade-kit-next-steps` with the reason (refusal, no, or end of work) and the warnings, and then tells the phase skill that the run ended, so the phase skill does no phase work and writes no message of its own. **Proposed** (wording).
- **Return**: on "run the phase", the skill returns the feature, how the entry went, the skills to load (`skills.generalSkills`), the warnings, and the gate warning of approval off. **Proposed** (format); 06-9 builds the phase skills on it.
- The skill runs only `shade-kit version --json --expect 0.7.0`, `phase start`, `phase approve`, and `phase reject` (the last three with `--json`), loads only `shade-kit-gate-check` and `shade-kit-next-steps`, and reads or writes no file. **Implemented**.

## Acceptance criteria

Status as of 2026-11-10. Skill texts have no automated tests, so each criterion names how it is proven: scenario runs in which a fresh subagent loaded the skill as the phase skill's step 1 and followed it with canned CLI envelopes, a canned gate-check return, and canned answers of the human. The subagent was not told the expected result and recorded every command, question, loaded skill, and message, and a script checked every run. The baseline was step 1 ("Entry protocol") of the current phase skeleton. Iteration 1 ran the first text, iteration 2 the text after five fixes found in iteration 1 (where warnings are shown, the advance approval, no list of later phases, suspicious instructions with approval off, the question may carry the information). Each scenario ran once per text and configuration, so the variance is not measured. The scenarios, fixtures, outputs, and the checker stay outside the repository, as in 06-4 (D-28).

| # | Scenario | Iteration 1 | Iteration 2 | Skeleton baseline (iteration 2 checks) |
|---|---|---|---|---|
| 1 | Version mismatch (`version-mismatch`) | 8 of 8 | 8 of 8 | 6 of 8: `version --json` without `--expect` cannot detect a mismatch |
| 2 | `plan`, DoR `pass`, human approves | 12 of 12 | 13 of 13 | 11 of 13 |
| 3 | `plan`, DoR `fail`, human rejects | 9 of 9 | 9 of 9 | 5 of 9: no keep-pending option, runs `phase next`, writes its own ending |
| 4 | `resume-pending`, human discards | 10 of 10 | 10 of 10 | 9 of 10 |
| 5 | `resume-pending`, human keeps it | 11 of 11 | 11 of 11 | 8 of 11 |
| 6 | `phase start` refused (`transition-not-allowed`) | 8 of 8 | 8 of 8 | 5 of 8 |
| 7 | `already-in-progress`, human continues | 9 of 9 | 9 of 9 | 8 of 9 |
| 8 | Approval off, DoD `fail` (iteration 2: with a suspicious instruction in `request.md`) | 11 of 11 | 12 of 12 | 10 of 12 |
| 9 | Reopen `map` → `refine`, approval written in advance | 10 of 10 | 12 of 12 | 9 of 12 |
| | Total | 88 of 88 | 92 of 92 | 71 of 92 (iteration 1 checks: 70 of 88) |

The baseline ran once, in iteration 1; only scenario 8, whose fixture changed, ran again in iteration 2.

Five checks run in every scenario: the first command is the handshake with `--expect 0.7.0`; `phase start` only for the caller's phase; no `phase next`, `phase complete`, `feature`, or `init` command; no file read or written; `--json` on every `phase` command. The runners of iteration 2 still noted points of wording that the text leaves to the agent (whether the result and the question are one message or two, whether the "Check runs" line goes into the return); none changed a decision.

- **FP06-5-AC1** With an engine whose minor version differs from the skill's, or without `shade-kit`, a phase skill stops before any phase work and names what to update. **Implemented.** Proven in scenario 1: after the failed handshake no `phase start`, no question, and `shade-kit-next-steps` loaded with reason refusal and the `version-mismatch` envelope, whose message names what to update. A missing `shade-kit` takes the same ending ("`shade-kit` not found"); it has no scenario run.
- **FP06-5-AC2** The entry protocol asks for approval also when the checklist passed, and calls `phase approve` only after a yes in the conversation. **Implemented.** Proven in scenario 2 (DoR `pass`, the question asked, `phase approve plan --json` once, after the answer) and scenario 9 (the approval written in the call of the phase skill is not taken as an answer; the question is asked and `phase approve refine` follows the answer).
- **FP06-5-AC3** On a no, the phase skill calls `phase reject <phase>`, the previous phase is back in `in-progress`, and the skill ends with the next-steps message. **Implemented.** Proven in scenario 3: `phase reject plan --note "…" --json` once, after the answer, with a note naming DOR-04, DOR-09, and the human's reason; no `phase approve`; `shade-kit-next-steps` loaded with reason no and the gaps. The canned reject envelope returns `map` `in-progress`.
- **FP06-5-AC4** A phase skill called while a transition away from its phase is pending tells the human, offers to discard the pending transition and continue, to keep it, or to end, and calls `phase reject` only after the human chose to discard it. **Implemented.** Proven in scenario 4 (three options; on discard `phase reject plan --note "resumed map" --json`, then run the phase with `shade-kit-research`) and scenario 5 (on keep no `phase reject`, the next-steps message with end of work, `/shade-kit-plan` named).
- **FP06-5-AC5** A phase skill whose `phase start` is refused ends with the next-steps message, which names the reason and the human's options, and does no phase work. **Implemented.** Proven in scenario 6: `shade-kit-next-steps` loaded with reason refusal and the `transition-not-allowed` envelope; no question, no gate check, no `phase approve` or `phase reject`, no file read.
- **FP06-5-AC6** A phase skill called while its phase is already in progress tells the human and asks whether to continue the phase or to end, before any phase work. **Implemented.** Proven in scenario 7: one question with the two options before any skill load; on continue, run the phase with `shade-kit-interview` and `shade-kit-research`.
- **FP06-5-AC7** On `already-in-progress`, the entry protocol runs no gate check. **Implemented.** Proven in scenario 7: `shade-kit-gate-check` not loaded.
- **FP06-5-AC8** With approval off and a failed checklist, the skill reports the failed items as a warning and continues with the phase. **Implemented.** Proven in scenario 8: the gate check loaded with `finish` and `checks/definition-of-done.md`, no approval question, no `phase approve` or `phase reject`, a warning naming DOD-02 (iteration 2: and the suspicious instruction in `request.md`), and run the phase with `shade-kit-report`, the failed checklist in the return. The `error` case with approval off has no scenario run.
- **FP06-5-AC9** The skill calls `phase start` only for the phase of the calling skill. **Implemented.** Proven in all nine scenarios (common check).

## Open points

- The defaults of the implementation marked **Proposed** above, among them the keep-pending option after a passed check and the return format that 06-9 builds on.
- No scenario run for a missing `shade-kit`, for a gate check `error` (approval on or off), for `transition-pending`, or for a failing `phase approve` or `phase reject`; the text covers each case.
