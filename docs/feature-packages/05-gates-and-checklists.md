# 05 · Gates and checklists

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (checklist format and default items), 2026-10-18 (`quick` phase; DOR-10, DOD-01, items that do not apply), 2026-10-24 (documentation check: the engine replaces `{{feature-id}}` in a checklist copy), 2026-10-25 (reference to the removed `request.md` template dropped), 2026-10-26 (package 04 implementation: gate part of the `phase start` result as built), 2026-10-27 (documentation check: "approval off" is built in the engine), 2026-11-01 (implementation clarification and implementation: `gates.json` shape and validation, cached reader with built-in defaults and warnings, gate provider, checklist copies, exit code 47), and 2026-11-04 (/simplify review of feature/feature-05: reader merged into the gate provider, ignored entries without gates, the engine applies the default gate, store write failures reported as exit code 17, `checklistCreated` in `phase start`). Implemented; checking a checklist is the agent's work (package 06). |
| Source | Concept §2 (Gates), §4.1 (`checks/`), §5.2, §6, §11.1, §11.4 |
| Depends on | 02, 04 |
| Enables | 12 (DoR), 15 (DoD), every phase transition |

## Benefit

A team decides per repository how strict the workflow is: which transitions need a human approval and which transitions must pass a checklist first. The Definition of Ready and Definition of Done are written down, can be edited by the team, and leave a filled-in record in every feature folder.

## Scope

### In scope

- `gates.json`: shape, defaults per profile, switching approval and checklist on or off.
- The checklist templates `checks/definition-of-ready.md` and `checks/definition-of-done.md` and their default content.
- Creating the feature's own copy of a checklist when its phase starts.
- The gate information in the `phase start` result (package 04).
- The rule for checklist items that do not apply to a profile.

### Out of scope

- Checking a checklist. The agent does that with the checklist-check skill (package 06). The engine does not parse checklists (concept §9.5, package 02).
- Quality checks run by the CLI and check commands configured in `gates.json` (later, concept §6).
- Approval enforcement (later, package 04 "Known weak point").

## Functional description

### Gate model

