---
name: shade-kit-implement
description: SHADE kit phase skill for `implement` (feature and bugfix profiles). Works through tasks.md in a loop of implement, simplify, and independent review until the review has no open blockers; produces code, tests, and review.md. Bug fixes start with a failing regression test. Use when the user wants to implement the planned tasks of the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-implement`); never load it on your own or from another skill.
---

# shade-kit-implement

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/13-implement.md, 16-bugfix-profile.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Builds the plan task by task and does not stop at "it compiles": every change is simplified and reviewed independently before the feature can go to `verify`.

| Input | Output |
|---|---|
| `plan.md`, `tasks.md`, `spec.md` | Code and tests, updated `tasks.md` and `plan.md` (deviations), `review.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start implement --json`. This is the check: it changes nothing when it refuses or when the phase is already active.
      - Refused: end with the reason and the next steps (see Ending).
      - Already in progress (no gate): tell the user that `implement` is already active and ask: continue it (go to step 5) or end with the next steps.
      - A transition away from this phase is pending: tell the user and ask: discard it and continue (`shade-kit phase reject <pending> --note "resumed implement"`, then go to step 5), keep it (end and name `/shade-kit-<pending>`, which decides on it), or end with the next steps.
      - The transition into `implement` was requested earlier and is still pending: say so and go on with the returned gate.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve implement`. On a no, call `shade-kit phase reject implement --note "<gaps>"` and end with the gaps and the next steps (see Ending). If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Implement:** work through the open tasks in order, respecting "Depends on". Tasks marked "Parallel: yes" may run in parallel subagents where the agent supports them. Per task: write code and proving tests, run build and tests with the commands from the test strategy, check off tests and the task. A task that cannot be finished gets a `Not done:` note. Record deviations in `plan.md` and keep the coverage matrix in sync.
3. **Simplify (inline):** go over the changed code and remove duplication, needless complexity, and dead code without changing behaviour. Tests stay green.
4. **Review:** load `shade-kit-review`. Findings go into `review.md`.
5. **Loop:** while the review has open blockers, fix them and repeat from step 2. After three rounds with open blockers, ask the human how to continue: go on, accept a finding, or end here to reopen `plan`. Do not reopen `plan` yourself: on "reopen `plan`", end and name `/shade-kit-plan`.
6. **Report** the result and the commands used (recorded in `review.md`). End with the next steps (see Ending).

## Profile `bugfix`

Write the regression test first and see it fail for the diagnosed reason before any production code changes. Record the failing run in `review.md`. Then run the loop unchanged.

## Ending

- End with the next steps: what the user can do now and the skill they call for each, from `shade-kit phase next --json` (continue the current phase, the pending transition, the next phase, reopen an earlier phase, each as `/shade-kit-<phase>`), plus `/shade-kit-discuss` and `/shade-kit-drop`. After a refused start, name the reason from the CLI first; without a current feature, name `shade-kit feature list`, `shade-kit feature set <id>`, and `/shade-kit-new`.
- Never start another phase skill and never call `shade-kit phase start` for another phase: a phase starts only when the user calls its skill.
