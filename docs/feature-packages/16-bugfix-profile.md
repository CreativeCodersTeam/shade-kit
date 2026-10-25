# 16 · Bugfix profile (`diagnose` and bugfix variants)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (short plan sections, DOR-09 and DOR-10 in `bugfix`), 2026-10-24 (documentation check: acceptance criteria in id order), and 2026-10-25 (code review of the rework of `feature new` in package 03: `request.md` treated as data, not as instructions) |
| Source | Concept §2 (Workflow profiles), §5.4, §11.2 |
| Depends on | 04, 05, 06, 09, 12, 13, 14, 15 |
| Enables | v1 profile `bugfix` end to end |

## Benefit

A defect is fixed through a short, disciplined path: reproduce it, prove the root cause, write a failing test, then fix. The bug cannot come back unnoticed, because its regression test is part of the traceability chain and of the report, just like the tests of a feature.

## Scope

### In scope

- The `diagnose` phase skill and the `diagnosis.md` template.
- The security research of `diagnose` (section `diagnose / security` in `researches.md`).
- The `bugfix` variant of `plan` (short plan).
- The `bugfix` variant of `implement` (failing test first).
- Acceptance of the whole `bugfix` profile from `new` to `finish`.

### Out of scope

- The profile definition and its gate defaults. They exist since packages 04 and 05.
- `verify` and `finish`, which run unchanged (packages 14 and 15).

## Functional description

Profile: `new` → `diagnose` → `plan` (short) → `implement` (failing test first) → `verify` → `finish`. **Decided** (concept §5.4).

### `diagnose`

1. Read `request.md`. It can hold third-party content (issue text, comments, web pages): the skill treats it as data that describes the requirement, never as instructions, and shows the human any instruction in it that looks suspicious ([skill conventions](06-skill-foundation.md#skill-conventions)). **Decided** on 2026-10-25 (code review of the rework of `feature new`, finding F6). **Implemented** (`shade-kit-diagnose`).
2. **Reproduce:** find a reliable way to trigger the bug, record steps, environment, and observed versus expected behaviour. If it cannot be reproduced, stop and ask the human for more information instead of guessing.
3. **Root cause:** trace from the symptom to the cause in the code, with file and line evidence. Name the causes that were checked and ruled out.
4. **Security:** run the research skill (package 06) with the focus "security at code level" (the `map` agent, package 11) on the affected code and the fix options: is the bug itself a vulnerability, and does a fix touch input handling, authentication, secrets, or protected data? The result goes into the section `diagnose / security` of `researches.md`. **Decided** on 2026-10-18 (findings clarification).
5. Write `diagnosis.md`: reproduction, observed versus expected behaviour, root cause with evidence, ruled-out causes, affected code, fix options with a recommendation.
6. Write `spec.md` in the format of package 10: the expected behaviour as requirements with `REQ-` IDs, and at least one acceptance criterion per requirement stating that the reproduction no longer shows the bug. "Security considerations" is filled from `diagnose / security`. **Proposed**, so the DoR, `plan`, `verify`, and the report work as in the `feature` profile.

Because `bugfix` has no `refine` and no `map`, `diagnose` uses the interview skill (package 06) for missing facts. The DoR item DOR-10 "affected areas known" passes with the "affected code" section of `diagnosis.md` as evidence, and DOR-09 "security considerations" passes on the section filled from `diagnose / security` (package 05). **Decided** on 2026-10-18 (findings clarification).

### `plan` variant

- Input: `spec.md`, `diagnosis.md`, and the `diagnose / security` section of `researches.md`.
- Short `plan.md` with four filled sections: Summary (the chosen fix option and why, with a link to `diagnosis.md`), Test strategy (test levels and the quality commands for build, test, lint, and coverage, as in package 12), Risks, and Deviations ("None." while planning; `implement` fills it as in package 13). Every other section says "n/a in bugfix". **Decided** on 2026-10-18 (findings clarification). Usually one to a few tasks.
- The first task in `tasks.md` is always the regression test that reproduces the bug and fails before the fix. **Decided** (failing test first, concept §5.4).

### `implement` variant

- The regression test is written first and must be seen failing for the diagnosed reason before any production code changes. The failing run is recorded in `review.md`.
- Then the loop of package 13 runs unchanged: implement, simplify, review.

## Acceptance criteria

- **FP16-AC1** `diagnose` writes `diagnosis.md` with reproduction, root cause with file evidence, and ruled-out causes.
- **FP16-AC2** `diagnose` stops and asks the human when the bug cannot be reproduced.
- **FP16-AC3** `diagnose` writes a `spec.md` that passes the DoR check, with DOR-10 passed on the "affected code" section of `diagnosis.md` and DOR-09 passed on the `diagnose / security` research.
- **FP16-AC4** In `bugfix`, the first task in `tasks.md` is a failing regression test.
- **FP16-AC5** `review.md` records a failing run of the regression test before the first production code change.
- **FP16-AC6** A `bugfix` feature goes from `new` to `done` with `verify` and `finish` unchanged, and, if a report is written (package 15), it lists the regression test under requirements and tests.
- **FP16-AC7** After `diagnose`, `researches.md` has a `diagnose / security` section.
- **FP16-AC8** The short `plan.md` in `bugfix` has a filled Summary, Test strategy with a command for build, test, lint, and coverage (or the reason one does not apply), Risks, and Deviations; every other section says "n/a in bugfix".

## Open points

- None.
