# 05 · Gates and checklists

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (checklist format and default items) and 2026-10-18 (`quick` phase; DOR-10, DOD-01, items that do not apply) |
| Source | Concept §2 (Gates), §4.1 (`checks/`), §5.2, §6, §11.1, §11.4 |
| Depends on | 02, 04 |
| Enables | 12 (DoR), 15 (DoD), every phase transition |

## Benefit

A team decides per repository how strict the workflow is: which transitions need a human approval and which transitions must pass a checklist first. The Definition of Ready and Definition of Done are written down, can be edited by the team, and leave a filled-in record in every feature folder.

## Scope

### In scope

- `gates.json`: shape, defaults per profile, switching approval and checklist on or off.
- The checklist templates `checks/definition-of-ready.md` and `checks/definition-of-done.md` and their default content.
- Creating the feature's own copy of a checklist when its phase starts.
- The gate information in the `phase start` result (package 04).
- The rule for checklist items that do not apply to a profile.

### Out of scope

- Checking a checklist. The agent does that with the checklist-check skill (package 06). The engine does not parse checklists (concept §9.5, package 02).
- Quality checks run by the CLI and check commands configured in `gates.json` (later, concept §6).
- Approval enforcement (later, package 04 "Known weak point").

## Functional description

### Gate model

- A gate belongs to a transition and is identified by its target phase. **Decided** (concept §5.2).
- It is checked when the next phase starts. **Decided** (concept §5.2).
- It has two independent switches: `approval` (a human must approve) and `checklist` (path of a checklist the agent must check first, or none). **Decided** (concept §5.2) that each gate can be switched off; switching off approval and checklist independently answers concept open question 4, **Decided** (confirmation list, 2026-10-18).
- DoR is the checklist of the gate into `plan`. DoD is the checklist of the gate into `finish`. All other gates have no checklist by default. **Decided** (concept §5.2).
- Any transition can get a checklist by pointing its gate to a `checks/<phase>.md` template. **Decided** as an extension point (concept §5.2).

### `gates.json`

Written by `init` (package 02), owned by the team for its values, versioned like every JSON file (package 01).

The concept's example is keyed by phase only. Because the `quick` profile needs different defaults than `feature` (one approval at the end), gates are keyed by profile and then by target phase. **Decided** (confirmation list, 2026-10-18); the concept now describes this shape.

```json
{
  "schemaVersion": 1,
  "profiles": {
    "feature": {
      "refine":    { "approval": true },
      "map":       { "approval": true },
      "plan":      { "approval": true, "checklist": "checks/definition-of-ready.md" },
      "implement": { "approval": true },
      "verify":    { "approval": true },
      "finish":    { "approval": true, "checklist": "checks/definition-of-done.md" }
    },
    "bugfix": {
      "diagnose":  { "approval": true },
      "plan":      { "approval": true, "checklist": "checks/definition-of-ready.md" },
      "implement": { "approval": true },
      "verify":    { "approval": true },
      "finish":    { "approval": true, "checklist": "checks/definition-of-done.md" }
    },
    "quick": {
      "quick":     { "approval": false },
      "finish":    { "approval": true, "checklist": "checks/definition-of-done.md" }
    }
  }
}
```

- A transition with no entry uses `{ "approval": true }`. Approval is the safe default. **Decided** (confirmation list, 2026-10-18).
- With `approval: false`, `phase start` performs the transition at once and records it in `history` with the note "approval off". **Proposed.**
- With `approval: false` and a `checklist`, the checklist copy is still created and checked by the agent, but a failed item is only reported as a warning; it does not block, because nobody is asked to decide. An agent never approves on its own instead. **Decided** (confirmation list, 2026-10-18).
- The defaults follow concept §5.2: every transition needs an approval, except the `quick` profile's transition into the `quick` phase (concept §5.4: "one approval at the end").

### Checklists

- The team edits the templates in `.shadekit/templates/checks/`. They are tracked by `templates.lock.json` like every template (package 02).
- When a phase with a checklist starts, the engine copies the template into the feature folder (`features/<id>/checks/definition-of-ready.md`) if the copy does not exist yet, and returns its path. The agent checks and fills in this copy. The filled copy is the record of the gate. **Proposed.**
- A reopened phase (package 04) reuses the existing copy; the agent rechecks every item, and the "Check runs" log keeps the earlier results visible.

### Checklist format

**Decided** in the template interview on 2026-10-17; the general template conventions are in package 02.

- One table per checklist: `ID | Criterion | Result | Evidence`.
- Item IDs are `DOR-nn` and `DOD-nn`. Teams may add, change, or remove items; existing IDs stay stable.
- `Result` is `pending` in the template and becomes `pass`, `fail`, or `n/a` (see "Items that do not apply"). A table is used instead of checkboxes because a checkbox cannot express "not applicable".
- `Evidence` names what was checked, with IDs, paths, or command results; for `fail`, what is missing.
- On every run the agent overwrites `Result` and `Evidence` and adds one line to the "Check runs" section: date and time, `pass` or `fail`, and the failed item IDs.

