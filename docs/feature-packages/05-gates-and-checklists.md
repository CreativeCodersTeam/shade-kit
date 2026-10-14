# 05 · Gates and checklists

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §2 (Gates), §4.1 (`checks/`), §5.2, §6, §11.1, §11.4 |
| Depends on | 02, 04 |
| Enables | 12 (DoR), 15 (DoD), every phase transition |

## Benefit

A team decides per repository how strict the workflow is: which transitions need a human approval and which transitions must pass a checklist first. The Definition of Ready and Definition of Done are written down, can be edited by the team, and leave a filled-in record in every feature folder.

## Scope

### In scope

- `gates.json`: shape, defaults per profile, switching approval and checklist on or off.
- The checklist templates `checks/dor.md` and `checks/dod.md` and their default content.
- Creating the feature's own copy of a checklist when its phase starts.
- The gate information in the `phase start` result (package 04).
- The rule for checklist items that do not apply to a profile.

### Out of scope

- Checking a checklist. The agent does that with the checklist-check skill (package 06). The engine does not parse checklists (ADR 0001, package 02).
- Quality checks run by the CLI and check commands configured in `gates.json` (later, concept §6).
- Approval enforcement (later, package 04 "Known weak point").

## Functional description

### Gate model

- A gate belongs to a transition and is identified by its target phase. **Decided.**
- It is checked when the next phase starts. **Decided.**
- It has two independent switches: `approval` (a human must approve) and `checklist` (path of a checklist the agent must check first, or none). **Decided** that each gate can be switched off; switching off approval and checklist independently is the concept's **Proposed** answer to open question 4, adopted here.
- DoR is the checklist of the gate into `plan`. DoD is the checklist of the gate into `finish`. All other gates have no checklist by default. **Decided.**
- Any transition can get a checklist by pointing its gate to a `checks/<phase>.md` template. **Decided** as an extension point.

### `gates.json`

Written by `init` (package 02), owned by the team for its values, versioned like every JSON file (package 01).

The concept's example is keyed by phase only. Because the `quick` profile needs different defaults than `feature` (one approval at the end), gates are keyed by profile and then by target phase. **Proposed**, deviation from the concept example.

```json
{
  "schemaVersion": 1,
  "profiles": {
    "feature": {
      "refine":    { "approval": true },
      "map":       { "approval": true },
      "plan":      { "approval": true, "checklist": "checks/dor.md" },
      "implement": { "approval": true },
      "verify":    { "approval": true },
      "finish":    { "approval": true, "checklist": "checks/dod.md" }
    },
    "bugfix": {
      "diagnose":  { "approval": true },
      "plan":      { "approval": true, "checklist": "checks/dor.md" },
      "implement": { "approval": true },
      "verify":    { "approval": true },
      "finish":    { "approval": true, "checklist": "checks/dod.md" }
    },
    "quick": {
      "implement": { "approval": false },
      "finish":    { "approval": true, "checklist": "checks/dod.md" }
    }
  }
}
```

- A transition with no entry uses `{ "approval": true }`. Approval is the safe default. **Proposed.**
- With `approval: false`, `phase start` performs the transition at once and records it in `history` with the note "approval off". **Proposed.**
- With `approval: false` and a `checklist`, the checklist copy is still created and checked by the agent, but a failed item is only reported as a warning; it does not block, because nobody is asked to decide. An agent never approves on its own instead. **Proposed.**
- The defaults follow concept §5.2: every transition needs an approval, except the `quick` transition into `implement` (concept §5.4: "one approval at the end").

### Checklists

- The team edits the templates in `.shadekit/templates/checks/`. They are tracked by `templates.lock.json` like every template (package 02).
- When a phase with a checklist starts, the engine copies the template into the feature folder (`features/<id>/checks/dor.md`) if the copy does not exist yet, and returns its path. The agent checks and fills in this copy. The filled copy is the record of the gate. **Proposed.**
- A reopened phase (package 04) reuses the existing copy, so earlier results stay visible; the agent rechecks every item.

Default content (**Proposed** in the concept, adopted):

| DoR (gate into `plan`) | DoD (gate into `finish`) |
|---|---|
| Every requirement has an ID and at least one acceptance criterion | Build, test, lint, and coverage checks pass |
| No open questions left in `spec.md` | Every acceptance criterion is covered by a passing test |
| Out of scope is stated | `review.md` has no open blockers |
| Affected areas known (`map` done, or skipped on purpose) | Every item in `playbook.md` is checked off by a human |

### Items that do not apply

A checklist item that refers to a phase or artifact the feature's profile does not have is marked "not applicable" with the reason, and does not fail the gate. Examples: "`map` done" in `bugfix` (`diagnose` covers affected areas), "`playbook.md`" in `quick`. **Proposed.** This keeps one DoR and one DoD for all profiles instead of a checklist per profile.

### `phase start` result

The gate part of the result contains: target phase, `approval` (true or false), `checklist` (path in the feature folder, or none), and whether the transition was already performed because approval is off.

## Acceptance criteria

- **FP05-AC1** `init` writes `gates.json` with the defaults above and `checks/dor.md` and `checks/dod.md` with the default items.
- **FP05-AC2** `phase start plan` in a `feature` creates `features/<id>/checks/dor.md` from the template and returns its path.
- **FP05-AC3** A second `phase start plan` after a reopen keeps the existing feature checklist unchanged.
- **FP05-AC4** With `approval: false` for a transition, `phase start` performs the transition and records it as "approval off" in `history`.
- **FP05-AC5** Without a `checklist` entry, `phase start` returns no checklist and creates no file.
- **FP05-AC6** A transition missing from `gates.json` requires approval.
- **FP05-AC7** In a new `quick` feature, `phase start implement` moves to `implement` without approval, and `phase start finish` asks for approval with the DoD checklist.
- **FP05-AC8** A team-defined `checks/<phase>.md` referenced in `gates.json` is copied and returned for that transition.

## Open points

- The deviation from the concept's `gates.json` example (keyed by profile) needs confirmation.
- Coverage threshold for the DoD item "coverage checks pass" (package 14).
