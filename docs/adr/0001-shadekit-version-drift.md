---
status: proposed
date: 2026-10-13
---

# Handling version drift between `.shadekit/` and the installed engine

A repository initialised by SHADE kit keeps its kit data in `.shadekit/`, fully checked into git. The installed `shade-kit` engine can be newer than that data (the repository needs an upgrade) or older (someone else already upgraded the repository). We decided that every JSON file in `.shadekit/` carries its own format version and has its own migration chain, that templates are tracked through a hash lock file, that an engine refuses to work with a format it does not know, and that data is only ever rewritten by an explicit `shade-kit upgrade`. Per-file versions are the only option we found that stays correct after git merges of branches with different format versions.

## Context

State of the code when this was decided:

- `.shadekit/` is the kit root. It is meant to hold `config.json`, `features/`, and `templates/` (`src/CreativeCoders.ShadeKit.Core/Store/KitStorePaths.cs`).
- The engine reads only `features/` so far (`KitStore.GetFeatureNames`). Nothing stores a version yet, and there is no migration logic.
- The kit repository ships `templates/spec.template.md`, `plan.template.md`, and `tasks.template.md`, which are still empty. How they are shipped (embedded resource or plain files) is not decided yet.

Requirements gathered for this decision:

| Topic | Requirement |
|---|---|
| Templates | Users may change them freely. They are plain text: no engine or skill parses their structure. A new kit version of a template is offered as a diff, and the user decides whether to take it. |
| `config.json` | Users own the values. The engine owns the schema and migrates it. |
| `features/` (workflow state) | Owned by the engine. `upgrade` migrates every feature in place, including features that are in the middle of a phase. |
| Skills and agent files | Not stored in the target repository. The plugin and the CLI are released together with the same version number, and a skill checks `shade-kit version` before it runs. |
| Measure of "newer" | An integer format version per artifact type. It changes only when the structure changes, not with every engine release. |
| Repository newer than engine | Hard stop with an exit code from `ShadeKitCliExitCodes` and the message "shade-kit >= X required". |
| Engine newer than repository | Commands report that `shade-kit upgrade` is needed. Only `upgrade` writes migrated data. |
| Unknown JSON fields | Preserved on read and write (round-trip, for example via `[JsonExtensionData]`). Adding a field therefore does not need a new format version. |
| Drift directions | Both directions matter. Several engine versions running in one repository at the same time is not a target scenario. |

## Decision

1. **Format version in every JSON file.** `config.json` and every feature state file carry a `schemaVersion` field. Each artifact type has its own chain of small migration steps (v1→v2→v3) that work on `JsonNode`.
2. **Version check on every command.** If a file has a `schemaVersion` higher than the engine supports, the command stops with an exit code and names the minimum engine version. If a file is older, the command reports that `shade-kit upgrade` is needed and writes nothing.
3. **Upgrade only through `shade-kit upgrade`.** It refuses to run while `.shadekit/` has uncommitted changes, so git is the backup. It writes all files or none: everything is written to temporary files first and then renamed into place.
4. **Unknown fields are preserved.** An older engine reading a file in a format version it knows writes back any fields it does not know, unchanged. Changes that only add fields do not raise the format version.
5. **Template lock file.** `.shadekit/templates.lock.json` records, for each shipped template, the kit version and the content hash at install time. On `upgrade`, templates the user has not changed are replaced without asking. A changed template is left as it is, and the new kit version is written next to it as `<name>.new` with a diff shown to the user.
6. **Plugin and CLI release together.** They share one version number and check each other's version at startup.

## Considered options

Every option shares the requirements above. The options differ in what lives in the repository and where versions are recorded.

| Option | Templates in the repository | `config.json` | Versions and hashes |
|---|---|---|---|
| V1 Copy + manifest | All templates are copied on `init` | Complete file | One central `.shadekit/manifest.json` with a format version per artifact type and the template hashes |
| V2 Overlay (eject) | Only templates the user has taken over with `shade-kit template eject <name>`. The engine serves the rest | Sparse: only values that differ from the defaults | Manifest only for templates that were taken over |
| **V3 Self-describing (chosen)** | As V1 | As V1 | `schemaVersion` in every JSON file, plus a small template lock file |
| V4 V1 + version pinning | As V1 | As V1 | As V1. In addition the repository pins the engine version, through `.config/dotnet-tools.json` or a `requiredVersion` field |

### The case that decides it: git merges

1. Branch A runs `upgrade`. The central manifest now says state format 3, and every feature state file has been migrated.
2. Branch B still uses the old engine and creates a new feature with state format 2.
3. After the merge, the manifest says 3, but one state file is in format 2.

With a single central version (V1, V2, V4), the engine reads that file as format 3, which leads to an error or a damaged state. The hard stop does not help, because the recorded version is wrong. With a version in every file (V3), the engine sees format 2 for that one file and reports that `upgrade` is needed.

### Scoring

Scale 1–5, 5 is best. For effort, 5 means least effort. Weights: robustness, effort, and user experience ×3, agent neutrality ×1.

| Criterion | V1 | V2 | V3 | V4 |
|---|---|---|---|---|
| Robustness / data safety | 3: manifest and files can disagree after merges | 4: fewer files to migrate, but the same merge problem; templates not taken over can differ between team members with different engine versions | **5**: every file describes itself, safe across merges | 4: pinning stops the team from running different versions, but the merge problem remains |
| Implementation effort | 4: manifest, hashes, `.new` files, migration chains | 2: resolving each template from the engine or the repository, an `eject` command, and a sparse config that is awkward to serialise with System.Text.Json | **4**: as V1; checks and migrations per file are simpler | 3: nearly free through `.config/dotnet-tools.json`, but self-contained binaries need their own mechanism |
| User experience | 3: everything is visible, but upgrades produce large diffs and `.new` files | 4: smallest repository and few template conflicts, but templates are harder to find | 3: as V1 | 4: hardly any hard stops, but everyone must work with a locally installed tool (`dotnet tool restore`) |
| Agent neutrality | 5 | 3: Copilot and Codex expect templates as files in the repository | 5 | 5 |
| **Weighted total** | 35 | 33 | **41** | 38 |

## Consequences

- Every JSON model in the engine needs a `schemaVersion` property, a store for unknown fields, and a registered migration chain, even when it is still at version 1.
- Raising a format version is a deliberate change. It always ships a migration step and a test that migrates a fixture from the previous version.
- Within one format version, changes may only add fields, and the engine must accept a file where the new fields are missing. Renaming, removing, or changing the meaning of a field raises the format version.
- After a merge, a repository can contain files with different format versions. This is a valid state, and `upgrade` resolves it.
- Version pinning (V4) is left as a possible later addition, as a `requiredVersion` in `config.json`. For v1 it is not needed, because the hard stop already prevents the dangerous case.
- Storing only non-default values in `config.json` (from V2) remains possible on its own. It would reduce how many migrations touch user values, but users would no longer see the available options in the file.
- Not adopted: serving templates from the engine (V2 overlay), because agents other than Claude Code would no longer see every template as a file.