- A gate belongs to a transition and is identified by its target phase. **Decided** (concept §5.2).
- It is checked when the next phase starts. **Decided** (concept §5.2).
- It has two independent switches: `approval` (a human must approve) and `checklist` (path of a checklist the agent must check first, or none). **Decided** (concept §5.2) that each gate can be switched off; switching off approval and checklist independently answers concept open question 4, **Decided** (confirmation list, 2026-10-18).
- DoR is the checklist of the gate into `plan`. DoD is the checklist of the gate into `finish`. All other gates have no checklist by default. **Decided** (concept §5.2).
- Any transition can get a checklist by pointing its gate to a `checks/<phase>.md` template. **Decided** as an extension point (concept §5.2). **Implemented**: a gate can name any template under `checks/` ([Checklists](#checklists)).

**Implemented** in the Core namespace `CreativeCoders.ShadeKit.Core.Gates` (`Core/Gates/`). All services are singletons registered by `AddShadeKitCore()` with `TryAddSingleton`.

### `gates.json`

Written by `init` (package 02), owned by the team for its values, versioned like every JSON file (package 01). It stays format version 1; no migration. **Decided** on 2026-11-01 (implementation clarification). **Implemented** (`GatesConfig`, `GateConfig`, `FormatIds.Gates`).

The concept's example is keyed by phase only. Because the `quick` profile needs different defaults than `feature` (one approval at the end), gates are keyed by profile and then by target phase. **Decided** (confirmation list, 2026-10-18); the concept now describes this shape.

```json
{
  "schemaVersion": 1,
  "profiles": {
    "feature": {
      "refine":    { "approval": true },
      "map":       { "approval": true },
      "plan":      { "approval": true, "checklist": "checks/definition-of-ready.md" },
      "implement": { "approval": true },
      "verify":    { "approval": true },
      "finish":    { "approval": true, "checklist": "checks/definition-of-done.md" }
    },
    "bugfix": {
      "diagnose":  { "approval": true },
      "plan":      { "approval": true, "checklist": "checks/definition-of-ready.md" },
      "implement": { "approval": true },
      "verify":    { "approval": true },
      "finish":    { "approval": true, "checklist": "checks/definition-of-done.md" }
    },
    "quick": {
      "quick":     { "approval": false },
      "finish":    { "approval": true, "checklist": "checks/definition-of-done.md" }
    }
  }
}
```

- A transition with no entry uses `{ "approval": true }`. Approval is the safe default. **Decided** (confirmation list, 2026-10-18). **Implemented** (`TransitionGate.Default`).
- With `approval: false`, `phase start` performs the transition at once and records it in `history` as `approve` with the note "approval off". **Implemented** in the engine (package 04, `PhaseEngine`), reachable through `gates.json` since 2026-11-01.
- With `approval: false` and a `checklist`, the checklist copy is still created and checked by the agent, but a failed item is only reported as a warning; it does not block, because nobody is asked to decide. An agent never approves on its own instead. **Decided** (confirmation list, 2026-10-18). The copy is **Implemented** (outcome `transition-performed`, [Checklists](#checklists)); the warning is the agent's part (package 06).
- The defaults follow concept §5.2: every transition needs an approval, except the `quick` profile's transition into the `quick` phase (concept §5.4: "one approval at the end"). **Implemented** (`GatesConfig.CreateDefault()`, written by `init`).

Shape of an entry. `approval` optional with default `true`, `checklist` optional or `null`, and the validation of a `checklist` value: **Decided** on 2026-11-01 (implementation clarification). The other rules (the type of `approval`, an explicit `null` for `profiles`, for a profile, or for a gate, duplicate keys, case-sensitive field names) were chosen in the implementation and are **Proposed**. All **Implemented** (`GatesConfig`, `GateConfig`, `GatesConfigTests`).

- `approval` is a boolean; a missing field means `true`. `null` or another type makes the file invalid (exit code 22, `invalid-file`).
- `checklist` is missing or `null` for no checklist (`null` is not written back). A value must be a relative path with `/` separators that starts with `checks/`, ends with `.md` after a file name, and has no `\`, no `:`, no control character, no empty segment, and no segment that ends with `.` or a space (which covers `.` and `..`), for example `checks/definition-of-ready.md` or `checks/team/security.md`; any other value makes the file invalid (exit code 22). The message names the profile and the gate and shows a control character of the value as `\uXXXX`. Rejecting `:` (on Windows an alternate data stream, for example `checks/x.md:hidden.md`) and control characters: **Decided** on 2026-11-04 (review R05-03), **Implemented** (`GatesConfig.IsValidChecklistPath`, `GatesConfigTests`, `FeatureChecklistWriterTests`). Rejecting a segment that ends with `.` or a space (on Windows `checks/a./x.md` and `checks/a/x.md` name the same file), on every operating system, so a `gates.json` is valid or invalid on every client alike: **Decided** on 2026-11-04 (review of feature/feature-05), **Implemented** (`GatesConfig.IsValidChecklistPath`, `GatesConfigTests`, `FeatureChecklistWriterTests`).
- A profile or phase name (a key of `profiles` or of a profile) with a control character makes the file invalid (exit code 22), also for an unknown profile or phase; the message shows the character as `\uXXXX`, so no control character reaches the terminal or the agent. **Decided** on 2026-11-04 (review R05-04), **Implemented** (`GatesConfig.OnDeserialized`, `GatesConfigTests`).
- An explicit `"profiles": null`, a `null` profile, or a `null` gate makes the file invalid, and so does a duplicate key. A missing `profiles` field stays apart from an empty one (`{}`) (see [Reading](#reading-gatesjson)).
- Field names are case-sensitive: `"Approval"` is kept as an unknown field, and the gate keeps the default `true`. Unknown fields are kept at every level, and so are entries of unknown profiles and phases (package 01).

### Reading `gates.json`

`GatesConfigTransitionGateProvider` reads the file and replaces the `DefaultTransitionGateProvider` of package 04 behind the seam `ITransitionGateProvider` (package 04). **Decided** on 2026-11-01 (implementation clarification): a reader cached per store, the provider interface unchanged. Since 2026-11-04 the seam has one call per profile instead of one per phase: `ITransitionGateProvider.GetGatesAsync(store, profile)` returns the gates of the profile, keyed by target phase, together with the warnings of reading `gates.json` (`WorkflowProfileGates`), so `PhaseWorkflow` no longer depends on the reader; the behaviour, the order of the warnings, and the single read of `gates.json` per store are unchanged. **Decided** on 2026-11-04 (review of feature/feature-05), **Implemented** (`GatesConfigTransitionGateProvider`, `PhaseWorkflow`; `GatesConfigTransitionGateProviderTests`, `PhaseWorkflowStartTests`, `PhaseWorkflowReadTests`, `PhaseWorkflowRobustnessTests`). The separate reader (`IGatesConfigReader`, `GatesConfigReader`, `GatesConfigReadResult`) is merged into the provider, which now reads `gates.json`, checks it against the profile catalog (`IWorkflowProfileCatalog`), and keeps the cache and the warnings; every observable behaviour stays as described below. **Decided** on 2026-11-04 (/simplify review of feature/feature-05), **Implemented** (`GatesConfigTransitionGateProvider`; `GatesConfigTransitionGateProviderTests`, which take over the tests of the reader).

- The provider reads the file once per store instance, that is once per command run, and caches the result in a `ConditionalWeakTable`; calls during a read share it, also calls for another profile. Only a successful read is cached, so a failed or canceled read is tried again by the next call. The shared read is not tied to the cancellation token of the call that started it: a canceled call stops only its own wait, and the calls that joined the read still get its result. **Decided** on 2026-11-04 (review of feature/feature-05), **Implemented** (`GatesConfigTransitionGateProvider`, `GatesConfigTransitionGateProviderTests`).
- The file is read with read access (`FormatAccess.Read`), also in `phase start`: an older format version follows `outdatedFormatOnRead` (package 01), and only `stop` ends with exit code 21. Exit codes 20 and 22 as in package 01.
- A missing file or a missing `profiles` field gives the built-in defaults (`GatesConfig.CreateDefault()`) and the warning `gates-defaults-used`. **Decided** on 2026-11-01 (implementation clarification). For a missing `profiles` field, the warning names the remedy: copy the `profiles` field that `shade-kit init` writes into the file. **Decided** on 2026-11-04 (review R05-07), **Implemented** (`CliErrorMapper`).
- An explicit `"profiles": {}` is taken literally: no defaults and no warning, so every transition uses the default for a missing entry. **Decided** on 2026-11-01 (implementation clarification).
- An entry that names an unknown profile (one warning per profile entry), a phase its profile does not have, or the first phase of its profile (no transition leads into it) gives the warning `gate-entry-ignored`, for the whole file, also for other profiles than the feature's. The entry stays in the file and is not used. **Decided** on 2026-11-01 (implementation clarification). Such an entry also has no gate in the result of the provider: an unknown profile has no gates, and an unknown phase or the first phase of a profile is left out of its profile's gates. **Decided** on 2026-11-04 (/simplify review of feature/feature-05), **Implemented** (`GatesConfigTransitionGateProvider`, `GatesConfigTransitionGateProviderTests`).
- Two or more gates of one profile that name the same `checklist` (compared without regard to case on every operating system, because the default file systems of macOS and Windows store `checks/dor.md` and `checks/DoR.md` as one file) would share one copy in the feature folder, so the later gate would overwrite the filled record of the earlier one. Only the gates that apply count (a known profile, a phase after the first one). The gate into the earliest phase of the profile keeps the checklist; every later one gets the warning `gate-entry-ignored` (reason `SharedChecklist`), after the other warnings of its profile and in phase order, and its checklist is ignored: the transition has no checklist, no copy is made, and its `approval` still applies. The file stays valid. **Decided** on 2026-11-04 (review R05-01: a warning in the reader; which gate keeps the checklist and that the later ones lose it were chosen in the implementation and are **Proposed**). **Implemented** (`GatesConfigTransitionGateProvider`, `GateEntryIgnoredReason.SharedChecklist`, `CliErrorMapper`; `GatesConfigTransitionGateProviderTests`, `CliErrorMapperTests`, `PhaseCommandsEndToEndTests`). The comparison without regard to case, the same on every operating system: **Decided** on 2026-11-04 (review of feature/feature-05), **Implemented** (`GatesConfigTransitionGateProvider`, `GatesConfigTransitionGateProviderTests`).
- The provider keeps a read-only view of the profiles (the gates as `TransitionGate`) and returns the read-only gates of one profile, not the cached document, so no caller can change the cached configuration. **Decided** on 2026-11-04 (review R05-08), **Implemented**.
- The provider returns the configured gates of the feature's profile as they are, keyed by target phase, or no gates if `gates.json` has no entry for the profile. Profile and phase names are matched ordinally, so `Feature` is an unknown profile. A phase without a gate gets approval required and no checklist (`TransitionGate.Default`) from the phase engine, the only place that applies this default. **Decided** on 2026-11-04 (/simplify review of feature/feature-05; before, the provider filled in the default for every phase of the profile), **Implemented** (`GatesConfigTransitionGateProvider`, `PhaseEngine`; `GatesConfigTransitionGateProviderTests`, `PhaseEngineStartTests`).
- The warnings of reading `gates.json` are part of the result of `phase start` and `phase next`, the two commands that read the gates.

### Checklists

- The team edits the templates in `.shadekit/templates/checks/`. They are tracked by `templates.lock.json` like every template (package 02).
- When a phase with a checklist starts, the engine copies the template into the feature folder (`features/<id>/checks/definition-of-ready.md`) if the copy does not exist yet, replaces `{{feature-id}}` with the id, and returns its path. The agent checks and fills in this copy. The filled copy is the record of the gate. **Decided** on 2026-11-01 (implementation clarification; **Proposed** before). **Implemented** (`IFeatureChecklistWriter` / `FeatureChecklistWriter`, `PhaseWorkflow.StartAsync`; `FeatureChecklistWriterTests`, `PhaseWorkflowStartTests`).
- A reopened phase (package 04) reuses the existing copy; the agent rechecks every item, and the "Check runs" log keeps the earlier results visible. **Decided** on 2026-11-01 (implementation clarification). **Implemented**: an existing copy is neither read nor changed.

How the copy is made. Its path, when it is made, its content, the order in `phase start`, and that `phase next` copies nothing are **Decided** on 2026-11-01 (implementation clarification). Creating the missing directories of a nested path and the warnings of writing the copy were chosen in the implementation and are **Proposed**. All **Implemented**.

- Path: the `checklist` value of the gate is relative to `.shadekit/templates/` and is also the path of the copy relative to the feature folder. `checks/definition-of-ready.md` copies `.shadekit/templates/checks/definition-of-ready.md` to `.shadekit/features/<id>/checks/definition-of-ready.md`; a nested path such as `checks/team/security.md` creates the missing directories.
- When: only for the outcomes `transition-requested`, `transition-performed`, and `transition-pending` of a forward transition whose gate has a checklist. Never for a reopen (whose gate has no checklist, package 04), `already-in-progress`, or `resume-pending`. A repeated `phase start` of a pending transition creates a missing copy, reads no git identity, and does not write `feature.json`.
- Content: the lines of the template with every `{{feature-id}}` replaced by the feature id; the UTF-8 byte order mark and the final line ending of the template are kept. The line ending is resolved for the path of the copy, by the rules of package 02. The warnings of writing the copy are part of the result.
- Order in `phase start`: engine decision → git identity (only if `feature.json` changes) → copy → write of `feature.json`. A missing identity (exit code 36) leaves no copy, and a failing copy leaves `feature.json` unchanged. If writing `feature.json` fails after the copy was created, the copy stays; the next start finds it and keeps it.
- `phase next` returns the checklist of `pending` and `forward` but copies nothing.

### Errors

Exit code 47 is new. **Decided** on 2026-11-01 (implementation clarification). **Implemented** (`ChecklistTemplateNotFoundException` in `Gates/Exceptions/`, `ShadeKitCliExitCodes`, `ShadeKitCliErrorCodes`, `CliErrorMapper`; `CliErrorMapperTests`, `FeatureChecklistWriterTests`, `PhaseWorkflowRobustnessTests`, `PhaseCommandsEndToEndTests`). Every error leaves `feature.json` unchanged and writes no copy.

| Exit code | Error code | When |
|---|---|---|
| 47 | `checklist-template-not-found` | The copy does not exist and neither does the template the gate names. The message names the template and `gates.json` and asks to restore the template or change the `checklist` of the gate. |
| 14 | `path-not-a-file` | A directory exists at the path of the template or of the copy (code of package 02). |
| 17 | `kit-file-write-failed` | The copy cannot be written because of an I/O error, for example because a file exists in the place of a directory of the copy path (`features/<id>/checks`, or a directory of a nested checklist), or because of missing permissions. The message names the copy, the cause, and the fix. Added on 2026-11-04 (review R05-02); **Implemented** (`KitFileWriteFailedException` in `Store/Exceptions/`, `FeatureChecklistWriter`, `CliErrorMapper`; `FeatureChecklistWriterTests`, `CliErrorMapperTests`, `PhaseCommandsEndToEndTests`). Missing permissions: **Decided** on 2026-11-04 (review of feature/feature-05), **Implemented** (`FeatureChecklistWriterTests`). Since then the store reports a refused write of every file in `.shadekit/` this way, not only of the copy, also the write of `feature.json` after the copy (package 01). **Decided** on 2026-11-04 (/simplify review of feature/feature-05), **Implemented** (`KitStore`; `KitStoreTests`, `KitStoreJsonTests`, `FeatureChecklistWriterTests`). |
| 22 | `invalid-file` | `gates.json` is invalid ([`gates.json`](#gatesjson)), or the template is not UTF-8 text (code of package 01). |
| 20, 21 | `format-too-new`, `upgrade-needed` | `gates.json` has a newer format version, or an older one with `outdatedFormatOnRead: stop` (package 01). |

Check order (package 04): reading `gates.json` (20–22) comes after the phase state check (46) and before the engine rules (44, 41, 42); the copy (47, 14, 22, 17) comes after the git identity (36) and before the write of `feature.json`.

### Checklist format

**Decided** in the template interview on 2026-10-17; the general template conventions are in package 02.

- One table per checklist: `ID | Criterion | Result | Evidence`.
- Item IDs are `DOR-nn` and `DOD-nn`. Teams may add, change, or remove items; existing IDs stay stable.
- `Result` is `pending` in the template and becomes `pass`, `fail`, or `n/a` (see "Items that do not apply"). A table is used instead of checkboxes because a checkbox cannot express "not applicable".
- `Evidence` names what was checked, with IDs, paths, or command results; for `fail`, what is missing.
- On every run the agent overwrites `Result` and `Evidence` and adds one line to the "Check runs" section: date, time with its UTC offset, `pass` or `fail`, and the failed item IDs, for example `- 2026-11-08 14:05 +01:00 – fail (DOR-04, DOR-07)`. The UTC offset: **Decided** on 2026-11-08 (06-4 implementation clarification); the templates show it in their guidance comment.

### Default content

**Decided** in the template interview on 2026-10-17. It contains the four items per checklist proposed in concept §5.2 (DOR-01, DOR-04, DOR-06, DOR-10; DOD-01/02, DOD-03, DOD-05, DOD-09) and adds the rest.

| ID | Definition of Ready (gate into `plan`) |
|---|---|
| DOR-01 | Every requirement in `spec.md` has an ID (`REQ-nnn`) and at least one acceptance criterion. |
| DOR-02 | Every acceptance criterion can be decided by an automated test, or is marked "(manual)" with a reason. |
| DOR-03 | Every requirement has a priority: must, should, or could. |
| DOR-04 | `spec.md` has no open questions: "Open questions" says "None." and no `(open: Q-nnn)` marker is left. |
| DOR-05 | `spec.md` has no template leftovers: no `{{placeholder}}` and no guidance comment. |
| DOR-06 | "Out of scope" is stated, also when nothing is excluded. |
| DOR-07 | Every assumption is confirmed by a human or marked as a risk. |
| DOR-08 | Every edge case has an acceptance criterion or is listed in "Out of scope". |
| DOR-09 | "Security considerations" lists the findings, or says "None identified." with a reason. |
| DOR-10 | The affected areas are known: `map` is done, was skipped on purpose with a recorded reason, or `diagnosis.md` lists the affected code. |

| ID | Definition of Done (gate into `finish`) |
|---|---|
| DOD-01 | Build, test, and lint pass with the quality commands recorded in `review.md`, as recorded in `verification.md`. |
| DOD-02 | Coverage was measured and recorded in `verification.md`. If `config.json` sets `coverageThreshold`, the value reaches it. |
| DOD-03 | Every acceptance criterion is covered by a passing test, or is marked manual and has an item in `playbook.md`. |
| DOD-04 | Every task in `tasks.md` is done, or has a "Not done" reason that the human accepted. |
| DOD-05 | `review.md` has no open blocker. |
| DOD-06 | The quality bar was not lowered: no new analyzer or lint suppressions, skipped or deleted tests, or weakened assertions without a reason accepted in `review.md`. |
| DOD-07 | Every deviation from the plan is recorded in `plan.md` with a reason, and every migration or rollout step is done or handed over. |
| DOD-08 | The documentation matches the change (for example README, changelog, API docs), or Evidence states why no update is needed. |
| DOD-09 | Every item in `playbook.md` is checked off by a human. |

The checklist check gets the `coverageThreshold` of DOD-02 from `shade-kit gate info`, not from `config.json` ([06-4](06-4-gate-check-skill.md)). **Decided** on 2026-11-08 (06-4 implementation clarification).

### Items that do not apply

A checklist item that refers to a phase or artifact the feature's profile does not have is marked `n/a` with the reason in `Evidence`, and does not fail the gate. Examples: DOD-04 (`tasks.md`) and DOD-07 (`plan.md`) in `quick`, and DOD-09 (`playbook.md`) in `quick` when no criterion is manual (package 17). In `bugfix`, DOR-10 is not `n/a`: it passes on the "affected code" section of `diagnosis.md` (package 16). **Proposed.** This keeps one DoR and one DoD for all profiles instead of a checklist per profile.

### `phase start` result

The gate part of the result (`gate`, package 04) contains: target phase, `approval` (true or false), `checklist` (path in the feature folder, or `null`), and `checklistCreated`. Whether the transition was already performed because approval is off, and whether the phase was already in progress, is the `outcome` of the result (`transition-performed`, `already-in-progress`; package 04); for a re-entry and for a resumed phase whose transition is pending, `gate` is `null`. **Implemented** in package 04; since 2026-11-01 the gates come from `gates.json`.

- `checklist` in `phase start` and in the gates of `phase next` (`pending`, `forward`) is the value from `gates.json`, which is the path of the copy relative to the feature folder, for example `checks/definition-of-ready.md`. **Decided** on 2026-11-01 (implementation clarification). The JSON shape of both commands is unchanged. **Implemented** (`PhaseStartCommandGateData`, `PhaseNextCommandGateData`).
- `checklistCreated` in `gate` of `phase start` tells whether this call created the feature's copy of the checklist: `true` if it created the copy, `false` if the copy already existed (also when a concurrent call created it), `null` if the gate has no checklist, so no copy was ensured. In plain text, a line `Checklist copy: created` or `Checklist copy: already exists` follows the `Gate:` line when the gate has a checklist. `phase next` copies nothing, and its gates are unchanged. **Decided** on 2026-11-04 (/simplify review of feature/feature-05), **Implemented** (`PhaseStartResult.ChecklistCreated`, `PhaseWorkflow.StartAsync`, `PhaseStartCommandGateData`, `PhaseStartCommand`; `PhaseWorkflowStartTests`, `PhaseStartCommandTests`, `PhaseCommandsEndToEndTests`).
- `warnings` of `phase start` also holds the warnings of reading `gates.json` and of writing the copy; `warnings` of `phase next` those of reading `gates.json`. Format warnings come first, and every warning is reported once (package 04). **Implemented** (`PhaseWorkflow`, `CliErrorMapper`).

  | Warning code | When |
  |---|---|
  | `gates-defaults-used` | `gates.json` does not exist, or has no `profiles` field; the built-in defaults apply. |
  | `gate-entry-ignored` | An entry names an unknown profile, a phase its profile does not have, or the first phase of its profile; the entry is ignored. Or a gate names the same checklist as the gate into an earlier phase of its profile; only its checklist is ignored. |

## Acceptance criteria

Every criterion is **Implemented**. The tests that prove it carry `[Trait("Requirement", "FP05-ACn")]`. `init`, `phase start plan` with the copy of the Definition of Ready, both warnings, and exit code 47 were also checked with the built CLI on macOS on 2026-11-01.

- **FP05-AC1** `init` writes `gates.json` with the defaults above and `checks/definition-of-ready.md` and `checks/definition-of-done.md` with the default items. Proven by `GatesConfigTests`, `RepositoryInitializerTests`, `InitCommandEndToEndTests`.
- **FP05-AC2** `phase start plan` in a `feature` creates `features/<id>/checks/definition-of-ready.md` from the template and returns its path. Proven by `FeatureChecklistWriterTests`, `PhaseWorkflowStartTests`, `PhaseCommandsEndToEndTests`.
- **FP05-AC3** A second `phase start plan` after a reopen keeps the existing feature checklist unchanged. Proven by `FeatureChecklistWriterTests`, `PhaseWorkflowStartTests`, `PhaseCommandsEndToEndTests`.
- **FP05-AC4** With `approval: false` for a transition, `phase start` performs the transition and records it as "approval off" in `history`. Proven by `PhaseWorkflowStartTests`, `PhaseCommandsEndToEndTests`.
- **FP05-AC5** Without a `checklist` entry, `phase start` returns no checklist and creates no file. Proven by `PhaseWorkflowStartTests`, `PhaseCommandsEndToEndTests`.
- **FP05-AC6** A transition missing from `gates.json` requires approval. Proven by `GatesConfigTransitionGateProviderTests`, `PhaseEngineStartTests`, `PhaseCommandsEndToEndTests`.
- **FP05-AC7** In a new `quick` feature, `phase start quick` moves to `quick` without approval, and `phase start finish` asks for approval with the DoD checklist. Proven by `PhaseCommandsEndToEndTests`.
- **FP05-AC8** A team-defined `checks/<phase>.md` referenced in `gates.json` is copied and returned for that transition. Proven by `FeatureChecklistWriterTests`, `PhaseCommandsEndToEndTests`.
- **FP05-AC9** The shipped checklist templates contain the default items above, each with `Result` `pending`, and an empty "Check runs" section. Proven by `ShippedTemplatesTests`.

## Open points

- The value of the coverage threshold. DOD-02 checks `coverageThreshold` from `config.json` only when it is set (packages 02 and 14).
- Follow-ups found in the implementation on 2026-11-01:
  - **Resolved** on 2026-11-04 (review R05-03): a `checklist` value with control characters or a `:` (on Windows an alternate data stream, for example `checks/x.md:hidden.md`) is rejected ([`gates.json`](#gatesjson)).
  - **Open**: the `gate-entry-ignored` warnings follow the enumeration order of `Dictionary`, which is the file order in practice, but .NET does not guarantee it. The read-only view of R05-08 keeps that order.
  - **Resolved** on 2026-11-04 (review R05-08): the reader (since 2026-11-04 the gate provider) returns a read-only view instead of the shared mutable `GatesConfig`.
  - **Resolved** on 2026-11-04 (review R05-02): a file at `features/<id>/checks`, or at a directory of a nested checklist path, gives exit code 17 (`kit-file-write-failed`) with a failure envelope instead of exit code 255 with a stack trace; `feature.json` stays unchanged ([Errors](#errors)).
  - **Resolved** on 2026-11-04 (review R05-06): the copy is created with `FileWriteMode.CreateNew` (`ITextFileWriter`, `IKitStore.WriteFileAsync`), so of two concurrent `phase start` calls only one creates it; the other finds it ("already exists" counts as an existing copy, without warnings) and keeps it.
  - **Open** until an upgrade (package 18) writes `profiles`: repositories initialised before 2026-11-01 have a `gates.json` without `profiles`, so they get the built-in defaults with the warning `gates-defaults-used` on every `phase start` and `phase next`. Since 2026-11-04 (review R05-07) the warning names the remedy: copy the `profiles` field that `shade-kit init` writes into the file to configure the gates and stop the warning.
- Skill skeletons not aligned with this package (the skills were not changed; package 06 owns them), **Open**:
  - The entry protocol of the phase skills and `shade-kit-next-phase` does not say to show the `warnings` of `phase start` and `phase next`, so the human may never see `gates-defaults-used` or `gate-entry-ignored`.
  - The refusal table of the next-steps message (package 06) has no row for exit code 47 (restore the template or change the `checklist` of the gate), none for 14 from the copy, and none for 17 (`kit-file-write-failed`, move or remove the file in the place of a directory of the copy).
