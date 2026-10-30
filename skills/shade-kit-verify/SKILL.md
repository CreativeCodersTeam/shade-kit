---
name: shade-kit-verify
description: SHADE kit phase skill for `verify` (feature and bugfix profiles). Runs the quality checks, confirms that every acceptance criterion is covered by a passing test, and writes verification.md and a playbook.md for manual tests. Use when the user wants to verify or test the implementation of the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-verify`); never load it on your own or from another skill.
---

# shade-kit-verify

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/14-verify.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Proves that the implementation meets the spec: automated checks by the agent, plus a playbook for what a human has to see working.

| Input | Output |
|---|---|
| Code, `spec.md`, `tasks.md`, `review.md`, `plan.md` | `verification.md`, `playbook.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start verify --json`. This is the check: it changes nothing when it refuses or when the phase is already active.
      - Refused: end with the reason and the next steps (see Ending).
      - Already in progress (no gate): tell the user that `verify` is already active and ask: continue it (go to step 5) or end with the next steps.
      - A transition away from this phase is pending: tell the user and ask: discard it and continue (`shade-kit phase reject <pending> --note "resumed verify"`, then go to step 5), keep it (end and name `/shade-kit-<pending>`, which decides on it), or end with the next steps.
      - The transition into `verify` was requested earlier and is still pending: say so and go on with the returned gate.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve verify`. On a no, call `shade-kit phase reject verify --note "<gaps>"` and end with the gaps and the next steps (see Ending). If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Quality checks:** run the build, test, and lint commands recorded in `review.md` and the coverage command from the test strategy in `plan.md`.
3. **Coverage of criteria:** for every acceptance criterion in `spec.md`, find its proving tests in the coverage matrix of `tasks.md` and confirm that they exist and passed in this run. Criteria marked manual go to the playbook.
4. **Write `verification.md`:** date, commands with results, coverage value, table acceptance criterion → test → result.
5. **Write `playbook.md`:** one unchecked item per manual criterion and per behaviour the human should see working (steps, expected result).
6. **Report** the result. If anything failed, recommend reopening `implement` (`/shade-kit-implement`) and name the failures. End with the next steps (see Ending).

## Rules

- Never check off playbook items; only a human does.
- `bugfix`: the regression test is one of the proving tests and must pass.

## Ending

- End with the next steps: what the user can do now and the skill they call for each, from `shade-kit phase next --json` (continue the current phase, the pending transition, the next phase, reopen an earlier phase, each as `/shade-kit-<phase>`), plus `/shade-kit-discuss` and `/shade-kit-drop`. After a refused start, name the reason from the CLI first; without a current feature, name `shade-kit feature list`, `shade-kit feature set <id>`, and `/shade-kit-new`.
- Never start another phase skill and never call `shade-kit phase start` for another phase: a phase starts only when the user calls its skill.
