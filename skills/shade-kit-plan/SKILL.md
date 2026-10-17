---
name: shade-kit-plan
description: SHADE kit phase skill for `plan` (feature and bugfix profiles). Writes plan.md with design decisions and test strategy and tasks.md with the requirement-to-test mapping and coverage matrix; short variant for bug fixes. Use when the user wants to plan the implementation of the current SHADE kit feature.
---

# shade-kit-plan

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/12-plan.md, 16-bugfix-profile.md. CLI commands other than `shade-kit version` and `shade-kit features list` are planned and do not exist yet. -->

## Purpose

Decides how the spec is built and why, and binds every acceptance criterion to the test that will prove it. The entry gate is the Definition of Ready.

| Input | Output |
|---|---|
| `spec.md`, `researches.md` (`bugfix`: `diagnosis.md`) | `plan.md`, `tasks.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start plan --json`. Stop if the transition is not allowed and report why.
   3. If the gate has a checklist (default: Definition of Ready): load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve plan`.
   5. Load the skills the CLI returned.
2. **Read** `spec.md` and `researches.md`.
3. **Clarify** open design choices with `shade-kit-interview`, each with an own recommendation.
4. **Write `plan.md`** from the template: summary, technical context, approach, affected components, design decisions (`D-` IDs), test strategy with the quality commands (build, test, lint, coverage), migration and rollout, risks, deviations ("None." while planning).
5. **Write `tasks.md`** from the template: tasks (`T-` IDs) in work order with requirements, dependencies, parallelism, affected paths, and proving tests, plus the coverage matrix.
6. **Check the mapping** and report gaps: every active requirement is in a task; every acceptance criterion has a planned test or is marked manual with a reason.
7. **Report** the plan summary and the quality commands the human approves with the plan. Continue with `shade-kit-next-phase`.

## Profile `bugfix`

- Input is `spec.md` and `diagnosis.md` instead of `researches.md`.
- Short `plan.md`: the chosen fix option and why, risks. Usually one to a few tasks.
- The first task is always the regression test that reproduces the bug and fails before the fix.
