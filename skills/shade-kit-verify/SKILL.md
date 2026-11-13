---
name: shade-kit-verify
description: SHADE kit phase skill for `verify` (feature and bugfix profiles). Runs the quality checks, confirms that every acceptance criterion is covered by a passing test, and writes verification.md and a playbook.md for manual tests. Use when the user wants to verify or test the implementation of the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-verify`); never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-verify

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/14-verify.md. -->

## Purpose

Proves that the implementation meets the spec: automated checks by the agent, plus a playbook for what a human has to see working.

| Input | Output |
|---|---|
| Code, `spec.md`, `tasks.md`, `review.md`, `plan.md` | `verification.md`, `playbook.md` |

## Flow

1. **Entry protocol:** load `shade-kit-phase-entry` and pass it the phase `verify`. It runs the version handshake, `shade-kit phase start verify --json`, the gate check, and the human's approval, and returns one of two results:
   - **Ended** (`Entry protocol of verify: ended (next-steps message written)`): stop here. Do no phase work and write nothing more; the closing message is already written.
   - **Run the phase:** keep the feature (`featureId`, `profile`) and the warnings of the return for the [ending](#ending). If its gate warning is not `none`, show it to the human once now, before any phase work. The return also names the general skills of the phase; each step below loads its skill when it gets there, not before.
2. **Quality checks:** run the build, test, and lint commands recorded in `review.md` and the coverage command from the test strategy in `plan.md`.
3. **Coverage of criteria:** for every acceptance criterion in `spec.md`, find its proving tests in the coverage matrix of `tasks.md` and confirm that they exist and passed in this run. Criteria marked manual go to the playbook.
4. **Write `verification.md`:** date, commands with results, coverage value, table acceptance criterion → test → result.
5. **Write `playbook.md`:** one unchecked item per manual criterion and per behaviour the human should see working (steps, expected result).
6. **Report** the result. If anything failed, name the failures and recommend reopening `implement`; the closing message lists the reopen. End as described in [Ending](#ending) (end of work).

## Rules

- Never check off playbook items; only a human does.
- `bugfix`: the regression test is one of the proving tests and must pass.

## Ending

Once the entry protocol said **run the phase**, every way this skill ends loads `shade-kit-next-steps` and passes it:

- why the skill ends: **end of work**, with one line of what happened; or **refusal**, with the failure envelope of a CLI call of this skill that failed;
- every warning of the entry protocol's return and of this skill's own CLI calls.

When the report of this skill makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next`, `/shade-kit-discuss`, and `/shade-kit-drop`. The closing message is the end of this skill; write nothing after it.

Never start another phase skill, and never run `shade-kit phase start`, `phase approve`, or `phase reject` yourself: the entry protocol runs them for this phase, and a phase starts only when the human calls its skill.
