---
name: shade-kit-verify
description: SHADE kit phase skill for `verify` (feature and bugfix profiles). Runs the quality checks, confirms that every acceptance criterion is covered by a passing test, and writes verification.md and a playbook.md for manual tests. Use when the user wants to verify or test the implementation of the current SHADE kit feature.
---

# shade-kit-verify

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/14-verify.md. CLI commands other than `shade-kit version` and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Proves that the implementation meets the spec: automated checks by the agent, plus a playbook for what a human has to see working.

| Input | Output |
|---|---|
| Code, `spec.md`, `tasks.md`, `review.md`, `plan.md` | `verification.md`, `playbook.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start verify --json`. Stop if the transition is not allowed and report why. If the phase is already in progress, the CLI returns no gate: go to step 5. If a transition away from this phase is pending, the CLI reports it and changes nothing: ask the human whether to discard it. On a yes, call `shade-kit phase reject <pending> --note "resumed verify"` and go to step 5; on a no, hand off to `shade-kit-next-phase` and stop.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve verify`. On a no, call `shade-kit phase reject verify --note "<gaps>"`, hand off to `shade-kit-next-phase`, and stop. If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Quality checks:** run the build, test, and lint commands recorded in `review.md` and the coverage command from the test strategy in `plan.md`.
3. **Coverage of criteria:** for every acceptance criterion in `spec.md`, find its proving tests in the coverage matrix of `tasks.md` and confirm that they exist and passed in this run. Criteria marked manual go to the playbook.
4. **Write `verification.md`:** date, commands with results, coverage value, table acceptance criterion → test → result.
5. **Write `playbook.md`:** one unchecked item per manual criterion and per behaviour the human should see working (steps, expected result).
6. **Report** the result. If anything failed, recommend reopening `implement` and name the failures. Continue with `shade-kit-next-phase`.

## Rules

- Never check off playbook items; only a human does.
- `bugfix`: the regression test is one of the proving tests and must pass.
