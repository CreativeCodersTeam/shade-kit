# 13 · Implement, simplify, review (`implement`)

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §5.1 (`implement`), §11.1 (implement loop), §4.1 (`review.md`), §6 |
| Depends on | 06, 12 |
| Enables | 14 (`verify`), 15 (DoD item "no open blockers") |

## Benefit

Code is written task by task against the plan, cleaned up, and reviewed independently before anyone verifies it. The loop ends only when the review has no blockers, so `verify` and the human reviewer start from code that already passed a structured review, and every finding and its outcome are on record.

## Scope

### In scope

- The `implement` phase skill in the `feature` profile.
- The loop implement → simplify → review.
- The `review.md` template and finding severities.
- Updating task status in `tasks.md`.

### Out of scope

- The `bugfix` variant (failing test first) and the `quick` variant (all in one). Packages 16 and 17 add them to this skill.
- The final verification of acceptance criteria and the quality checks for the DoD (package 14).
- Committing. The kit never commits, pushes, or opens pull requests; the human does. **Proposed.**

## Functional description

### Loop

```
implement ──► simplify ──► review ──(blockers)──► implement
                              │
                              └──(no blockers)──► ready for verify
```

1. **Implement:** work through the open tasks in `tasks.md` in plan order. For each task, write the code and the proving tests named in the task, run build and tests, and set the task to `done` when they pass. Deviations from the plan are recorded in `plan.md` with the reason.
2. **Simplify:** go over the changed code and remove duplication, needless complexity, and dead code without changing behaviour. Tests must stay green.
3. **Review:** an independent review of the change against `spec.md`, `plan.md`, and the repository's conventions. Independent means a separate context that did not write the code (a subagent where the agent supports it, see package 08 for the fallback). Findings go into `review.md`.
4. If the review has open blockers, fix them and repeat the loop.

The loop stops after three rounds with open blockers and asks the human how to continue (go on, accept a finding, or reopen `plan`). **Proposed.**

### Quality commands

In v1 the agent runs the build, test, and lint commands itself (concept §6). It takes them from the repository's agent instructions (for example `CLAUDE.md`, `AGENTS.md`) or the build files, and records the commands it used in `review.md`, so `verify` runs the same ones. **Proposed.**

### `review.md`

| Section | Content |
|---|---|
| Rounds | One entry per loop round: date, what changed, check results. |
| Findings | ID `F-<nnn>`, severity, file and line, description, status. |
| Commands | Build, test, and lint commands used. |

Severities: `blocker`, `major`, `minor`, `note`. Status: `open`, `fixed`, `accepted` (by the human, with reason). Only an `open` blocker keeps the loop going. **Proposed.**

## Acceptance criteria

- **FP13-AC1** After `implement`, every task in `tasks.md` is `done` or has a recorded reason why not.
- **FP13-AC2** Every proving test named in `tasks.md` exists after `implement`.
- **FP13-AC3** The review runs in a context separate from the one that wrote the code where the agent supports it.
- **FP13-AC4** The loop repeats while `review.md` has an `open` blocker and stops after three rounds with a question to the human.
- **FP13-AC5** Every finding in `review.md` has an ID, a severity, a location, and a status.
- **FP13-AC6** A finding is `accepted` only with a reason and after the human agreed in the conversation.
- **FP13-AC7** `review.md` records the build, test, and lint commands used.

## Open points

- None.
