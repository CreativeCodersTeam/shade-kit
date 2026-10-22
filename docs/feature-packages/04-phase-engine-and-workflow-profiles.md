# 04 · Phase engine and workflow profiles

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (`phase reject`, re-entry, `quick` phase; `phase next`, pending transition on `phase start <current>`) and 2026-10-21 (package 03 implemented: `feature new` checks the profile names) |
| Source | Concept §3.2, §5.1, §5.3, §5.4, §11.2–11.4 |
| Depends on | 01, 03 |
| Enables | 05 and every phase package |

## Benefit

The engine is the referee of the workflow: it knows each feature's phase, allows only transitions that the profile permits, and records every approval with who and when. The same state is visible to every developer and every agent, and it survives sessions, branch switches, and merges because it is committed with the code.

## Scope

### In scope

- The three v1 profiles `feature`, `bugfix`, `quick` as built-in definitions.
- The phase state machine and the phase fields of `feature.json`.
- `phase start`, `phase approve`, `phase reject`, `phase complete`, `phase skills`, `phase next`.
- Forward transitions, re-entering a phase that is in progress, rejecting a pending transition, and reopening an earlier phase.
- The approval history.

### Out of scope

- Gate configuration and checklists (package 05). Until package 05 exists, every transition needs an approval and has no checklist.
- What happens inside a phase (phase packages 09–17).
- `refactor` and custom profiles (later).
- Technical enforcement of approvals (later, see "Known weak point").

## Functional description

### Profiles

A profile is an ordered list of phases plus its gate defaults. One phase engine runs every profile. **Decided** (concept §5.4).

| Profile | Phases |
|---|---|
| `feature` | `new` → `refine` → `map` → `plan` → `implement` → `verify` → `finish` |
| `bugfix` | `new` → `diagnose` → `plan` → `implement` → `verify` → `finish` |
| `quick` | `new` → `quick` → `finish` |

- `quick` is a phase of its own: clarification, compact spec, implementation, and review in one run (package 17). **Decided** on 2026-10-18.
- Every phase runs through the skill named after it (`shade-kit-<phase>`, **Proposed** names); with the `quick` phase this holds for every phase of every profile. Skills adapt their behaviour to the profile they read from `feature current` (for example `plan` is short in `bugfix`).
- The profile of a feature is fixed at `feature new` and cannot change in v1. **Proposed.** `feature new` (package 03) already exists: it records the profile in `feature.json` and accepts only `feature`, `bugfix`, and `quick`, else it stops with exit code 40 (`unknown-profile`), which is in this package's range. **Implemented** in package 03 (`WorkflowProfiles`, `UnknownProfileException`); this package takes over the profile definitions.
- `discuss` and `drop` are not phases of any profile. `discuss` needs no feature state (package 09), `drop` is `feature drop` (package 03). **Decided** (findings clarification, 2026-10-18).

### Phase states

States per phase (**Proposed** in the concept, adopted): `pending` → `in-progress` → `awaiting-approval` → `approved`.

`feature.json` gets:

| Field | Content |
|---|---|
| `currentPhase` | The phase that is in progress or awaiting approval. |
| `phases` | Map phase → state, one entry per phase of the profile. |
| `pendingTransition` | Target phase while the current phase is `awaiting-approval`, otherwise absent. |
| `history` | List of entries: time, git user, action (`start`, `approve`, `reject`, `reopen`, `complete`), from phase, to phase, optional note. |

`feature new` sets `new` to `in-progress` and every other phase to `pending`. Until this package is built, `feature new` (package 03) writes no phase fields.

### Transitions

| Command | Allowed when | Effect |
|---|---|---|
| `phase start <next> --json` | `<next>` is the phase that follows `currentPhase` in the profile, and the current phase is `in-progress` | Current phase → `awaiting-approval`, `pendingTransition` = `<next>`. Returns: transition allowed, entry gate (approval required, checklist path or none), skills to load. |
| `phase start <next> --json` (repeated) | `pendingTransition` is already `<next>` | Changes nothing and returns the same gate again, for example in a new session. No `history` entry. **Decided** on 2026-10-18 (findings clarification). |
| `phase start <current> --json` (re-entry) | `<current>` is `currentPhase` and `in-progress` | Changes nothing and returns "already in progress" with no gate and the skills to load, so a phase skill can be called again in a new session or for another `refine` round. No `history` entry. **Decided** on 2026-10-18. |
| `phase start <current> --json` | `<current>` is `currentPhase` and `awaiting-approval` | Changes nothing and returns "transition to `<pendingTransition>` pending" with no gate and the skills to load. The phase skill asks the human whether to discard the pending transition and, on yes, calls `phase reject <pendingTransition>` (package 06). No `history` entry. **Decided** on 2026-10-18 (findings clarification). |
| `phase approve <next>` | `pendingTransition` is `<next>` | Records the approval in `history`. Current phase → `approved`, `<next>` → `in-progress`, `currentPhase` = `<next>`. |
| `phase reject <next> [--note <text>]` | `pendingTransition` is `<next>` | Cancels the pending transition after the human said no; the current phase goes back to `in-progress` (gate failed, changes requested). Recorded as `reject` with the note, for example the failed checklist items. **Decided** on 2026-10-18. |
| `phase start <earlier> --json` | `<earlier>` comes before `currentPhase` in the profile, and the current phase is `in-progress` | Like a forward transition, but needs an approval and is recorded as `reopen`. On approval, `<earlier>` → `in-progress` and every phase after it → `pending`. Their artifacts stay on disk. A "no" is a `phase reject <earlier>`. **Proposed.** |
| `phase complete` | The current phase is the last phase of the profile (`finish`) and `in-progress` | Last phase → `approved`, feature `status` → `done`, and the local override (package 03) is cleared if it points to this feature. No approval, because the transition into `finish` was approved already. **Proposed.** |

