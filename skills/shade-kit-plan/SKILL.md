---
name: shade-kit-plan
description: SHADE kit phase skill for `plan` (feature and bugfix profiles). Writes plan.md with design decisions and test strategy and tasks.md with the requirement-to-test mapping and coverage matrix; short variant for bug fixes. Use when the user wants to plan the implementation of the current SHADE kit feature.
---

# shade-kit-plan

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/12-plan.md, 16-bugfix-profile.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Decides how the spec is built and why, and binds every acceptance criterion to the test that will prove it. The entry gate is the Definition of Ready.

| Input | Output |
|---|---|
| `spec.md`, `researches.md` (`bugfix`: `diagnosis.md` and the `diagnose / security` section) | `plan.md`, `tasks.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start plan --json`. Stop if the transition is not allowed and report why. If the phase is already in progress, the CLI returns no gate: go to step 5. If a transition away from this phase is pending, the CLI reports it and changes nothing: ask the human whether to discard it. On a yes, call `shade-kit phase reject <pending> --note "resumed plan"` and go to step 5; on a no, hand off to `shade-kit-next-phase` and stop.
   3. If the gate has a checklist (default: Definition of Ready): load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve plan`. On a no, call `shade-kit phase reject plan --note "<gaps>"`, hand off to `shade-kit-next-phase`, and stop. If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Read** `spec.md` and `researches.md`.
3. **Clarify** open design choices with `shade-kit-interview`, each with an own recommendation.
4. **Write `plan.md`** from the template: summary, technical context, approach, affected components, design decisions (`D-` IDs), test strategy with the quality commands (build, test, lint, coverage), migration and rollout, risks, deviations ("None." while planning).
5. **Write `tasks.md`** from the template: tasks (`T-` IDs) in work order with requirements, dependencies, parallelism, affected paths, and proving tests, plus the coverage matrix.
6. **Check the mapping** and report gaps: every active requirement is in a task; every acceptance criterion has a planned test or is marked manual with a reason.
7. **Report** the plan summary and the quality commands the human approves with the plan. Continue with `shade-kit-next-phase`.

## Profile `bugfix`

- Input is `spec.md`, `diagnosis.md`, and the `diagnose / security` section of `researches.md`.
- Short `plan.md`: fill Summary (the chosen fix option and why, linking `diagnosis.md`), Test strategy (with the quality commands for build, test, lint, and coverage), Risks, and Deviations ("None."); every other section says "n/a in bugfix". Usually one to a few tasks.
- The first task is always the regression test that reproduces the bug and fails before the fix.
