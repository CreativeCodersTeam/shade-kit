# 17 · Quick profile (`quick` phase)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (`quick` is a phase of its own; DoD items, coverage, playbook on demand) |
| Source | Concept §2 (Workflow profiles), §5.4, §11.2 |
| Depends on | 04, 05, 06, 09, 13, 15 |
| Enables | v1 profile `quick` end to end |

## Benefit

Small, clear changes get the kit's traceability and DoD without the ceremony of six phases: one feature folder, one working phase, and one approval at the end. Teams can then use the kit for everyday work instead of bypassing it for small changes.

## Scope

### In scope

- The `quick` phase skill (`shade-kit-quick`): short clarification, compact spec, implementation, review.
- Acceptance of the whole `quick` profile from `new` to `finish`.

### Out of scope

- The profile definition and its gate defaults (one approval at the end). They exist since packages 04 and 05.
- `finish`, which runs unchanged (package 15).
- Switching a `quick` feature to another profile (package 04, open point).

## Functional description

Profile: `new` → `quick` (all in one) → `finish`, one approval at the end. **Decided** (concept §5.4).

`quick` is a phase of its own, not a variant of `implement` (**Decided** on 2026-10-18). It reuses the review loop of package 13 and the `verification.md` format of package 14, but has its own skill, so `shade-kit-implement` stays free of `quick` branches.

### The `quick` phase

1. Read `request.md`.
2. **Short clarification:** ask only the questions that block the change, using the interview skill (package 06). If the change turns out not to be small or not clear, recommend dropping it and creating a `feature` or `bugfix` instead, and stop until the human decides. **Proposed.**
3. **Compact spec:** write `spec.md` in the format of package 10 with only goal, requirements with `REQ-` IDs, and acceptance criteria; the other sections state "not applicable in quick". **Proposed**, so the DoD and the report can trace requirements to tests also in `quick`.
4. **Implement and test:** write the code and the tests that prove each acceptance criterion. No `plan.md` or `tasks.md`.
5. **Simplify and review:** the loop of package 13 with `review.md`, so the DoD item "no open blockers" applies.
6. **Self-check:** run the quality commands (build, test, lint, and coverage) and record the criterion → test mapping, the results, and the coverage value in `verification.md` (format of package 14), so the DoD can be checked without a `verify` phase. **Proposed.**
7. **Playbook on demand:** if an acceptance criterion is marked "(manual)", write `playbook.md` in the format of package 14 with one unchecked item per manual criterion. Without manual criteria, no `playbook.md` is written. **Decided** on 2026-10-18 (findings clarification).

### DoD in `quick`

The gate into `finish` uses the normal DoD (package 05). DOD-04 (`tasks.md`) and DOD-07 (`plan.md`) are not applicable. DOD-09 (`playbook.md`) is not applicable when no criterion is manual and therefore no `playbook.md` exists; otherwise it applies as in every profile. **Decided** on 2026-10-18 (findings clarification).

## Acceptance criteria

- **FP17-AC1** A `quick` feature goes from `new` to `done` with exactly one human approval, the one into `finish`.
- **FP17-AC2** The `quick` phase writes `spec.md` with `REQ-` IDs and acceptance criteria, `review.md`, and `verification.md` with the coverage value, `playbook.md` only when a criterion is manual, and no `plan.md` or `tasks.md`.
- **FP17-AC3** The DoD check in `quick` marks DOD-04 and DOD-07 as not applicable, marks DOD-09 as not applicable only when no `playbook.md` exists, and checks every other item.
- **FP17-AC4** When the change turns out to be large or unclear, the skill stops and recommends another profile.
- **FP17-AC5** If a report is written (package 15), the `quick` report has the same sections as every other report.
- **FP17-AC6** `phase skills` returns `shade-kit-quick` for the `quick` phase, and `shade-kit-implement` contains no `quick` branch.

## Open points

- None.
