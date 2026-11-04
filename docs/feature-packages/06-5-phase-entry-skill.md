# 06-5 · Entry protocol (`shade-kit-phase-entry`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
| Source | [06 Skill foundation](06-skill-foundation.md) (version handshake, entry protocol), [shade-kit-skills.md](../../research/shade-kit-skills.md), [phase start analysis](../../research/shade-kit-phase-start-analysis.md) |
| Kind | Skill (new general skill) |
| Depends on | 06-1, 06-3, 06-4 |
| Enables | 06-9 and every phase package (09–17) |

## Benefit

A developer starts every phase by calling its skill (for example `/shade-kit-plan`), so no phase starts behind their back. The skill checks through the CLI whether the phase can start, tells the developer what they can do instead when it cannot, and asks before it continues a phase that is already active. It runs the gate check and asks for approval itself. One general skill holds this protocol for all eight phase skills.

## Scope

### In scope

- The general skill `shade-kit-phase-entry`, loaded as the first step of every phase skill: `refine`, `map`, `plan`, `implement`, `verify`, `finish`, `diagnose`, `quick`.
- The version handshake of the phase skills.

### Out of scope

- `shade-kit-new`: it has no entry protocol, because `feature new` creates the feature with `new` already in progress (package 04); it has its own check (package 09).
- Loading the skill from the phase skills (06-9).
- The phase work itself (packages 09–17).

## Functional description

### Input from the calling skill

- The phase of the calling skill.

### Version handshake

- The skill calls `shade-kit version --json --expect <version>` with the version from its own frontmatter (06-1, 06-2). **Decided** on 2026-11-04.
- The phase skills do not check the version themselves; this check covers them, because all kit skills carry the same version (06-2). **Decided** on 2026-11-04.
- On a mismatch, or if `shade-kit` is not found, the skill stops before any phase work and ends with the next-steps message (06-3), which names what to update and how. A warning `engine-prerelease-version-mismatch` is shown, and the skill continues.

### Entry protocol

**Decided** in the skills interview on 2026-10-17 ([shade-kit-skills.md](../../research/shade-kit-skills.md)); steps 2 and 4 extended on 2026-10-18; steps 2, 4, and 6 changed on 2026-10-30 ([phase start analysis](../../research/shade-kit-phase-start-analysis.md)); moved into the general skill `shade-kit-phase-entry` on 2026-11-04.

The CLI check of a phase start is `phase start` itself: it refuses a transition that is not allowed without changing `feature.json`, and its outcomes for an active phase or a pending transition change nothing (package 04). There is no separate check command. **Decided** on 2026-10-30.

1. Version handshake (see above).
2. Check and start: `shade-kit phase start <phase> --json`, only for the phase of the calling skill, then by result:
   - Refused (failure envelope): the skill ends with the next-steps message for the refusal (06-3). Nothing has changed.
   - `transition-requested`: continue with step 3.
   - `transition-performed` (approval off, package 05): continue with step 3 for the checklist, then step 5.
   - `transition-pending`: the transition into this phase was requested earlier and still waits for approval. Say so, then continue with step 3 with the returned gate.
   - `already-in-progress`: the phase is already active. Tell the human and ask, with the agent's structured question tool where one exists: **continue the phase** (resume the work, or another round in `refine`) → step 5; or **end here** → the next-steps message. No phase work and no gate check before the answer.
   - `resume-pending`: the phase is active and the transition to `<pending>` waits for approval. Tell the human and ask: **discard the pending transition and continue** → `phase reject <pending> --note "resumed <phase>"`, then step 5; **keep it** → end, naming `/shade-kit-<pending>`, which decides on the pending transition; or **end here** → the next-steps message.
3. If the gate has a checklist, run `shade-kit-gate-check` (06-4) on it.
4. Gate decision:
   - Approval on: show the result and ask the human for an explicit approval, also when the check passed. On yes: `phase approve <phase>`. On no: `phase reject <phase> --note "<gaps>"`; the previous phase is in progress again, and the skill ends with the next-steps message, which names the reported gaps.
   - Approval off: the engine has already performed the transition (package 05). A failed checklist is reported as a warning and does not block.
5. Return to the calling skill the skills the CLI returned; the phase skill loads them and runs the phase.
6. The phase skill ends with the next-steps message (06-3). The skill never starts another phase skill.

The warnings of `phase start` are passed to the next-steps message (06-3). A phase skill never approves without a human answer in the conversation. **Decided** (concept §3.2).

## Acceptance criteria

- **FP06-5-AC1** With an engine whose minor version differs from the skill's, or without `shade-kit`, a phase skill stops before any phase work and names what to update.
- **FP06-5-AC2** The entry protocol asks for approval also when the checklist passed, and calls `phase approve` only after a yes in the conversation.
- **FP06-5-AC3** On a no, the phase skill calls `phase reject <phase>`, the previous phase is back in `in-progress`, and the skill ends with the next-steps message.
- **FP06-5-AC4** A phase skill called while a transition away from its phase is pending tells the human, offers to discard the pending transition and continue, to keep it, or to end, and calls `phase reject` only after the human chose to discard it.
- **FP06-5-AC5** A phase skill whose `phase start` is refused ends with the next-steps message, which names the reason and the human's options, and does no phase work.
- **FP06-5-AC6** A phase skill called while its phase is already in progress tells the human and asks whether to continue the phase or to end, before any phase work.
- **FP06-5-AC7** On `already-in-progress`, the entry protocol runs no gate check.
- **FP06-5-AC8** With approval off and a failed checklist, the skill reports the failed items as a warning and continues with the phase.
- **FP06-5-AC9** The skill calls `phase start` only for the phase of the calling skill.

## Open points

None.