### Default content

**Decided** in the template interview on 2026-10-17. It contains the four items per checklist proposed in concept §5.2 (DOR-01, DOR-04, DOR-06, DOR-10; DOD-01/02, DOD-03, DOD-05, DOD-09) and adds the rest.

| ID | Definition of Ready (gate into `plan`) |
|---|---|
| DOR-01 | Every requirement in `spec.md` has an ID (`REQ-nnn`) and at least one acceptance criterion. |
| DOR-02 | Every acceptance criterion can be decided by an automated test, or is marked "(manual)" with a reason. |
| DOR-03 | Every requirement has a priority: must, should, or could. |
| DOR-04 | `spec.md` has no open questions: "Open questions" says "None." and no `(open: Q-nnn)` marker is left. |
| DOR-05 | `spec.md` has no template leftovers: no `{{placeholder}}` and no guidance comment. |
| DOR-06 | "Out of scope" is stated, also when nothing is excluded. |
| DOR-07 | Every assumption is confirmed by a human or marked as a risk. |
| DOR-08 | Every edge case has an acceptance criterion or is listed in "Out of scope". |
| DOR-09 | "Security considerations" lists the findings, or says "None identified." with a reason. |
| DOR-10 | The affected areas are known: `map` is done, was skipped on purpose with a recorded reason, or `diagnosis.md` lists the affected code. |

| ID | Definition of Done (gate into `finish`) |
|---|---|
| DOD-01 | Build, test, and lint pass with the quality commands recorded in `review.md`, as recorded in `verification.md`. |
| DOD-02 | Coverage was measured and recorded in `verification.md`. If `config.json` sets `coverageThreshold`, the value reaches it. |
| DOD-03 | Every acceptance criterion is covered by a passing test, or is marked manual and has an item in `playbook.md`. |
| DOD-04 | Every task in `tasks.md` is done, or has a "Not done" reason that the human accepted. |
| DOD-05 | `review.md` has no open blocker. |
| DOD-06 | The quality bar was not lowered: no new analyzer or lint suppressions, skipped or deleted tests, or weakened assertions without a reason accepted in `review.md`. |
| DOD-07 | Every deviation from the plan is recorded in `plan.md` with a reason, and every migration or rollout step is done or handed over. |
| DOD-08 | The documentation matches the change (for example README, changelog, API docs), or Evidence states why no update is needed. |
| DOD-09 | Every item in `playbook.md` is checked off by a human. |

### Items that do not apply

A checklist item that refers to a phase or artifact the feature's profile does not have is marked `n/a` with the reason in `Evidence`, and does not fail the gate. Examples: DOD-04 (`tasks.md`) and DOD-07 (`plan.md`) in `quick`, and DOD-09 (`playbook.md`) in `quick` when no criterion is manual (package 17). In `bugfix`, DOR-10 is not `n/a`: it passes on the "affected code" section of `diagnosis.md` (package 16). **Proposed.** This keeps one DoR and one DoD for all profiles instead of a checklist per profile.

### `phase start` result

The gate part of the result contains: target phase, `approval` (true or false), `checklist` (path in the feature folder, or none), whether the transition was already performed because approval is off, and whether the phase was already in progress (re-entry, package 04; then there is no gate).

## Acceptance criteria

- **FP05-AC1** `init` writes `gates.json` with the defaults above and `checks/definition-of-ready.md` and `checks/definition-of-done.md` with the default items.
- **FP05-AC2** `phase start plan` in a `feature` creates `features/<id>/checks/definition-of-ready.md` from the template and returns its path.
- **FP05-AC3** A second `phase start plan` after a reopen keeps the existing feature checklist unchanged.
- **FP05-AC4** With `approval: false` for a transition, `phase start` performs the transition and records it as "approval off" in `history`.
- **FP05-AC5** Without a `checklist` entry, `phase start` returns no checklist and creates no file.
- **FP05-AC6** A transition missing from `gates.json` requires approval.
- **FP05-AC7** In a new `quick` feature, `phase start quick` moves to `quick` without approval, and `phase start finish` asks for approval with the DoD checklist.
- **FP05-AC8** A team-defined `checks/<phase>.md` referenced in `gates.json` is copied and returned for that transition.
- **FP05-AC9** The shipped checklist templates contain the default items above, each with `Result` `pending`, and an empty "Check runs" section.

## Open points

- The value of the coverage threshold. DOD-02 checks `coverageThreshold` from `config.json` only when it is set (packages 02 and 14).
