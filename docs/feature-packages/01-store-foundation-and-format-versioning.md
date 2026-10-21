# 01 · Store foundation and format versioning

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (implementation state; package 02 implementation: missing `features/` is valid, exit codes 11, 13, 23; code review rework: exit codes 14, 24) and 2026-10-21 (package 03 implementation: exit codes 15, 16, 30–34, 36, 40, warning codes, store file operations; code review rework: exit codes 2 and 130, warning codes `feature-folder-invalid`, `feature-id-mismatch`, `current-feature-invalid`, `MoveDirectory`; second code review: exit code 35). Implemented except the integration versions in `version --json` (package 07). |
| Source | Concept §3.1, §3.2, §4.1, [§9](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | — |
| Enables | Every later package |

Labels follow the concept: **Decided**, **Proposed**, **Open**, **Unverified**, **Implemented**.

## Benefit

Every file the kit writes into a repository is safe across branch merges and kit upgrades from day one. Later packages only add new file types to an existing, tested mechanism instead of each inventing their own version handling. Agents get one predictable way to talk to the engine.

## Scope

### In scope

- Locating the kit root `.shadekit/` from any directory inside a repository.
- Versioned JSON files: `schemaVersion` in every JSON file, unknown fields preserved, a migration chain per file type.
- The version check that runs before every command (hard stop or "upgrade needed").
- `shade-kit version`, including the data an agent or skill needs for the version handshake.
- The agent contract shared by all agent-facing commands: `--json` output, error shape, exit codes.

### Out of scope

- The `upgrade` command that actually rewrites files (package 18). This package only provides the migration chains it will run.
- Template hashing and the lock file (package 02 writes it, package 18 evaluates it).
- Any concrete file content beyond the version fields (`config.json` in 02, `feature.json` in 03/04, `gates.json` in 05).

## Functional description

### Kit root

- The kit root is `.shadekit/` at the repository root. **Decided** (concept §4.1).
- Commands find it by walking up from the working directory to the git repository root. A command that needs an initialised repository and finds no kit root stops with a clear message that points to `shade-kit init`. **Implemented** (`KitStoreFactory`, `GitRepositoryLocator`, `KitStorePaths`).
- A kit root without a `features/` directory is valid and has no features. **Decided** on 2026-10-18 (package 02 implementation clarification), because git does not keep the empty `features/` that `init` creates, so a fresh clone has none. The former exit code 11 (`features-dir-not-found`) is removed. **Implemented** (`KitStore.GetFeatureNames` returns an empty list).
- Besides the versioned JSON files, the store reads and writes plain files and directories inside the kit root (`ReadFileAsync`, `WriteFileAsync`, `FileExists`, `DirectoryExists`, `GetFiles`, `DeleteFile`, `DeleteDirectory`, `MoveDirectory`); a path that points outside the kit root is rejected. All except `WriteFileAsync` were added for package 03 on 2026-10-21. `MoveDirectory` is a single rename that refuses an existing file or directory at the destination (on some systems, an empty directory created at the destination between the check and the rename can still be replaced); package 03 uses it to move a new feature into place and to hide a feature before it deletes it. **Decided** on 2026-10-21 (package 03 code review rework). **Implemented** (`IKitStore`, `KitStore`).

### Versioned files (concept §9.1, §9.4)

- Every JSON file in `.shadekit/` has a top-level integer `schemaVersion`. **Decided** (concept §9.1). The version lives in the file it describes: every `features/<id>/feature.json` carries its own, so features created on different branches with different engine versions stay independent. **Decided** (concept §9.1).
- Every JSON file also has a top-level `minEngineVersion` (semver string): the first engine version that supports the file's format version. The engine stamps it on every write from an internal table "format version → engine version that introduced it". An older engine reads it without understanding the rest of the format. **Decided** (concept §9.1). **Implemented** (`VersionedDocument`, `VersionedJsonSerializer`).
- Each file type has its own format version and its own migration chain of small steps (v1→v2→v3). **Decided** (concept §9.1). **Implemented** (`FormatDefinition`, `IFormatMigration`). File types are identified by these logical ids, all starting at format version 1. **Decided** (implemented in code). **Implemented** (`FormatIds`, `FormatRegistry.CreateDefault()`).

  | Format id | File |
  |---|---|
  | `config` | `.shadekit/config.json` |
  | `gates` | `.shadekit/gates.json` |
  | `feature` | `.shadekit/features/<id>/feature.json` |
  | `current-feature` | `.shadekit/features/current-feature.json` |
  | `templates-lock` | `.shadekit/templates.lock.json` |
  | `integrations` | User-level integration record (package 07) |

- Fields the engine does not know are kept unchanged when the file is written back. Adding a field never raises the format version. **Decided** (concept §9.4). **Implemented** (`[JsonExtensionData]` in `VersionedDocument`).
- A file without `schemaVersion` is treated as invalid, not as version 1, and every command that touches it stops (`invalid-file`). **Decided** (concept §9.1), because a missing version most likely means a hand-edited or foreign file. **Implemented** (`InvalidKitFileException`).

### Version check on every command

| Situation | Behaviour |
|---|---|
| File format is newer than the engine supports | Hard stop (`format-too-new`), message "shade-kit >= X required" with X taken from the file's `minEngineVersion`. **Decided** (concept §9.2). |
| File format is older than the engine's current format | Write commands stop and report that `shade-kit upgrade` is needed (`upgrade-needed`). **Decided** (concept §9.2). Read commands follow `outdatedFormatOnRead` in `config.json` (package 02): `migrate-and-warn` (default) migrates in memory, returns the result, and adds an `upgrade-needed` warning; `stop` behaves like a write command. **Decided** (concept §9.2). To read this setting, an outdated `config.json` is itself migrated in memory. |
| File format is current | Normal operation. |

**Implemented** (`KitStore`, `FormatTooNewException`, `UpgradeNeededException`, `OutdatedFormatOnReadPolicy`). The check covers only the files a command touches. A repository that contains files in several format versions after a merge is a valid state (concept §9).

### `shade-kit version`

- Plain output: the engine version. **Decided** (implemented in code). **Implemented** (`VersionCommand`).
- `--json` output: engine version and the current format version per file type, keyed by format id (`"formats": { "config": 1, … }`). This is the engine's capability, not the state of a repository. **Decided** (implemented in code). **Implemented** (`VersionCommandData`). Skills use it for the version handshake (package 06), and package 07 adds the installed integration versions to it.

### Agent contract (applies to every agent-facing command)

- Every command an agent calls supports `--json`. **Decided** (concept §3.2).
- JSON output goes to stdout. Warnings and errors in JSON mode are part of the JSON document, not free text on stderr. **Decided** (implemented in code).
- Every JSON output uses the same envelope. **Decided** (implemented in code).

  ```json
  {
    "ok": true,
    "data": { },
    "warnings": [ { "code": "upgrade-needed", "message": "…" } ],
    "error": null
  }
  ```

  On failure, `ok` is `false`, `data` is `null`, and `error` is `{ "code": "…", "message": "…" }`. **Implemented** (`CliJsonEnvelope`, `ICliJsonWriter`, `CliErrorMapper`).
- Every failure has a stable exit code from `ShadeKitCliExitCodes` and, in JSON mode, a stable error code string, so skills can branch on it without parsing messages. **Decided** (implemented in code). Error and warning codes are kebab-case. **Decided** (implemented in code). Codes that only appear as warnings are in `ShadeKitCliWarningCodes`: `current-feature-mismatch`, `current-feature-missing`, `current-feature-invalid`, `feature-file-missing`, `feature-folder-invalid`, and `feature-id-mismatch`, all from package 03; `upgrade-needed` is both an error code and a warning code. **Implemented** (`ShadeKitCliWarningCodes`, since 2026-10-21).
- Exit codes are grouped in ranges per area: 10–19 store and kit root, 20–29 format and version, 30–39 features, 40–49 phases and gates, 255 host errors. **Decided** (implemented in code). Two codes are outside the area ranges, because they apply to every command: 2 (`invalid-arguments`) is the usage code for arguments the option parser rejects, for example a missing required argument; the message names the problem and points to `shade-kit <command> --help`, and there is no stack trace. **Decided** on 2026-10-21 (package 03 code review rework). 130 (`canceled`, 128 + SIGINT) ends a run canceled with Ctrl+C. **Decided** on 2026-10-21 (package 03 code review rework). Both are **Implemented** (`CliErrorMapper.MapArgumentError`, `CliErrorMapper.MapCancellation`, `Program`); in JSON mode each writes one failure envelope. An option class the parser cannot create is a defect of the CLI and stays 255 (`host-error`). Codes owned by packages 01, 02, and 03, all **Implemented** (`ShadeKitCliExitCodes`, `ShadeKitCliErrorCodes`, `CliErrorMapper`). Exit code 11 is free since 2026-10-18 (see [Kit root](#kit-root)). Exit code 35 (`feature-io-failed`) was added on 2026-10-21 after the second code review of package 03. Exit code 40 is in the range of package 04, because profiles belong to it; package 03 owns the profile check until package 04 exists:

  | Exit code | Error code | Owner |
  |---|---|---|
  | 2 | `invalid-arguments` | 03 (usage, every command) |
  | 10 | `kit-root-not-found` | 01 |
  | 12 | `not-a-git-repository` | 02 |
  | 13 | `already-initialized` | 02 |
  | 14 | `path-not-a-file` | 02 |
  | 15 | `kit-root-not-a-directory` | 02 |
  | 16 | `git-unavailable` | 03 |
  | 20 | `format-too-new` | 01 |
  | 21 | `upgrade-needed` | 01 |
  | 22 | `invalid-file` | 01 |
  | 23 | `invalid-managed-section` | 02 |
  | 24 | `unsupported-encoding` | 02 |
  | 30 | `feature-already-exists` | 03 |
  | 31 | `feature-not-found` | 03 |
  | 32 | `invalid-feature-id` | 03 |
  | 33 | `invalid-source` | 03 |
  | 34 | `invalid-title` | 03 |
  | 35 | `feature-io-failed` | 03 |
  | 36 | `git-identity-missing` | 03 |
  | 40 | `unknown-profile` | 03 (until 04) |
  | 130 | `canceled` | 03 (every command) |
  | 255 | `host-error` | 01 |

- "Not found" or "not resolved" answers that are part of normal flow (for example no current feature) are results, not errors. **Proposed.**
- Command names in this and all later packages are the proposals from the concept §3 (concept open question 6).

## Acceptance criteria

- **FP01-AC1** A command run in a subdirectory of an initialised repository finds `.shadekit/` at the repository root.
- **FP01-AC2** A command that needs the kit root, run in a repository without `.shadekit/`, stops with an exit code and points to `shade-kit init`.
- **FP01-AC3** Reading and writing a JSON file keeps every unknown field and its value unchanged.
- **FP01-AC4** A file with a `schemaVersion` above the supported one stops every command that touches it, with the minimum engine version in the message.
- **FP01-AC5** A file with an older `schemaVersion` makes write commands stop with "upgrade needed" and leaves the file byte-for-byte unchanged.
- **FP01-AC6** A migration chain for a file type can be registered and applied in memory; a test migrates a v1 fixture through a sample v2 step.
- **FP01-AC7** `shade-kit version --json` returns the engine version and one format version per file type.
- **FP01-AC8** In JSON mode, every error produces a JSON document with an error code and a non-zero exit code.
- **FP01-AC9** Every JSON file the engine writes carries the `minEngineVersion` that belongs to its `schemaVersion`.
- **FP01-AC10** A read command on a file with an older `schemaVersion` returns its result with an `upgrade-needed` warning when `outdatedFormatOnRead` is `migrate-and-warn` or absent, and stops with exit code 21 when it is `stop`.
- **FP01-AC11** A file without `schemaVersion` stops every command that touches it with exit code 22 (`invalid-file`).
- **FP01-AC12** Every `--json` output, success or failure, has the envelope fields `ok`, `data`, `warnings`, and `error`.

## Open points

- None of its own. Concept open question 6 (exact command names) applies to all packages.
