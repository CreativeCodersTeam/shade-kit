---
name: shade-kit-gate-check
description: SHADE kit general skill that checks a gate checklist (Definition of Ready, Definition of Done) against the feature artifacts, fills in result and evidence per item, and reports pass or fail. Loaded by the entry protocol of every SHADE kit phase skill; not meant to be called directly.
---

# shade-kit-gate-check

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/05-gates-and-checklists.md. -->

## Purpose

Gives the human a checked, evidenced basis for the approval of a phase transition. This skill only checks and reports; it never approves.

## Input from the calling skill

- The path of the checklist copy in the feature folder, as returned by `shade-kit phase start <phase> --json` (for example `checks/definition-of-ready.md`).
- The feature's profile.

## Flow

1. **Read** the checklist copy and the artifacts its items refer to.
2. **Check** every item (`DOR-nn`, `DOD-nn`, …) and set `Result`:
   - `pass` or `fail`, with `Evidence` naming what was checked (IDs, paths, command results) or what is missing.
   - `n/a` when the item refers to a phase or artifact the profile does not have, with the reason in `Evidence`.
3. **Log** one line in the "Check runs" section: date and time, overall `pass` or `fail`, failed item IDs.
4. **Return** to the calling skill: overall result and the failed items with their evidence.

## Rules

- Overwrite `Result` and `Evidence` on every run; never delete earlier "Check runs" lines.
- Do not change any other artifact and never call `shade-kit phase approve`.
