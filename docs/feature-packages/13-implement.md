# 13 · Implement, simplify, review (`implement`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (task checkboxes, commands from `plan.md`), 2026-10-18 (owner of the review skill), and 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md): the skill ends instead of reopening `plan` itself) |
| Source | Concept §5.1 (`implement`), §11.1 (implement loop), §4.1 (`review.md`), §6 |
| Depends on | 06, 12 |
| Enables | 14 (`verify`), 15 (DoD item "no open blockers") |

## Benefit

Code is written task by task against the plan, cleaned up, and reviewed independently before anyone verifies it. The loop ends only when the review has no blockers, so `verify` and the human reviewer start from code that already passed a structured review, and every finding and its outcome are on record.

## Scope

### In scope

- The `implement` phase skill in the `feature` profile.
- The loop implement → simplify → review.
- The general skill `shade-kit-review`, also used by the `quick` phase (package 17). **Decided** on 2026-10-18 (findings clarification): this package owns the skill and the `review.md` format; package 06 lists it among the general skills.
- The `review.md` template and finding severities.
- Updating task status in `tasks.md`.

### Out of scope

- The `bugfix` variant (failing test first). Package 16 adds it to this skill.
- The `quick` profile. It has its own phase `quick` with its own skill (package 17) instead of an `implement` phase.
- The final verification of acceptance criteria and the quality checks for the DoD (package 14).
- Committing. The kit never commits, pushes, or opens pull requests; the human does. **Decided** (confirmation list, 2026-10-18).

## Functional description

### Loop

```
implement ──► simplify ──► review ──(blockers)──► implement
                              │
                              └──(no blockers)──► ready for verify
```

1. **Implement:** work through the open tasks in `tasks.md` in their order, respecting "Depends on". Tasks marked "Parallel: yes" may run in parallel subagents where the agent supports them. **Proposed.** For each task, write the code and the proving tests named in the task, and run build and tests. Check off each proving test when it exists and passes, and the task when all its tests pass (package 12). A task that cannot be finished keeps its empty checkbox and gets a `Not done:` note with the reason. Deviations from the plan are recorded in the "Deviations" section of `plan.md` with the date and the reason; a changed or added test is also updated in the coverage matrix.
2. **Simplify:** go over the changed code and remove duplication, needless complexity, and dead code without changing behaviour. Tests must stay green.
3. **Review:** an independent review of the change against `spec.md`, `plan.md`, and the repository's conventions. Independent means a separate context that did not write the code (a subagent where the agent supports it, see package 08 for the fallback). Findings go into `review.md`.
4. If the review has open blockers, fix them and repeat the loop.

The loop stops after three rounds with open blockers and asks the human how to continue: go on, accept a finding, or end here to reopen `plan`. **Proposed.** The skill does not reopen `plan` itself: on "reopen `plan`" it ends and names `/shade-kit-plan`, which the human calls (package 06). **Decided** on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)).

### Quality commands

In v1 the agent runs the build, test, and lint commands itself (concept §6). It takes them from the test strategy in `plan.md`, which the human approved with the plan (package 12). If a command does not work as planned, the agent records the change as a deviation in `plan.md`. Without a `plan.md` (`quick` phase, package 17), it takes them from the repository's agent instructions (for example `CLAUDE.md`, `AGENTS.md`) or the build files. In every case it records the commands it used in `review.md`, so `verify` runs the same ones. **Proposed.**

### `review.md`

| Section | Content |
|---|---|
| Rounds | One entry per loop round: date, what changed, check results. |
| Findings | ID `F-<nnn>`, severity, file and line, description, status. |
| Commands | Build, test, and lint commands used. |

Severities: `blocker`, `major`, `minor`, `note`. Status: `open`, `fixed`, `accepted` (by the human, with reason). Only an `open` blocker keeps the loop going. **Proposed.**

## Acceptance criteria

- **FP13-AC1** After `implement`, every task in `tasks.md` is checked off or has a `Not done:` note with the reason.
- **FP13-AC2** Every proving test named in `tasks.md` exists after `implement`.
- **FP13-AC3** The review runs in a context separate from the one that wrote the code where the agent supports it.
- **FP13-AC4** The loop repeats while `review.md` has an `open` blocker and stops after three rounds with a question to the human.
- **FP13-AC5** Every finding in `review.md` has an ID, a severity, a location, and a status.
- **FP13-AC6** A finding is `accepted` only with a reason and after the human agreed in the conversation.
- **FP13-AC7** `review.md` records the build, test, and lint commands used.
- **FP13-AC8** The commands used are the ones from the test strategy in `plan.md`, or every difference is recorded as a deviation in `plan.md`.
- **FP13-AC9** After `implement`, the coverage matrix in `tasks.md` names the same proving tests as the task blocks.

## Open points

- None.
