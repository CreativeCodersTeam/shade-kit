# 06-4 · Checklist check (`shade-kit-gate-check`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
| Source | [06 Skill foundation](06-skill-foundation.md) (general skills), [05 Gates and checklists](05-gates-and-checklists.md) (checklist format, "not applicable" rule) |
| Kind | Skill (general skill, skeleton exists) |
| Depends on | 06-2 |
| Enables | 06-5 |

## Benefit

The human approves a phase transition on a checked basis: every item of the Definition of Ready or Definition of Done has a result and the evidence for it, and every check run is logged in the feature folder.

## Scope

### In scope

- The general skill `shade-kit-gate-check`, loaded by the entry protocol (06-5) for every gate with a checklist.

### Out of scope

- The approval itself (06-5).
- Running build, test, lint, and coverage commands: the items that need them are worked out with the phases that own them (packages 12 and 14).

## Functional description

### Input from the calling skill

- The path of the checklist copy in the feature folder, as returned by `shade-kit phase start <phase> --json` (for example `checks/definition-of-ready.md`).
- The feature's profile.

### Flow

1. Read the checklist copy and the artifacts its items refer to.
2. Check every item (`DOR-nn`, `DOD-nn`, …) and set its `Result` (package 05 rule):
   - `pass` or `fail`, with `Evidence` naming what was checked (IDs, paths, command results) or what is missing.
   - `n/a` when the item refers to a phase or artifact the profile does not have, with the reason in `Evidence`.
3. Add one line to the "Check runs" log of the checklist copy (format in package 05): date and time, overall `pass` or `fail`, failed item IDs.
4. Return to the calling skill: overall result and the failed items with their evidence.

### Rules

- `Result` and `Evidence` are overwritten on every run; earlier "Check runs" lines are never deleted.
- The skill changes no artifact other than the checklist copy, and never calls `shade-kit phase approve`.
- The checklist copy keeps its guidance comments (06-2).

## Acceptance criteria

- **FP06-4-AC1** The checklist check marks an item that refers to an artifact the profile does not have as `n/a` with a reason.
- **FP06-4-AC2** After a run, every item has a `Result` of `pass`, `fail`, or `n/a` and an `Evidence`, and the "Check runs" log has one more line while the earlier lines are unchanged.
- **FP06-4-AC3** The skill changes no file other than the checklist copy and calls no `phase` command.

## Open points

None.
