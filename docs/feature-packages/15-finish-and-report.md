# 15 · Finish and report (`finish`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (report is optional) |
| Source | Concept §5.1 (`finish`), §5.2 (DoD), §6 |
| Depends on | 03, 04, 05, 06, 14 |
| Enables | End-to-end `feature` profile; 16 and 17 reuse it |

## Benefit

A finished feature is closed with a recorded state, and on request leaves a single report that shows what was required, which tests prove it, what the checks said, and who approved each step. Reviewers of the pull request, and anyone looking at the feature later, then get the evidence in one place instead of collecting it from six artifacts. Teams that do not need the report switch it off.

## Scope

### In scope

- The `finish` phase skill for every profile.
- The `report.md` template and the optional report (`report` in `config.json`, package 02).
- Closing the feature with `phase complete` (package 04).

### Out of scope

- The DoD check. It runs in the gate into `finish` (packages 05 and 06), before this skill starts.
- Distilling memory (later, concept §8).
- Merging, pushing, or opening a pull request (the kit never does that, package 13).

## Functional description

### Run

1. Decide whether a report is written. The `phase start finish` result contains the `report` setting from `config.json`: `always` writes it, `never` skips it, `ask` (default) asks the human. **Decided** on 2026-10-18 (findings clarification): the report is optional.
2. If a report is written: the report skill (package 06) reads the feature state with `shade-kit feature show <id> --json` (package 03: profile, phases, approval history) and the artifacts the profile produced (`spec.md`, `tasks.md`, `review.md`, `verification.md`, `playbook.md`, the filled `checks/definition-of-done.md`, and in `bugfix` `diagnosis.md`), writes `report.md`, and shows its summary to the human.
3. Call `shade-kit phase complete`. The feature is `done`, with or without a report.

### `report.md`

| Section | Content |
|---|---|
| Summary | Goal from `spec.md`, profile, start and finish date. |
| Requirements and tests | Table: requirement → acceptance criteria → proving tests → result (from `verification.md`). |
| Checks | Build, test, lint, coverage results from the last `verification.md`. |
| Review | Number of findings per severity, every `accepted` finding with its reason. |
| Manual checks | Playbook items and whether each is checked off. |
| Deviations | Changes from the plan recorded in `plan.md`. |
| Approvals | Every transition from the history: from, to, who, when, "approval off" where it applied. |

A section whose artifact the profile does not produce says so in one line instead of being left out. **Proposed**, so reports of all profiles have the same shape.

### After finish

- The feature folder stays in the repository with status `done`. Nothing is deleted or archived. **Proposed.**
- The branch may still be checked out. `shade-kit-next-phase` (package 06) then reports that the feature is finished.

## Acceptance criteria

- **FP15-AC1** If a report is written, `report.md` contains every section of the template for every profile.
- **FP15-AC2** In a written report, the approvals section lists every transition in `history`, including "approval off" transitions.
- **FP15-AC3** In a written report, every requirement of `spec.md` that is not marked "removed" appears in the requirements table.
- **FP15-AC4** After `finish`, `feature current` on the feature's branch shows status `done`, and `shade-kit-next-phase` reports the feature as finished.
- **FP15-AC5** After `finish`, a local override that pointed to the feature is cleared.
- **FP15-AC6** With `report` `never`, `finish` writes no `report.md` and still completes the feature; with `ask`, it asks the human; with `always`, it writes the report without asking.
- **FP15-AC7** The `phase start finish` result contains the `report` setting, `ask` when `config.json` has none.

## Open points

- None.
