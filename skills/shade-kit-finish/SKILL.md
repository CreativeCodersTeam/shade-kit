---
name: shade-kit-finish
description: SHADE kit phase skill for `finish` (every profile). Checks the Definition of Done at the entry gate, optionally writes the feature report (requirements, tests, checks, approvals) with shade-kit-report, and sets the feature to done. Use when the user wants to finish, close, or complete the current SHADE kit feature. Runs only when the user calls it by name (`/shade-kit-finish`); never load it on your own or from another skill.
metadata:
  version: 0.7.0
---

# shade-kit-finish

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/15-finish-and-report.md. -->

## Purpose

Closes the feature. The entry gate is the Definition of Done. On request it adds a traceable report: which requirements were built, which tests prove them, which checks passed, and who approved each transition.

| Input | Output |
|---|---|
| Every artifact of the feature | feature status `done`, `report.md` (optional) |

## Flow

1. **Entry protocol:** load `shade-kit-phase-entry` and pass it the phase `finish`. It runs the version handshake, `shade-kit phase start finish --json`, the gate check, and the human's approval, and returns one of two results:
   - **Ended** (`Entry protocol of finish: ended (next-steps message written)`): stop here. Do no phase work and write nothing more; the closing message is already written.
   - **Run the phase:** keep the feature (`featureId`, `profile`) and the warnings of the return for the [ending](#ending). If its gate warning is not `none`, show it to the human once now, before any phase work. The return also names the general skills of the phase; each step below loads its skill when it gets there, not before.
2. **Report decision:** the `report` setting comes from the result of `shade-kit phase start finish --json`, which the entry protocol passes on: `always` writes a report, `never` skips it, `ask` asks the human with the agent's structured question tool (in Claude Code `AskUserQuestion`; without one, a plain question): write a report, or finish without one. When the return carries no `report` setting, treat it as `ask`. Never read `config.json` for it. Without an answer, do not complete the feature: end with **end of work** and the line that `finish` waits for the report decision; calling `/shade-kit-finish` again continues it.
3. **Report:** only if a report is written, load `shade-kit-report`, pass it the feature id, and show its summary to the human. Otherwise do not load it.
4. **Complete:** `shade-kit phase complete --json`. Keep its warnings.
   - Success: the feature is `done`; its folder stays in the repository. End as described in [Ending](#ending) (end of work, with the line that the feature `<featureId>` is done). The closing message names the next feature (`/shade-kit-new`); also when `phase complete` cleared the local override and no feature is current any more.
   - Failure envelope: the feature is not done. End with **refusal** and the envelope.

## Ending

Once the entry protocol said **run the phase**, every way this skill ends loads `shade-kit-next-steps` and passes it:

- why the skill ends: **end of work**, with one line of what happened; or **refusal**, with the failure envelope of a CLI call of this skill that failed;
- every warning of the entry protocol's return and of this skill's own CLI calls.

When the report of this skill makes a recommendation, it names the recommendation and its reason, but a skill only when the closing message does not list it: that message lists the steps of `shade-kit phase next`, `/shade-kit-discuss`, and `/shade-kit-drop`. The closing message is the end of this skill; write nothing after it.

Never start another phase skill, and never run `shade-kit phase start`, `phase approve`, or `phase reject` yourself: the entry protocol runs them for this phase, and a phase starts only when the human calls its skill.
