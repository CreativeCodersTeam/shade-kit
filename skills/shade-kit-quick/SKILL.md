---
name: shade-kit-quick
description: SHADE kit phase skill for the `quick` profile. Runs short clarification, a compact spec, implementation with tests, simplify and review, and a self-check in one go, with a single approval at the end. Use when the current SHADE kit feature has the quick profile and the user wants to implement it.
---

# shade-kit-quick

<!-- Skeleton: purpose and flow only, details follow. Source: docs/feature-packages/17-quick-profile.md. CLI commands other than `shade-kit version` and `shade-kit features list` are planned and do not exist yet. -->

## Purpose

Runs the `implement` phase of the `quick` profile: a mini run of refine, plan, and implement for small, clear changes. It still leaves enough traces (`spec.md`, `review.md`, `verification.md`) for the Definition of Done and the report.

| Input | Output |
|---|---|
| `request.md` | `spec.md` (compact), code and tests, `review.md`, `verification.md` |

## Flow

1. **Entry protocol:**
   1. Version handshake: `shade-kit version --json`. Stop on a mismatch and name what to update.
   2. `shade-kit feature current --json`. If the profile is not `quick`, hand off to `shade-kit-implement` and stop here.
   3. `shade-kit phase start implement --json`. Stop if the transition is not allowed and report why. By default this gate needs no approval in `quick`.
   4. If the gate has a checklist: load `shade-kit-gate-check` on it.
   5. If the gate needs approval: show the result, ask the human, and only after a yes call `shade-kit phase approve implement`.
   6. Load the skills the CLI returned.
2. **Read** `request.md`.
3. **Short clarification:** load `shade-kit-interview` and ask only the questions that block the change. If the change is not small or not clear, recommend dropping it and creating a `feature` or `bugfix` instead, and wait for the human.
4. **Compact spec:** write `spec.md` with only goal, `REQ-` requirements, and acceptance criteria; the other sections say "not applicable in quick".
5. **Implement and test:** write the code and the tests that prove each acceptance criterion. No `plan.md`, no `tasks.md`.
6. **Simplify and review:** simplify inline, then load `shade-kit-review`; loop until no blocker is open.
7. **Self-check:** run the quality commands (from the repository's agent instructions or build files) and write `verification.md` with the criterion → test mapping and the results.
8. **Report** the result. The single approval follows at the gate into `finish` (`shade-kit-next-phase`).
