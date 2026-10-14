# 01 · Store foundation and format versioning

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §3.1, §3.2, §4.1, §9; [ADR 0001](../adr/0001-shadekit-version-drift.md) |
| Depends on | — |
| Enables | Every later package |

Labels follow the concept: **Decided**, **Proposed**, **Open**, **Unverified**.

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

- The kit root is `.shadekit/` at the repository root. **Decided.**
- Commands find it by walking up from the working directory to the git repository root. A command that needs an initialised repository and finds no kit root stops with a clear message that points to `shade-kit init`.

### Versioned files (ADR 0001)

- Every JSON file in `.shadekit/` has a top-level integer `schemaVersion`. **Decided.**
- Each file type (`config`, `gates`, `feature`, `current-feature`, `templates-lock`, the user-level integration record from package 07, and later ones) has its own format version and its own migration chain of small steps (v1→v2→v3). **Decided.**
- Fields the engine does not know are kept unchanged when the file is written back. Adding a field never raises the format version. **Decided.**
- A file without `schemaVersion` is treated as invalid, not as version 1. **Proposed**, because a missing version most likely means a hand-edited or foreign file.

### Version check on every command

| Situation | Behaviour |
|---|---|
| File format is newer than the engine supports | Hard stop. Exit code from `ShadeKitCliExitCodes`, message "shade-kit >= X required". **Decided.** |
| File format is older than the engine's current format | Write commands stop and report that `shade-kit upgrade` is needed. **Decided.** Read commands migrate in memory, return their result, and add an "upgrade needed" warning. **Proposed.** |
| File format is current | Normal operation. |

The check covers only the files a command touches. A repository that contains files in several format versions after a merge is a valid state (ADR 0001).

### `shade-kit version`

- Plain output: the engine version. **Decided** (exists already).
- `--json` output: engine version and the current format version per file type. **Proposed.** Skills use it for the version handshake (package 06), and package 07 adds the installed integration versions to it.

### Agent contract (applies to every agent-facing command)

- Every command an agent calls supports `--json`. **Decided.**
- JSON output goes to stdout. Warnings and errors in JSON mode are part of the JSON document, not free text on stderr. **Proposed.**
- Every failure has a stable exit code from `ShadeKitCliExitCodes` and, in JSON mode, a stable error code string, so skills can branch on it without parsing messages. **Proposed.**
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

## Open points

- None of its own. Concept open question 6 (exact command names) applies to all packages.
