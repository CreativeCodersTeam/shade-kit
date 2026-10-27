# 04 · Phase engine and workflow profiles

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (`phase reject`, re-entry, `quick` phase; `phase next`, pending transition on `phase start <current>`), 2026-10-21 (package 03 implemented: `feature new` checks the profile names), and 2026-10-26 (implementation clarification and implementation: profiles as built-in definitions behind a catalog, a pure phase engine, the gate seam for package 05, the six `phase` commands, the phase fields of `feature.json`, exit codes 37 and 41–46). Implemented; gate configuration and checklists come with package 05. |
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

- Gate configuration and checklists (package 05). Until package 05 exists, every transition needs an approval and has no checklist (`DefaultTransitionGateProvider`).
- The `report` setting in the result of `phase start finish` (package 15, [open points](#open-points)).
- What happens inside a phase (phase packages 09–17).
- `refactor` and custom profiles (later).
- Technical enforcement of approvals (later, see "Known weak point").

## Functional description

**Implemented** in the Core namespaces `CreativeCoders.ShadeKit.Core.Workflow.Profiles`, `CreativeCoders.ShadeKit.Core.Workflow.Engine`, and `CreativeCoders.ShadeKit.Core.Workflow`, and in the CLI commands under `Commands/Phase/`. All services are singletons registered by `AddShadeKitCore()` with `TryAddSingleton`.

### Architecture

**Decided** on 2026-10-26 (implementation clarification): one engine manages the phases of every profile; the profiles are defined separately, so a profile can be added and the phases of a profile changed without a change to the engine. **Implemented**.

```text
CLI Commands/Phase/*  ──►  IPhaseWorkflow / PhaseWorkflow           (orchestration, all I/O)
                             │ asks             │ asks              │ calls with profile, state, gates
                             ▼                  ▼                   ▼
            IWorkflowProfileCatalog   ITransitionGateProvider   IPhaseEngine / PhaseEngine
            └ WorkflowProfile         (seam for package 05)     (pure state machine)
              └ WorkflowPhase
```

- **Profiles** (`Workflow/Profiles/`): a `WorkflowProfile` is a name and an ordered list of `WorkflowPhase` (name, phase skill, general skills, `CanReopen`, default `true`). The built-in profiles are C# definitions in `BuiltInWorkflowProfiles`, offered by `IWorkflowProfileCatalog` / `BuiltInWorkflowProfileCatalog`. A profile needs a non-empty name, at least one phase, and unique phase names; the catalog needs unique profile names. Names are compared ordinally. Adding a profile or changing the phase order of a profile means changing `BuiltInWorkflowProfiles` only. The names are constants in `WorkflowProfileNames`, `WorkflowPhaseNames`, and `WorkflowSkillNames` (`ForPhase(phase)` gives `shade-kit-<phase>`). **Implemented** (`BuiltInWorkflowProfilesTests`, `BuiltInWorkflowProfileCatalogTests`, `WorkflowProfileTests`, `WorkflowPhaseTests`).
- **Engine** (`Workflow/Engine/`): `IPhaseEngine` / `PhaseEngine` is a pure state machine without I/O, clock, or git. It gets the profile, the stored phase state, and the gates, and returns a decision (new state and one history record) or throws. It contains no profile, phase, or skill name; everything comes from the `WorkflowProfile` it is given. The interface `IPhaseEngine` is an explicit decision of the user (2026-10-26). **Implemented** (`PhaseEngineGenericityTests` run synthetic profiles with four, two, and one phase; `PhaseEngineArchitectureTests` scan the IL of the engine namespace for references to the built-in profiles and for profile, phase, and skill name literals).
- **Gate seam**: `ITransitionGateProvider.GetGateAsync(store, profile, targetPhase)` returns the `TransitionGate(Approval, Checklist)` of the transition into a phase. Package 04 registers `DefaultTransitionGateProvider`: approval required, no checklist, for every transition. The engine already handles `Approval = false` generically ([Transitions](#transitions)); package 05 is to register a provider that reads `gates.json` from the store it gets. A reopen always needs an approval and has no checklist, whatever the provider returns (engine rule). **Implemented** (`DefaultTransitionGateProviderTests`, `PhaseEngineStartTests`).
- **Orchestration**: `IPhaseWorkflow` / `PhaseWorkflow` resolves the current feature, reads `feature.json`, asks the catalog for the profile and the provider for the gates, calls the engine, reads the git identity only when a write follows, and writes `feature.json` through `IKitStore.WriteJsonAsync`. `phase complete` also clears the local override. **Implemented** (`PhaseWorkflow*Tests`).

**The profile applies live.** **Decided** on 2026-10-26 (implementation clarification). The phase order always comes from the current profile definition, also for existing features:

- A phase of the profile without an entry in `phases` counts as `pending`.
- Entries in `phases` for phases the profile no longer has are kept when the file is written (after the profile phases, in their stored order) but ignored by every rule.
- A `currentPhase` or `pendingTransition` that is not a phase of the profile stops every command that reads the phase state with exit code 46.
- A `done` feature is only checked for a consistent end state (current phase `approved`, no pending transition), not for the position of its current phase, so a phase added to the end of a profile does not make finished features invalid.

**Implemented** (`PhaseEngineReadStateTests`, `PhaseEngineGenericityTests`).

### Profiles

A profile is an ordered list of phases plus its gate defaults (gate defaults: package 05). One phase engine runs every profile. **Decided** (concept §5.4). **Implemented** (`BuiltInWorkflowProfiles`).

| Profile | Phases with their general skills |
|---|---|
| `feature` | `new` (none) → `refine` (`shade-kit-interview`, `shade-kit-research`) → `map` (`shade-kit-research`) → `plan` (`shade-kit-interview`) → `implement` (`shade-kit-review`) → `verify` (none) → `finish` (`shade-kit-report`) |
| `bugfix` | `new` (none) → `diagnose` (`shade-kit-interview`, `shade-kit-research`) → `plan` (`shade-kit-interview`) → `implement` (`shade-kit-review`) → `verify` (none) → `finish` (`shade-kit-report`) |
| `quick` | `new` (none) → `quick` (`shade-kit-interview`, `shade-kit-review`) → `finish` (`shade-kit-report`) |

- The phase skill of every phase is `shade-kit-<phase>`; with the `quick` phase this holds for every phase of every profile. Skills adapt their behaviour to the profile they read from `feature current` (for example `plan` is short in `bugfix`). The general skills follow `research/shade-kit-skills.md` §3. **Decided** on 2026-10-26 (implementation clarification). `shade-kit-gate-check` is a constant in `WorkflowSkillNames` but in no phase list: the entry protocol loads it for a gate with a checklist (package 06).
- `new` cannot be reopened in any built-in profile (`CanReopen = false`); every other phase can. **Decided** on 2026-10-26 (implementation clarification).
- `quick` is a phase of its own: clarification, compact spec, implementation, and review in one run (package 17). **Decided** on 2026-10-18.
- The profile of a feature is fixed at `feature new` and cannot change in v1. **Proposed.** `feature new` (package 03) checks the profile against the catalog (`IWorkflowProfileCatalog.GetProfile`) and stops with exit code 40 (`unknown-profile`) for an unknown one. A `feature.json` whose `profile` the catalog does not know stops every phase command and `feature list`, `current`, and `show` with exit code 40 too; the message names the file and the field. **Implemented** (`UnknownProfileException` in `Workflow/Profiles/Exceptions/`, which carries the known profile names, so `CliErrorMapper` needs no catalog).
- `discuss` and `drop` are not phases of any profile. `discuss` needs no feature state (package 09), `drop` is `feature drop` (package 03). **Decided** (findings clarification, 2026-10-18).

### Phase states and `feature.json`

States per phase (**Proposed** in the concept, adopted): `pending` → `in-progress` → `awaiting-approval` → `approved`. **Implemented** (`PhaseState`).

`feature.json` stays format version 1: the phase fields are optional, so features created before package 04 need no migration. **Decided** on 2026-10-26 (implementation clarification). **Implemented** (`FeatureDocument`, `FeatureDocumentTests`).

| Field | Content |
|---|---|
| `currentPhase` | The phase that is in progress or awaiting approval; for a `done` feature the last phase. |
| `phases` | Object phase → state, in profile order, then kept entries of phases the profile no longer has. |
| `pendingTransition` | Target phase while the current phase is `awaiting-approval`; omitted otherwise. |
| `history` | List of entries `{ at, by: { name, email }, action, from, to, note }`; `action` is `start`, `approve`, `reject`, `reopen`, or `complete`; `to` and `note` are omitted when they have no value. |

- The fields follow `status` in this order: `currentPhase`, `phases`, `pendingTransition`, `history`. Unknown fields are kept at the top level, in history entries, and in `by` (package 01).
- `feature new` writes the initial state: the first phase `in-progress`, every other phase `pending`, `currentPhase` = the first phase, no `pendingTransition`, an empty `history`. **Implemented** (`FeatureStore.CreateAsync`, `IPhaseEngine.CreateInitialState`).
- A feature without `currentPhase` (created by an older engine) is read as the initial state of its profile. The first writing phase command writes all phase fields; reading commands (`phase next`, `phase skills`, `feature list`, `current`, `show`) never write. Without `currentPhase`, the file must have no `pendingTransition`, no or empty `phases`, and status `active`; otherwise exit code 46. **Proposed** (the conditions without `currentPhase`).
- An active feature's current phase must be `in-progress` without `pendingTransition`, or `awaiting-approval` with a `pendingTransition` that is the next phase or an earlier one; only the state of the current phase is checked. **Proposed.**
- `at` is the local time with its offset from `TimeProvider.GetLocalNow()`. Unlike `createdAt` it is not cut to whole seconds, so it has fractional seconds (seen with the built CLI on 2026-10-26, for example `2026-10-26T21:28:05.117086+01:00`; [open points](#open-points)). `by` is the git user (`user.name`, `user.email`) from `IGitRepositoryInfo.GetIdentityAsync`. **Implemented** (`PhaseHistoryEntry`, `PhaseHistoryActor`).
- One entry per state change. **Decided** on 2026-10-26 (implementation clarification). **Implemented**.

  | Call | `action` | `from` → `to` |
  |---|---|---|
  | `phase start <next>` (approval on) | `start` | current → next |
  | `phase start <next>` (approval off) | `approve`, note `approval off` | current → next |
  | `phase start <earlier>` | `reopen` | current → earlier |
  | `phase approve <x>` | `approve` | from and to of the pending transition |
  | `phase reject <x> [--note]` | `reject`, with the note | from and to of the pending transition |
  | `phase complete` | `complete` | last phase, no `to` |

  Every no-op (repeated `phase start`, re-entry, resume, `phase next`, `phase skills`) adds no entry and writes nothing; `feature.json` stays byte-for-byte unchanged.

### Transitions

Every `phase` command works on the current feature, resolved like `feature current` (branch, then local override, package 03); there is no `--feature` option. **Decided** on 2026-10-26 (implementation clarification). **Implemented** (`PhaseEngine`, `PhaseWorkflow`).

| Command | Allowed when | Effect | `outcome` |
|---|---|---|---|
| `phase start <next>` | `<next>` follows `currentPhase` in the profile, and the current phase is `in-progress` | Gate with approval: current phase → `awaiting-approval`, `pendingTransition` = `<next>`. Gate without approval (only through a package 05 gate provider): current phase → `approved`, `<next>` → `in-progress`, `currentPhase` = `<next>`. | `transition-requested`; without approval `transition-performed` |
| `phase start <x>` (repeated) | `pendingTransition` is `<x>` | Changes nothing and returns the gate again, for example in a new session. A pending forward transition returns approval required and the current checklist, also if its gate no longer needs an approval (**Proposed**); a pending reopen returns the reopen gate. **Decided** on 2026-10-18 (findings clarification). | `transition-pending` |
| `phase start <current>` (re-entry) | `<current>` is `currentPhase` and `in-progress` | Changes nothing; no gate, no transition, the skills of the current phase, so a phase skill can run again in a new session or for another `refine` round. **Decided** on 2026-10-18. | `already-in-progress` |
| `phase start <current>` | `<current>` is `currentPhase` and `awaiting-approval` | Changes nothing; no gate, `transition` is the pending transition, the skills of the current phase. The phase skill asks the human whether to discard the pending transition and, on yes, calls `phase reject <pendingTransition>` (package 06). **Decided** on 2026-10-18 (findings clarification). | `resume-pending` |
| `phase start <earlier>` (reopen) | `<earlier>` comes before `currentPhase`, the current phase is `in-progress`, and `<earlier>` can be reopened | Current phase → `awaiting-approval`, `pendingTransition` = `<earlier>`. Always approval required and no checklist. A "no" is a `phase reject <earlier>`. **Proposed.** | `transition-requested` (kind `reopen`) |
| `phase approve <x>` | `pendingTransition` is `<x>` | Forward: current phase → `approved`, `<x>` → `in-progress`. Reopen: `<x>` → `in-progress` and every later phase of the profile → `pending`; artifact files stay on disk. Then `currentPhase` = `<x>` and `pendingTransition` is removed. A pending reopen of a phase that can no longer be reopened stops with exit code 42 (**Proposed**). | — |
| `phase reject <x> [--note <text>]` | `pendingTransition` is `<x>` | Cancels the pending transition after the human said no; the current phase goes back to `in-progress` (gate failed, changes requested). **Decided** on 2026-10-18. | — |
| `phase complete` | The current phase is the last phase of the profile and `in-progress` | Last phase → `approved`, feature `status` → `done`, and the local override (package 03) is deleted if it selects this feature (`overrideCleared`). No approval, because the transition into the last phase was approved already. **Proposed.** | — |

Every other request stops with an exit code and a reason the skill can show to the human ([Errors](#errors)); `feature.json` is unchanged. `phase start`, `approve`, `reject`, and `complete` on a `done` feature stop with exit code 44; `phase skills` and `phase next` stay allowed.

The diagram in concept §11.3 stays valid: `pending → in-progress` happens through `phase approve`, `in-progress → awaiting-approval` through `phase start <next>`, and `awaiting-approval → in-progress` through `phase reject`.

Because every phase skill calls `phase start <its phase>` as the first step of its entry protocol (package 06), the same call covers five situations: moving forward, repeating a pending transition, re-entering a phase in progress, resuming a phase whose transition is pending, and reopening an earlier phase.

`phase complete` deletes the override after `feature.json` was written; an override that cannot be read because of its format is left alone. A failing delete is not mapped and ends with exit code 255 (`host-error`), like the delete in `feature drop` (package 03); the feature is `done` by then. **Proposed.** Note: a repository whose branch does not select the completed feature has no current feature once the override is cleared, so `phase next` there stops with exit code 37. The `done` answer of `phase next` is reached through the feature's branch or after `feature set <id>`. Checked with the built CLI on 2026-10-26.

### Approval

- Every transition needs an explicit human approval in v1. Package 05 adds switching it off per transition. **Decided** (concept §5.2).
- The agent asks the human, and only after the human says yes does it call `phase approve`. **Decided** (concept §3.2).
- **Known weak point (Decided, accepted for v1):** nothing technically stops an agent from calling `phase approve` on its own. Later options: `approve` only in an interactive terminal, or a hook that blocks the call.

### `phase next --json`

Returns the possible next steps of the current feature, so `shade-kit-next-phase` (package 06) does not derive them from the profile. **Decided** on 2026-10-18 (findings clarification). It changes nothing. **Implemented** (`PhaseEngine.GetNext`, `PhaseNextCommand`).

| Field | Content |
|---|---|
| `current` | `{ featureId, phase, state, status }`. For a `done` feature every other field is `null`. |
| `continue` | `{ phase, skill }` of the current phase when it is `in-progress`. |
| `pending` | `{ phase, gate: { approval, checklist }, skill }` of the target phase when the current phase is `awaiting-approval`. |
| `forward` | `{ phase, gate, skill }` of the next phase when the current phase is `in-progress` and not the last phase. |
| `reopen` | When the current phase is `in-progress`: list of `{ phase, skill }` of the earlier phases that can be reopened, in profile order; `[]` if there is none (for example in `new`). A reopen always needs an approval. |

Parts that do not apply are written as `null`, following `CliJsonWriter`, which writes null values. **Proposed.**

### `phase skills <phase> --json`

Returns the phase skill for the feature's profile and the general skills it needs (package 06). The same list is part of the `phase start` result. Extension skills are added here later (concept §7). **Implemented** (`PhaseSkillsCommand`).

### Output

**Decided** on 2026-10-26 (implementation clarification). **Implemented** (`Phase<X>CommandData`).

- `--json`: one envelope (package 01). Enum values are kebab-case. `data` per command:

  | Command | `data` |
  |---|---|
  | `phase start <phase>` | `{ featureId, profile, outcome, transition: { kind, from, to }, currentPhase: { name, state }, gate: { phase, approval, checklist }, skills: { phaseSkill, generalSkills } }`; `kind` is `forward` or `reopen`; `currentPhase` is the state after the call; `transition` is `null` for `already-in-progress` and the pending transition for `resume-pending`; `gate` is `null` for `already-in-progress` and `resume-pending`; `skills` are those of the requested phase, for `already-in-progress` and `resume-pending` those of the current phase |
  | `phase approve <phase>` | `{ featureId, transition: { kind, from, to }, currentPhase: { name, state } }` |
  | `phase reject <phase>` | `{ featureId, rejected: { kind, from, to }, currentPhase: { name, state }, note }`; `note` is `null` without a note |
  | `phase complete` | `{ featureId, phase, status, overrideCleared }` |
  | `phase skills <phase>` | `{ featureId, profile, phase, phaseSkill, generalSkills }` |
  | `phase next` | see [`phase next --json`](#phase-next---json) |

  `warnings` holds the warnings of resolving the current feature and reading `feature.json` (packages 01 and 03) and of writing it (package 02), in the order of package 03.
- `--note` (short `-n`) of `phase reject` is trimmed; an empty or whitespace-only note is no note; line breaks are allowed. **Decided** on 2026-10-26 (implementation clarification).
- Plain: `Key: value` lines like `feature show`, for example `Feature:`, `Outcome:`, `Transition: <from> -> <to> (<kind>)`, `Current phase: <name> (<state>)`, `Gate:`, `Phase skill:`, `General skills:`; `phase start` adds an approve and reject hint while a transition is pending. The wording is **Proposed**.

### Errors

**Decided** on 2026-10-26 (implementation clarification). **Implemented** (`ShadeKitCliExitCodes`, `ShadeKitCliErrorCodes`, `CliErrorMapper`; exceptions in `Workflow/Engine/Exceptions/`, `Workflow/Profiles/Exceptions/`, and `Features/Exceptions/`). Every error leaves `feature.json` unchanged.

| Exit code | Error code | When |
|---|---|---|
| 36 | `git-identity-missing` | A writing phase call without `user.name` or `user.email`. The identity is read only when a write follows, so a no-op never stops on it (code of package 03). |
| 37 | `no-current-feature` | Neither the branch nor the local override selects an existing feature (`NoCurrentFeatureException`). The message points to `shade-kit feature list` and `shade-kit feature set <id>`. In the features range (30–39), because it is about features. |
| 40 | `unknown-profile` | `feature new` with an unknown `--profile` or `defaultProfile`, or a `feature.json` whose `profile` the catalog does not know (`UnknownProfileException`). The message lists the known profiles. |
| 41 | `phase-not-in-profile` | The phase is not a phase of the feature's profile (`PhaseNotInProfileException`). The message lists the phases of the profile. |
| 42 | `transition-not-allowed` | The transition would skip phases; `phase start <x>` while a transition is pending and `<x>` is neither the current phase nor the pending target; a reopen while a transition is pending; a reopen of a phase that cannot be reopened (`PhaseTransitionNotAllowedException` with `PhaseTransitionRefusalReason`). The message names the current phase and state and points to `shade-kit phase next`. |
| 43 | `no-pending-transition` | `phase approve` or `phase reject` without a pending transition to that phase (`NoPendingTransitionException`). The message names the actual pending transition, if any. |
| 44 | `feature-done` | `phase start`, `approve`, `reject`, or `complete` on a `done` feature (`FeatureDoneException`). |
| 45 | `complete-not-allowed` | `phase complete` outside the last phase, or while the last phase is not `in-progress` (`PhaseCompleteNotAllowedException`). |
| 46 | `phase-state-invalid` | The phase fields of `feature.json` do not fit the profile or contradict each other (`InvalidPhaseStateException`, [Phase states](#phase-states-and-featurejson)). The message names the file and the field. Also from `feature list`, `current`, and `show`. |

Check order: resolve the current feature (37) → read `feature.json` (20–22 as in package 01; reading commands migrate in memory, writing commands need the current format) → profile known (40) → phase state valid (46) → feature not `done` (44, for `start`, `approve`, `reject`, `complete`) → phase in the profile (41) → transition rule (42, 43, 45) → git identity (36, only if a write follows) → write. Reused codes: 10, 12, 15, 16, 20–22, 2 (`invalid-arguments`, for example a missing phase argument), and 130 (packages 01–03). **Implemented** (`PhaseCommandsEndToEndTests`, `CliErrorMapperTests`).

### Merges

`feature.json` is changed by one developer on one branch in the normal case. If a merge conflict happens anyway, the file is plain JSON that a human can resolve; the engine gives no extra support in v1. **Proposed.**

## Acceptance criteria

Every criterion is **Implemented**. The tests that prove it carry `[Trait("Requirement", "FP04-ACn")]`. Git is faked in every test; the history timestamps come from a `FakeTimeProvider`. The commands were also run with the built CLI on macOS on 2026-10-26.

- **FP04-AC1** A new feature of every profile has `new` `in-progress` and every other phase `pending`. Proven by `FeatureStoreCreateTests`, `PhaseEngineReadStateTests`, `FeatureCommandsEndToEndTests`.
- **FP04-AC2** `phase start` with the next phase of the profile sets the current phase to `awaiting-approval` and returns the skills to load. Proven by `PhaseEngineStartTests`, `PhaseWorkflowStartTests`, `PhaseStartCommandTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC3** `phase start` with a phase that is not next, not current, and not earlier stops with an exit code; `feature.json` is unchanged. Proven by `PhaseEngineStartTests`, `PhaseWorkflowStartTests`, `CliErrorMapperTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC4** `phase approve` without a matching pending transition stops with an exit code. Proven by `PhaseEngineApproveTests`, `PhaseWorkflowApproveRejectTests`, `CliErrorMapperTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC5** `phase approve` records time, git user, from, and to in `history` and moves `currentPhase`. Proven by `PhaseWorkflowApproveRejectTests`, `PhaseApproveCommandTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC6** `phase reject <next>` while the transition to `<next>` is pending returns the current phase to `in-progress`, removes the pending transition, and records `reject` with the note; without a matching pending transition it stops with an exit code. Proven by `PhaseEngineRejectTests`, `PhaseWorkflowApproveRejectTests`, `PhaseRejectCommandTests`, `CliErrorMapperTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC7** An approved reopen of an earlier phase sets every later phase to `pending` and keeps their artifact files. Proven by `PhaseEngineApproveTests`, `PhaseWorkflowApproveRejectTests`, `PhaseApproveCommandTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC8** `phase complete` in `finish` sets the feature to `done`; in any other phase it stops with an exit code. Proven by `PhaseEngineCompleteTests`, `PhaseWorkflowCompleteTests`, `PhaseWorkflowRobustnessTests`, `PhaseCompleteCommandTests`, `CliErrorMapperTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC9** `bugfix` contains `diagnose` and no `refine` or `map`; `quick` contains only `new`, `quick`, `finish`. Proven by `BuiltInWorkflowProfilesTests`, `PhaseEngineSkillsTests`.
- **FP04-AC10** `phase skills` returns a different phase skill list per profile where the profiles differ. Proven by `BuiltInWorkflowProfilesTests`, `PhaseEngineSkillsTests`, `PhaseWorkflowReadTests`, `PhaseSkillsCommandTests`.
- **FP04-AC11** `phase start <current>` while the current phase is `in-progress` leaves `feature.json` byte-for-byte unchanged and returns "already in progress" without a gate. Proven by `PhaseEngineStartTests`, `PhaseWorkflowStartTests`, `PhaseStartCommandTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC12** `phase start <next>` while the transition to `<next>` is already pending returns the same gate again and adds no `history` entry. Proven by `PhaseEngineStartTests`, `PhaseWorkflowStartTests`, `PhaseStartCommandTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC13** `phase start <current>` while the transition away from `<current>` is pending leaves `feature.json` unchanged and returns the pending transition without a gate. Proven by `PhaseEngineStartTests`, `PhaseWorkflowStartTests`, `PhaseCommandsEndToEndTests`.
- **FP04-AC14** `phase next --json` returns `continue`, `forward`, and `reopen` for a phase `in-progress`, `pending` for a phase `awaiting-approval`, no `forward` in the last phase, and only `current` for a `done` feature; `feature.json` is unchanged. Proven by `PhaseEngineNextTests`, `PhaseWorkflowReadTests`, `PhaseNextCommandTests`, `PhaseCommandsEndToEndTests`.

## Open points

- Whether a profile can be changed on an existing feature (for example `quick` turns out to be a `feature`) is **Open**. v1 proposal: drop and create a new feature. Changing a profile definition is covered: it applies live to existing features ([Architecture](#architecture)).
- The `report` setting (`config.json`, package 02) is not part of the result of `phase start finish`, although the skeleton `skills/shade-kit-finish/SKILL.md` expects it there. Owned by package 15. **Open.**
- The known weak point of approvals is unchanged ([Approval](#approval)).
- `at` in history entries has fractional seconds, while `createdAt` has whole seconds. Whether `at` should be cut to whole seconds is **Open**.
- Defaults chosen in the implementation, not confirmed yet, all **Proposed**: a repeated `phase start` of a pending forward transition always returns approval required; `phase approve` of a pending reopen whose phase can no longer be reopened stops with exit code 42; the conditions for a `feature.json` without `currentPhase` (a hand-edited `done` feature without phase fields stops with exit code 46); the done rule (current phase `approved`, no pending transition) and that only the current phase's state is checked; `phase complete` checks the override before the write and deletes it after, and a failing delete ends with exit code 255; the plain-text wording; parts of `phase next` that do not apply written as `null`.
