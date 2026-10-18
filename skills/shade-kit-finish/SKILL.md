---
name: shade-kit-finish
description: SHADE kit phase skill for `finish` (every profile). Checks the Definition of Done at the entry gate, writes the feature report (requirements, tests, checks, approvals), and sets the feature to done. Use when the user wants to finish, close, or complete the current SHADE kit feature.
---

# shade-kit-finish

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/15-finish-and-report.md. CLI commands other than `shade-kit version` and `shade-kit feature list` are planned and do not exist yet. -->

## Purpose

Closes the feature with a traceable record: which requirements were built, which tests prove them, which checks passed, and who approved each transition. The entry gate is the Definition of Done.

| Input | Output |
|---|---|
| Every artifact of the feature | `report.md`, feature status `done` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start finish --json`. Stop if the transition is not allowed and report why. If the phase is already in progress, the CLI returns no gate: go to step 5.
   3. If the gate has a checklist (default: Definition of Done): load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve finish`. On a no, call `shade-kit phase reject finish --note "<gaps>"`, hand off to `shade-kit-next-phase`, and stop. If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Read state:** `shade-kit feature show <id> --json` (profile, phases, approval history).
3. **Read artifacts** the profile produced: `spec.md`, `tasks.md`, `review.md`, `verification.md`, `playbook.md`, `checks/definition-of-done.md`, and in `bugfix` `diagnosis.md`.
4. **Write `report.md` (inline)** from the template: summary, requirements and tests, checks, review, manual checks, deviations, approvals. A section whose artifact the profile does not have says so in one line.
5. **Show** the report summary to the human.
6. **Complete:** `shade-kit phase complete`. The feature is `done`; its folder stays in the repository.
