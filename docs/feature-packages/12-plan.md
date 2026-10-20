# 12 · Plan the implementation (`plan`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (`plan.md` and `tasks.md` templates) |
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
3. Write `plan.md` (see below): how the spec is built and why.
4. Write `tasks.md` (see below): the tasks in the order they are worked on, with their requirement IDs and the tests that prove them, and the coverage matrix.
5. Check the mapping (see below) and report gaps before the human is asked to move on.

The order of work is the task order in `tasks.md`; `plan.md` does not repeat it, so the two cannot drift apart. **Decided** in the template interview on 2026-10-17.

### `plan.md`

Section order and rules. **Decided** in the template interview on 2026-10-17; the general template conventions are in package 02.

| Section | Rule |
|---|---|
| Summary | Three to five sentences: what the feature does and the chosen technical approach. |
| Technical context | Language and runtime, frameworks and libraries, storage, test frameworks, target platforms, constraints. Taken from the code base, not guessed; "n/a" where a line does not apply. |
| Approach | The main idea, the data flow, and the key interfaces, with `REQ-` references. |
| Affected components | Table `Component or path \| Change (new, changed, removed) \| Requirements`. |
| Design decisions | One subsection per decision with a real alternative, ID `D-<nnn>`, in ADR-light form: Context, Decision, Rejected alternatives (each with the reason), Consequences. |
| Test strategy | Test levels (unit, integration, manual) with scope and location, and the quality commands for build, test, lint, and coverage. The human approves the commands together with the plan; `implement` and `verify` run them (packages 13 and 14). |
| Migration and rollout | Data or format migrations, breaking changes, feature flags, backwards compatibility, rollback; "None." with the reason. |
| Risks | Table `Risk \| Impact \| Mitigation`, including every fact that could not be verified. |
| Deviations | Filled in by `implement` (package 13): date, what changed, and why. "None." while planning. |

### `tasks.md`

**Decided** in the template interview on 2026-10-17; the general template conventions are in package 02.

Every task is one checkbox item with fields below it:

```markdown
- [ ] **T-003** – Add token refresh to the SSO client
  - Requirements: REQ-002
  - Depends on: T-001
  - Parallel: no
  - Affected: `src/Auth/SsoClient.cs`
  - Proving tests:
    - [ ] `SsoClientTests.Refresh_ExpiredToken_RenewsToken` → AC-002.1
    - [ ] `SsoClientTests.Refresh_RevokedToken_Throws` → AC-002.2
  - Notes: –
```

| Field | Rule |
|---|---|
| Checkbox | The task status. `[x]` means done: the code is written and every proving test of the task exists and passes. There is no separate status field. |
| ID | `T-<nnn>`, stable like requirement IDs. |
| Title | What is built or changed. |
| Requirements | One or more `REQ-` IDs from `spec.md`. |
| Depends on | `T-` IDs that must be done first, or `none`. |
| Parallel | `yes` only when the task touches other files than every task it could run alongside and depends on none of them. |
| Affected | Paths or components the task touches (from `map` and `plan.md`). |
| Proving tests | One checkbox per test: test name or planned test name → the `AC-` IDs it covers. `[x]` when the test exists and passes. |
| Notes | Deviations and remarks from `implement`. A task that is not done at the end of `implement` keeps its empty checkbox, and its notes start with `Not done: blocked \| skipped \| dropped – <reason>`. |

Order: one flat list in the order the tasks are worked on. A large feature may group its tasks under stage headings (Setup, Foundation, Requirements, Polish), each ending with a "Checkpoint" line that says what works at that point.

The coverage matrix at the end of `tasks.md` has one row per acceptance criterion of every requirement that is not removed: `Criterion | Requirement | Proving tests | Tasks | Manual`. `plan` writes it, `implement` keeps it in sync with the task blocks, and `verify` (package 14) and the report (package 15) read it.

Mapping rules (**Decided** in principle, concept §6):

- Every requirement that is not marked "removed" appears in at least one task.
- Every acceptance criterion is covered by at least one planned test, or is marked manual in the coverage matrix.
- A criterion that cannot be tested automatically is marked "manual" with the reason and goes into the playbook in `verify` (package 14). **Proposed.**
- A matrix row without a proving test that is not marked manual is a gap.

The agent checks these rules; the engine does not parse `tasks.md` (package 02).

## Acceptance criteria

- **FP12-AC1** After `plan`, every requirement not marked "removed" appears in at least one task in `tasks.md`.
- **FP12-AC2** Every acceptance criterion in `spec.md` is covered by a planned test or marked "manual".
- **FP12-AC3** Every design decision in `plan.md` has a `D-` ID, its context, the decision, every rejected alternative with the reason, and its consequences.
- **FP12-AC4** The skill reports every mapping gap before the human is asked to approve the transition to `implement`.
- **FP12-AC5** The coverage matrix in `tasks.md` has exactly one row for every acceptance criterion of every requirement that is not removed.
- **FP12-AC6** The test strategy in `plan.md` names a command for build, test, lint, and coverage, or states why one does not apply.
- **FP12-AC7** Every task in `tasks.md` has a checkbox, a `T-` ID, at least one `REQ-` ID, and every field of the task block.

## Open points

- None.
