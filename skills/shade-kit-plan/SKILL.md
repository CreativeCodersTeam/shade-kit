---
name: shade-kit-plan
description: SHADE kit phase skill for `plan` (feature and bugfix profiles). Writes plan.md with design decisions and test strategy and tasks.md with the requirement-to-test mapping and coverage matrix; short variant for bug fixes. Use when the user wants to plan the implementation of the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-plan`); never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-plan

<!-- Skeleton: purpose and flow only, details follow. Sources: docs/feature-packages/12-plan.md, 16-bugfix-profile.md. -->

## Purpose

Decides how the spec is built and why, and binds every acceptance criterion to the test that will prove it. The entry gate is the Definition of Ready.

| Input | Output |
|---|---|
| `spec.md`, `researches.md` (`bugfix`: `diagnosis.md` and the `diagnose / security` section) | `plan.md`, `tasks.md` |

## Flow

1. **Entry protocol:** load `shade-kit-phase-entry` and pass it the phase `plan`. It runs the version handshake, `shade-kit phase start plan --json`, the gate check, and the human's approval, and returns one of two results:
   - **Ended** (`Entry protocol of plan: ended (next-steps message written)`): stop here. Do no phase work and write nothing more; the closing message is already written.
   - **Run the phase:** keep the feature (`featureId`, `profile`) and the warnings of the return for the [ending](#ending). If its gate warning is not `none`, show it to the human once now, before any phase work. The return also names the general skills of the phase; each step below loads its skill when it gets there, not before.
2. **Read** `spec.md` and `researches.md`.
3. **Clarify** open design choices: load `shade-kit-interview` and pass it:
   - mode `targeted`;
   - topics: the design choices that have a real alternative and that `spec.md` and `researches.md` leave open;
   - target artifact: `plan.md` "Design decisions" (`bugfix`: the Summary, for the choice of the fix option);
   - recording rule: one `D-nnn` subsection per answered choice, with context, decision, the rejected alternatives with the reason, and consequences;
   - recording time `on return`: `plan.md` is written in step 4, from the return;
   - artifacts to check against: `spec.md` and `researches.md` (`bugfix`: also `diagnosis.md`).

   Name the choices still open in the report of step 7.
4. **Write `plan.md`** from the template: summary, technical context, approach, affected components, design decisions (`D-` IDs), test strategy with the quality commands (build, test, lint, coverage), migration and rollout, risks, deviations ("None." while planning).
5. **Write `tasks.md`** from the template: tasks (`T-` IDs) in work order with requirements, dependencies, parallelism, affected paths, and proving tests, plus the coverage matrix.
6. **Check the mapping** and report gaps: every active requirement is in a task; every acceptance criterion has a planned test or is marked manual with a reason.
7. **Report** the plan summary and the quality commands the human approves with the plan. End as described in [Ending](#ending) (end of work).

## Profile `bugfix`

- Input is `spec.md`, `diagnosis.md`, and the `diagnose / security` section of `researches.md`.
- Short `plan.md`: fill Summary (the chosen fix option and why, linking `diagnosis.md`), Test strategy (with the quality commands for build, test, lint, and coverage), Risks, and Deviations ("None."); every other section says "n/a in bugfix". Usually one to a few tasks.
- The first task is always the regression test that reproduces the bug and fails before the fix.

## Ending

Once the entry protocol said **run the phase**, every way this skill ends loads `shade-kit-next-steps` and passes it:

- why the skill ends: **end of work**, with one line of what happened; or **refusal**, with the failure envelope of a CLI call of this skill that failed;
- every warning of the entry protocol's return and of this skill's own CLI calls.

When the report of this skill makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next`, `/shade-kit-discuss`, and `/shade-kit-drop`. The closing message is the end of this skill; write nothing after it.

Never start another phase skill, and never run `shade-kit phase start`, `phase approve`, or `phase reject` yourself: the entry protocol runs them for this phase, and a phase starts only when the human calls its skill.
