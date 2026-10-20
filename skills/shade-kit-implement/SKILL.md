---
name: shade-kit-implement
description: SHADE kit phase skill for `implement` (feature and bugfix profiles). Works through tasks.md in a loop of implement, simplify, and independent review until the review has no open blockers; produces code, tests, and review.md. Bug fixes start with a failing regression test. Use when the user wants to implement the planned tasks of the current SHADE kit feature.
---

# shade-kit-implement

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/13-implement.md, 16-bugfix-profile.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Builds the plan task by task and does not stop at "it compiles": every change is simplified and reviewed independently before the feature can go to `verify`.

| Input | Output |
|---|---|
| `plan.md`, `tasks.md`, `spec.md` | Code and tests, updated `tasks.md` and `plan.md` (deviations), `review.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start implement --json`. Stop if the transition is not allowed and report why. If the phase is already in progress, the CLI returns no gate: go to step 5. If a transition away from this phase is pending, the CLI reports it and changes nothing: ask the human whether to discard it. On a yes, call `shade-kit phase reject <pending> --note "resumed implement"` and go to step 5; on a no, hand off to `shade-kit-next-phase` and stop.
   3. If the gate has a checklist: load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve implement`. On a no, call `shade-kit phase reject implement --note "<gaps>"`, hand off to `shade-kit-next-phase`, and stop. If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Implement:** work through the open tasks in order, respecting "Depends on". Tasks marked "Parallel: yes" may run in parallel subagents where the agent supports them. Per task: write code and proving tests, run build and tests with the commands from the test strategy, check off tests and the task. A task that cannot be finished gets a `Not done:` note. Record deviations in `plan.md` and keep the coverage matrix in sync.
3. **Simplify (inline):** go over the changed code and remove duplication, needless complexity, and dead code without changing behaviour. Tests stay green.
4. **Review:** load `shade-kit-review`. Findings go into `review.md`.
5. **Loop:** while the review has open blockers, fix them and repeat from step 2. After three rounds with open blockers, ask the human how to continue (go on, accept a finding, or reopen `plan`).
6. **Report** the result and the commands used (recorded in `review.md`). Continue with `shade-kit-next-phase`.

## Profile `bugfix`

Write the regression test first and see it fail for the diagnosed reason before any production code changes. Record the failing run in `review.md`. Then run the loop unchanged.