Every other request (skipping a phase, a phase that is not in the profile, any transition on a `done` feature) stops with an exit code and a reason the skill can show to the human.

The diagram in concept §11.3 stays valid: `pending → in-progress` happens through `phase approve`, `in-progress → awaiting-approval` through `phase start <next>`, and `awaiting-approval → in-progress` through `phase reject`.

Because every phase skill calls `phase start <its phase>` as the first step of its entry protocol (package 06), the same call covers five situations: moving forward, repeating a pending transition, re-entering a phase in progress, resuming a phase whose transition is pending, and reopening an earlier phase.

### Approval

- Every transition needs an explicit human approval in v1. Package 05 adds switching it off per transition. **Decided** (concept §5.2).
- The agent asks the human, and only after the human says yes does it call `phase approve`. **Decided** (concept §3.2).
- **Known weak point (Decided, accepted for v1):** nothing technically stops an agent from calling `phase approve` on its own. Later options: `approve` only in an interactive terminal, or a hook that blocks the call.

### `phase next --json`

Returns the possible next steps of the current feature, so `shade-kit-next-phase` (package 06) does not derive them from the profile. **Decided** on 2026-10-18 (findings clarification). It changes nothing.

| Field | Content |
|---|---|
| `current` | Current phase and its state, and the feature status. For a `done` feature this is the only field. |
| `continue` | Present when the current phase is `in-progress`: the phase and its skill. |
| `pending` | Present when the current phase is `awaiting-approval`: the target phase, its gate (approval, checklist), and its skill. |
| `forward` | The next phase of the profile with its gate and skill, or absent in the last phase. |
| `reopen` | List of earlier phases with their skills. A reopen always needs an approval. |

### `phase skills <phase> --json`

Returns the phase skill for the feature's profile and the general skills it needs (package 06). The same list is part of the `phase start` result. Extension skills are added here later (concept §7).

### Merges

`feature.json` is changed by one developer on one branch in the normal case. If a merge conflict happens anyway, the file is plain JSON that a human can resolve; the engine gives no extra support in v1. **Proposed.**

## Acceptance criteria

- **FP04-AC1** A new feature of every profile has `new` `in-progress` and every other phase `pending`.
- **FP04-AC2** `phase start` with the next phase of the profile sets the current phase to `awaiting-approval` and returns the skills to load.
- **FP04-AC3** `phase start` with a phase that is not next, not current, and not earlier stops with an exit code; `feature.json` is unchanged.
- **FP04-AC4** `phase approve` without a matching pending transition stops with an exit code.
- **FP04-AC5** `phase approve` records time, git user, from, and to in `history` and moves `currentPhase`.
- **FP04-AC6** `phase reject <next>` while the transition to `<next>` is pending returns the current phase to `in-progress`, removes the pending transition, and records `reject` with the note; without a matching pending transition it stops with an exit code.
- **FP04-AC7** An approved reopen of an earlier phase sets every later phase to `pending` and keeps their artifact files.
- **FP04-AC8** `phase complete` in `finish` sets the feature to `done`; in any other phase it stops with an exit code.
- **FP04-AC9** `bugfix` contains `diagnose` and no `refine` or `map`; `quick` contains only `new`, `quick`, `finish`.
- **FP04-AC10** `phase skills` returns a different phase skill list per profile where the profiles differ.
- **FP04-AC11** `phase start <current>` while the current phase is `in-progress` leaves `feature.json` byte-for-byte unchanged and returns "already in progress" without a gate.
- **FP04-AC12** `phase start <next>` while the transition to `<next>` is already pending returns the same gate again and adds no `history` entry.
- **FP04-AC13** `phase start <current>` while the transition away from `<current>` is pending leaves `feature.json` unchanged and returns the pending transition without a gate.
- **FP04-AC14** `phase next --json` returns `continue`, `forward`, and `reopen` for a phase `in-progress`, `pending` for a phase `awaiting-approval`, no `forward` in the last phase, and only `current` for a `done` feature; `feature.json` is unchanged.

## Open points

- Whether a profile can be changed on an existing feature (for example `quick` turns out to be a `feature`) is **Open**. v1 proposal: drop and create a new feature.
