# 12 · Plan the implementation (`plan`)

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §5.1 (`plan`), §5.2 (DoR), §6, §4.1 (`plan.md`, `tasks.md`) |
| Depends on | 05, 06, 10, 11 |
| Enables | 13 (`implement`), 14 (`verify` coverage check), 15 (report) |

## Benefit

Implementation starts only from a spec that passed the Definition of Ready, and follows a plan in which every requirement is bound to the tasks that build it and the tests that prove it. This mapping is what lets `verify` show that nothing was forgotten.

## Scope

### In scope

- The `plan` phase skill in the `feature` profile.
- The `plan.md` and `tasks.md` templates.
- The requirement → task → test mapping.

### Out of scope

- The DoR check itself. It runs in the gate into `plan` (packages 05 and 06), before this skill starts.
- The short `bugfix` variant of `plan`. Package 16 adds it to this skill, because it depends on `diagnosis.md` from package 16.

## Functional description

### Run

1. Read `spec.md` and `researches.md`.
2. Clarify open design choices with the interview skill (package 06), each with the agent's own recommendation.
3. Write `plan.md`: approach, design decisions with the rejected alternatives, affected components, risks, and the order of work.
4. Write `tasks.md`: the tasks with their requirement IDs and the tests that prove them.
5. Check the mapping (see below) and report gaps before the human is asked to move on.

### `tasks.md`

Every task has:

| Field | Rule |
|---|---|
| ID | `T-<nnn>`, stable like requirement IDs. **Proposed.** |
| Description | What is built or changed. |
| Requirements | One or more `REQ-` IDs from `spec.md`. |
| Proving tests | The tests that prove the task's acceptance criteria: test name or planned test name, and the `AC-` IDs it covers. |
| Status | `open`, `done`, updated by `implement` (package 13). |

Mapping rules (**Decided** in principle, concept §6):

- Every requirement that is not marked "removed" appears in at least one task.
- Every acceptance criterion is covered by at least one planned test.
- A criterion that cannot be tested automatically is marked "manual" and goes into the playbook in `verify` (package 14). **Proposed.**

The agent checks these rules; the engine does not parse `tasks.md` (package 02).

## Acceptance criteria

- **FP12-AC1** After `plan`, every requirement not marked "removed" appears in at least one task in `tasks.md`.
- **FP12-AC2** Every acceptance criterion in `spec.md` is covered by a planned test or marked "manual".
- **FP12-AC3** `plan.md` lists design decisions with the alternatives that were rejected.
- **FP12-AC4** The skill reports every mapping gap before the human is asked to approve the transition to `implement`.

## Open points

- None.
