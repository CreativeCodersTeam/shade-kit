# 10 · Refine the requirement (`refine`)

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §3.3 (research focuses), §5.1 (`refine`), §5.2 (DoR), §6 |
| Depends on | 06, 09 |
| Enables | 11 (`map`), 12 (`plan`, DoR) |

## Benefit

Vague statements, gaps, and contradictions in a requirement are found and resolved before anyone plans or codes. The result is a spec with stable requirement IDs and testable acceptance criteria, which every later phase and the final report can trace back to.

## Scope

### In scope

- The `refine` phase skill in the `feature` profile.
- The research focuses of `refine`: gaps and open questions, security at requirement level.
- The `spec.md` template and its rules (IDs, acceptance criteria, out of scope, open questions).
- Repeated `refine` rounds.

### Out of scope

- Anything about the code base (`map`, package 11). `refine` deals with the requirement only. **Proposed** split in the concept, adopted.
- `bugfix` and `quick`: their specs are written by `diagnose` (package 16) and `implement` (package 17) in the same `spec.md` format.

## Functional description

### Run

1. Read `request.md`, any discussion notes of the feature, and an existing `spec.md` (later rounds).
2. Run the research skill (package 06) with two focuses in parallel:
   - **Gaps and open questions:** vague terms, missing cases, contradictions, unstated assumptions.
   - **Security at requirement level:** data that needs protection, roles and permissions, abuse cases.
3. Merge the results into `researches.md` (sections `refine / gaps`, `refine / security`).
4. Run the interview skill on the findings: one topic at a time, each with the agent's own proposal.
5. Write or update `spec.md` from the template.
6. Report what changed and which questions are still open. The human decides whether to run another round or move on.

### `spec.md`

| Section | Rule |
|---|---|
| Goal | One short paragraph: what problem the feature solves and for whom. |
| Requirements | Each with a stable ID `REQ-<nnn>`, unique within the feature, numbered from `REQ-001`. **Decided** (stable IDs), **Proposed** (format from the concept example). |
| Acceptance criteria | At least one per requirement, written so that a test can decide it. ID `AC-<nnn>.<m>` linked to its requirement (for example `AC-001.1`). **Proposed.** |
| Out of scope | Stated explicitly, also when empty ("nothing excluded"). |
| Open questions | Every unresolved question. Must be empty before the DoR passes (package 05). |
| Security considerations | The relevant findings from the security research. |

IDs are never renumbered. A removed requirement keeps its ID with the mark "removed", so references in `tasks.md`, tests, and the report stay valid. **Proposed.**

### Repeated rounds

`refine` can run several times. **Decided.** A new round starts from the current `spec.md`. If the feature has moved on, the human reopens `refine` through the router (package 04 reopen). Later phases then go back to `pending`, and their artifacts must be checked against the changed spec when they run again.

## Acceptance criteria

- **FP10-AC1** After a `refine` run, every requirement in `spec.md` has a `REQ-` ID and at least one linked acceptance criterion.
- **FP10-AC2** `researches.md` contains a `refine / gaps` and a `refine / security` section after the run.
- **FP10-AC3** A second round keeps every existing ID; a removed requirement is marked "removed", not deleted.
- **FP10-AC4** `spec.md` always has an out-of-scope section and an open-questions section.
- **FP10-AC5** The interview asks about every gap finding or records why it was dismissed.

## Open points

- None.
