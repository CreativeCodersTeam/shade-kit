---
name: shade-kit-implement
description: SHADE kit phase skill for `implement` (feature and bugfix profiles). Works through tasks.md in a loop of implement, simplify, and independent review until the review has no open blockers; produces code, tests, and review.md. Bug fixes start with a failing regression test. Use when the user wants to implement the planned tasks of the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-implement`); never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-implement

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/13-implement.md, 16-bugfix-profile.md. -->

## Purpose

Builds the plan task by task and does not stop at "it compiles": every change is simplified and reviewed independently before the feature can go to `verify`.

| Input | Output |
|---|---|
| `plan.md`, `tasks.md`, `spec.md` | Code and tests, updated `tasks.md` and `plan.md` (deviations), `review.md` |

## Flow

1. **Entry protocol:** load `shade-kit-phase-entry` and pass it the phase `implement`. It runs the version handshake, `shade-kit phase start implement --json`, the gate check, and the human's approval, and returns one of two results:
   - **Ended** (`Entry protocol of implement: ended (next-steps message written)`): stop here. Do no phase work and write nothing more; the closing message is already written.
   - **Run the phase:** keep the feature (`featureId`, `profile`) and the warnings of the return for the [ending](#ending). If its gate warning is not `none`, show it to the human once now, before any phase work. The return also names the general skills of the phase; each step below loads its skill when it gets there, not before.
2. **Implement:** work through the open tasks in order, respecting "Depends on". Tasks marked "Parallel: yes" may run in parallel subagents where the agent supports them. Per task: write code and proving tests, run build and tests with the commands from the test strategy, check off tests and the task. A task that cannot be finished gets a `Not done:` note. Record deviations in `plan.md` and keep the coverage matrix in sync.
3. **Simplify (inline):** go over the changed code and remove duplication, needless complexity, and dead code without changing behaviour. Tests stay green.
4. **Review:** load `shade-kit-review`. Findings go into `review.md`.
5. **Loop:** while the review has open blockers, fix them and repeat from step 2. After three rounds with open blockers, ask the human how to continue: go on, accept a finding, or end here to reopen `plan`. Do not reopen `plan` yourself: on "reopen `plan`", report the open blockers, recommend reopening `plan`, and end (end of work); the closing message lists the reopen.
6. **Report** the result and the commands used (recorded in `review.md`). End as described in [Ending](#ending) (end of work).

## Profile `bugfix`

Write the regression test first and see it fail for the diagnosed reason before any production code changes. Record the failing run in `review.md`. Then run the loop unchanged.

## Ending

Once the entry protocol said **run the phase**, every way this skill ends loads `shade-kit-next-steps` and passes it:

- why the skill ends: **end of work**, with one line of what happened; or **refusal**, with the failure envelope of a CLI call of this skill that failed;
- every warning of the entry protocol's return and of this skill's own CLI calls.

When the report of this skill makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next`, `/shade-kit-discuss`, and `/shade-kit-drop`. The closing message is the end of this skill; write nothing after it.

Never start another phase skill, and never run `shade-kit phase start`, `phase approve`, or `phase reject` yourself: the entry protocol runs them for this phase, and a phase starts only when the human calls its skill.
