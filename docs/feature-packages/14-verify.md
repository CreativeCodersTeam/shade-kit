# 14 · Verify (`verify`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (commands from `plan.md`, coverage matrix) |
| Source | Concept §5.1 (`verify`), §5.2 (DoD), §6 |
| Depends on | 06, 12, 13 |
| Enables | 15 (DoD gate and report) |

## Benefit

Before a feature is called done, there is evidence that every acceptance criterion is covered by a passing test and that build, test, lint, and coverage pass. What a test cannot prove is turned into a short manual playbook that a developer or user works through, so the DoD covers both automated and human checks.

## Scope

### In scope

- The `verify` phase skill in the `feature` profile (and in `bugfix`, which runs it unchanged).
- The AI verification: quality checks and the acceptance criterion → passing test check.
- The `verification.md` and `playbook.md` templates.

### Out of scope

- Quality checks run by the CLI and stored by the engine (later, concept §6).
- The DoD check itself. It runs in the gate into `finish` (packages 05 and 06).
- Fixing failures. A failure sends the feature back to `implement` through a reopen (package 04).

## Functional description

### Run

1. Run the build, test, and lint commands recorded in `review.md` (package 13), plus the coverage command from the test strategy in `plan.md` (package 12), or found the same way as in package 13 where there is no `plan.md`. Record every command in `verification.md`. **Decided** (agent runs them in v1, concept §6).
2. For every acceptance criterion in `spec.md`, find the proving tests in the coverage matrix of `tasks.md` and confirm that they exist and passed in this run. Criteria marked manual in the matrix (package 12) go to the playbook.
3. Write `verification.md`: date, commands with their results, coverage value, and a table acceptance criterion → test → result.
4. Write `playbook.md`: one checkbox item per manual criterion and per behaviour the human should see working (steps, expected result). Every item starts unchecked.
5. Report the result. If anything failed, recommend reopening `implement` and name the failures.

A developer or user works through `playbook.md` and checks the items off. The agent does not check playbook items. **Proposed**, because the DoD item says "checked off by a human".

### Coverage

The coverage value is measured and recorded. The threshold it must reach is **Open**: the concept names coverage as a check but sets no value. Proposal: an optional `coverageThreshold` in `config.json`; without it, `verify` records the value and the DoD item only requires that coverage was measured.

### `bugfix`

`verify` runs unchanged in `bugfix`. The regression test of the `bugfix` profile (package 16) is one of the proving tests and must pass.

## Acceptance criteria

- **FP14-AC1** `verification.md` lists every acceptance criterion of `spec.md` with its test and result, or "manual".
- **FP14-AC2** `verification.md` records every command run and its result, including the coverage value.
- **FP14-AC3** Every criterion marked "manual" has an item in `playbook.md`.
- **FP14-AC4** The agent never checks off a playbook item.
- **FP14-AC5** A failing check or a criterion without a passing test is reported, together with the recommendation to reopen `implement`.

## Open points

- Coverage threshold (see above).
