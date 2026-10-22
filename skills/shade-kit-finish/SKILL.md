---
name: shade-kit-finish
description: SHADE kit phase skill for `finish` (every profile). Checks the Definition of Done at the entry gate, optionally writes the feature report (requirements, tests, checks, approvals) with shade-kit-report, and sets the feature to done. Use when the user wants to finish, close, or complete the current SHADE kit feature.
---

# shade-kit-finish

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/15-finish-and-report.md. CLI commands other than `shade-kit version`, `shade-kit init`, and `shade-kit feature …` (`new`, `list`, `current`, `set`, `show`, `drop`) are planned and do not exist yet. -->

## Purpose

Closes the feature. The entry gate is the Definition of Done. On request it adds a traceable report: which requirements were built, which tests prove them, which checks passed, and who approved each transition.

| Input | Output |
|---|---|
| Every artifact of the feature | feature status `done`, `report.md` (optional) |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit phase start finish --json`. Stop if the transition is not allowed and report why. If the phase is already in progress, the CLI returns no gate: go to step 5. If a transition away from this phase is pending, the CLI reports it and changes nothing: ask the human whether to discard it. On a yes, call `shade-kit phase reject <pending> --note "resumed finish"` and go to step 5; on a no, hand off to `shade-kit-next-phase` and stop.
   3. If the gate has a checklist (default: Definition of Done): load `shade-kit-gate-check` on it.
   4. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve finish`. On a no, call `shade-kit phase reject finish --note "<gaps>"`, hand off to `shade-kit-next-phase`, and stop. If approval is switched off, a failed checklist is only a warning.
   5. Load the skills the CLI returned.
2. **Report decision:** the result of `shade-kit phase start finish --json` contains the `report` setting from `config.json`: `always` writes a report, `never` skips it, `ask` (default) asks the human.
3. **Report:** if a report is written, load `shade-kit-report` and show its summary to the human.
4. **Complete:** `shade-kit phase complete`. The feature is `done`; its folder stays in the repository.
