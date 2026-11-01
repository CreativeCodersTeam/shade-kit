---
name: shade-kit-plan
description: SHADE kit phase skill for `plan` (feature and bugfix profiles). Writes plan.md with design decisions and test strategy and tasks.md with the requirement-to-test mapping and coverage matrix; short variant for bug fixes. Use when the user wants to plan the implementation of the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-plan`); never load it on your own or from another skill.
---

# shade-kit-plan

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/12-plan.md, 16-bugfix-profile.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Decides how the spec is built and why, and binds every acceptance criterion to the test that will prove it. The entry gate is the Definition of Ready.

| Input | Output |
|---|---|
| `spec.md`, `researches.md` (`bugfix`: `diagnosis.md` and the `diagnose / security` section) | `plan.md`, `tasks.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start plan --json`. This is the check: it changes nothing when it refuses or when the phase is already active.
      - Refused: end with the reason and the next steps (see Ending).
      - Already in progress (no gate): tell the user that `plan` is already active and ask: continue it (go to step 5) or end with the next steps.
      - A transition away from this phase is pending: tell the user and ask: discard it and continue (`shade-kit phase reject <pending> --note "resumed plan"`, then go to step 5), keep it (end and name `/shade-kit-<pending>`, which decides on it), or end with the next steps.
      - The transition into `plan` was requested earlier and is still pending: say so and go on with the returned gate.
   3. If the gate has a checklist (default: Definition of Ready): load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve plan`. On a no, call `shade-kit phase reject plan --note "<gaps>"` and end with the gaps and the next steps (see Ending). If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Read** `spec.md` and `researches.md`.
3. **Clarify** open design choices with `shade-kit-interview`, each with an own recommendation.
4. **Write `plan.md`** from the template: summary, technical context, approach, affected components, design decisions (`D-` IDs), test strategy with the quality commands (build, test, lint, coverage), migration and rollout, risks, deviations ("None." while planning).
5. **Write `tasks.md`** from the template: tasks (`T-` IDs) in work order with requirements, dependencies, parallelism, affected paths, and proving tests, plus the coverage matrix.
6. **Check the mapping** and report gaps: every active requirement is in a task; every acceptance criterion has a planned test or is marked manual with a reason.
7. **Report** the plan summary and the quality commands the human approves with the plan. End with the next steps (see Ending).

## Profile `bugfix`

- Input is `spec.md`, `diagnosis.md`, and the `diagnose / security` section of `researches.md`.
- Short `plan.md`: fill Summary (the chosen fix option and why, linking `diagnosis.md`), Test strategy (with the quality commands for build, test, lint, and coverage), Risks, and Deviations ("None."); every other section says "n/a in bugfix". Usually one to a few tasks.
- The first task is always the regression test that reproduces the bug and fails before the fix.

## Ending

- End with the next steps: what the user can do now and the skill they call for each, from `shade-kit phase next --json` (continue the current phase, the pending transition, the next phase, reopen an earlier phase, each as `/shade-kit-<phase>`), plus `/shade-kit-discuss` and `/shade-kit-drop`. After a refused start, name the reason from the CLI first; without a current feature, name `shade-kit feature list`, `shade-kit feature set <id>`, and `/shade-kit-new`.
- Never start another phase skill and never call `shade-kit phase start` for another phase: a phase starts only when the user calls its skill.
