# 16 · Bugfix profile (`diagnose` and bugfix variants)

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §2 (Workflow profiles), §5.4, §11.2 |
| Depends on | 04, 05, 06, 09, 12, 13, 14, 15 |
| Enables | v1 profile `bugfix` end to end |

## Benefit

A defect is fixed through a short, disciplined path: reproduce it, prove the root cause, write a failing test, then fix. The bug cannot come back unnoticed, because its regression test is part of the traceability chain and of the report, just like the tests of a feature.

## Scope

### In scope

- The `diagnose` phase skill and the `diagnosis.md` template.
- The `bugfix` variant of `plan` (short plan).
- The `bugfix` variant of `implement` (failing test first).
- Acceptance of the whole `bugfix` profile from `new` to `finish`.

### Out of scope

- The profile definition and its gate defaults. They exist since packages 04 and 05.
- `verify` and `finish`, which run unchanged (packages 14 and 15).

## Functional description

Profile: `new` → `diagnose` → `plan` (short) → `implement` (failing test first) → `verify` → `finish`. **Decided.**

### `diagnose`

1. Read `request.md`.
2. **Reproduce:** find a reliable way to trigger the bug, record steps, environment, and observed versus expected behaviour. If it cannot be reproduced, stop and ask the human for more information instead of guessing.
3. **Root cause:** trace from the symptom to the cause in the code, with file and line evidence. Name the causes that were checked and ruled out.
4. Write `diagnosis.md`: reproduction, observed versus expected behaviour, root cause with evidence, ruled-out causes, affected code, fix options with a recommendation.
5. Write `spec.md` in the format of package 10: the expected behaviour as requirements with `REQ-` IDs, and at least one acceptance criterion per requirement stating that the reproduction no longer shows the bug. **Proposed**, so the DoR, `plan`, `verify`, and the report work as in the `feature` profile.

Because `bugfix` has no `refine` and no `map`, `diagnose` uses the interview skill (package 06) for missing facts, and the DoR item "affected areas known" passes on the "affected code" section of `diagnosis.md`. The `map` reference in that item is not applicable (package 05 rule).

### `plan` variant

- Input: `spec.md` and `diagnosis.md` instead of `researches.md`.
- Short `plan.md`: the chosen fix option and why, risks. Usually one to a few tasks.
- The first task in `tasks.md` is always the regression test that reproduces the bug and fails before the fix. **Decided** (failing test first).

### `implement` variant

- The regression test is written first and must be seen failing for the diagnosed reason before any production code changes. The failing run is recorded in `review.md`.
- Then the loop of package 13 runs unchanged: implement, simplify, review.

## Acceptance criteria

- **FP16-AC1** `diagnose` writes `diagnosis.md` with reproduction, root cause with file evidence, and ruled-out causes.
- **FP16-AC2** `diagnose` stops and asks the human when the bug cannot be reproduced.
- **FP16-AC3** `diagnose` writes a `spec.md` that passes the DoR check, with the `map` item marked not applicable.
- **FP16-AC4** In `bugfix`, the first task in `tasks.md` is a failing regression test.
- **FP16-AC5** `review.md` records a failing run of the regression test before the first production code change.
- **FP16-AC6** A `bugfix` feature goes from `new` to `done` with `verify` and `finish` unchanged, and its report lists the regression test under requirements and tests.

## Open points

- None.
