# 18 · Repository upgrade (`upgrade`)

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-24 (documentation check: line endings of the instruction section follow package 02; staged multi-file write as open point) and 2026-10-25 (orphaned `request.md` template as open point) |
| Source | Concept §2 (Lifecycle), §3.1 (`upgrade`), [§9](../../research/shade-kit-concept.md#9-version-drift) (§9.2, §9.3, §9.5) |
| Depends on | 01, 02, 05, 08 |
| Enables | Kit releases that change a file format or a template, without stranding existing repositories |

## Benefit

When the kit evolves, a team brings its repository to the new formats with one safe command. Features in the middle of a phase keep working, templates the team changed are never overwritten, and a failed upgrade leaves no half-migrated state. This is what makes the success criterion "state survives kit upgrades" hold.

## Scope

### In scope

- `shade-kit upgrade` with `--dry-run` and `--json`.
- Migrating every versioned JSON file in `.shadekit/` with the chains from package 01.
- Updating shipped templates through `templates.lock.json`.
- Updating the managed instruction section (packages 02 and 08).
- Adding templates and gate entries that a newer kit version ships and the repository does not have yet.

### Out of scope

- Updating the engine itself or the agent integrations (`integrations update`, package 07).
- Version pinning of the engine per repository (concept §9, possible later addition).
- Merging a `.new` template into the team's version. The human does that.

## Functional description

### Preconditions

- The repository is initialised (package 01 kit root check).
- `.shadekit/` has no uncommitted changes. Otherwise `upgrade` stops, because git is the backup. **Decided** (concept §9.3).
- No file has a format newer than the engine supports; otherwise the hard stop from package 01 applies.

### JSON files

- Every JSON file in `.shadekit/` (`config.json`, `gates.json`, `templates.lock.json`, every `feature.json`) is migrated from its own `schemaVersion` to the engine's current format through its chain. Files from different format versions after a merge are handled file by file. **Decided** (concept §9.1, §9 Consequences).
- Features in any phase are migrated in place. **Decided** (concept §9.3).
- Unknown fields are kept (package 01).
- Gitignored local files (`current-feature.json`) are migrated too, but are not part of the uncommitted-changes check. **Proposed.**

### Templates

For each template the running kit version ships (**Decided**, concept §9.5):

| State in the repository | Action |
|---|---|
| Unchanged since install (hash matches the lock) | Replaced by the new version without asking. |
| Changed by the team | Left as it is. The new version is written next to it as `<name>.new`, and the diff is shown. |
| Missing because the kit added it in a newer version | Added. |
| Deleted by the team although it is in the lock | Not recreated; reported. **Proposed.** |

`templates.lock.json` then records the new kit version and the hash of the new shipped content for every template. A template the team changed therefore keeps counting as changed on later upgrades. **Proposed.**

### Gates and configuration

- New gate entries that a kit version adds to the defaults are added to `gates.json`. Values the team changed are never overwritten. **Proposed.**
- `kitVersion` in `config.json` is set to the running kit version.

### Instruction section

The managed section in `CLAUDE.md`, `AGENTS.md`, and any further instruction file from package 08 is replaced between its markers. Every line outside the markers is kept; a changed file is written as a whole with the line ending decided at write time (package 02, [Managed instruction section](02-repository-initialisation.md#managed-instruction-section)), so its other lines are normalised too. A file whose section is already identical is not written. A file whose markers were removed is reported, not changed. **Proposed.**

### All or nothing

Every changed file is first written to a temporary file; only when all are written are they renamed into place. If anything fails, no file in `.shadekit/` or in the instruction files has changed. **Decided** (for `.shadekit/`, concept §9.3), **Proposed** (extended to the instruction files). Every file is still written through `ITextFileWriter` with the line ending of its final path (package 02); the writer has no staged multi-file write yet, so this package adds one (**Open**).

### Output

A summary per file: migrated from version x to y, template replaced, `.new` written, nothing to do. `--dry-run` prints the same summary and writes nothing. **Proposed.**

## Acceptance criteria

- **FP18-AC1** `upgrade` with uncommitted changes in `.shadekit/` stops and writes nothing.
- **FP18-AC2** After a merge that left `feature.json` files in two format versions, `upgrade` migrates each file from its own version.
- **FP18-AC3** A feature in `implement` keeps its phase, phase states, and history through an upgrade.
- **FP18-AC4** An unchanged template is replaced; a changed template stays and gets a `<name>.new` next to it; the diff is shown.
- **FP18-AC5** If one file fails to migrate, no file is changed.
- **FP18-AC6** `--dry-run` writes nothing and reports the same actions as a real run.
- **FP18-AC7** The managed section is replaced, and every line outside the markers is unchanged (line endings follow package 02).
- **FP18-AC8** Every migration step that exists has a test that migrates a fixture from the previous version (concept §9 consequence).

## Open points

- A staged write of several files (temporary file, then rename) through `ITextFileWriter` / `IKitStore`; the line ending is decided for the final path, as `IKitStore.CreateDirectoryWriter` does for `feature new` (package 03). **Open.**
- A `.shadekit/templates/request.md` written by `init` before 2026-10-25, and its entry in `templates.lock.json`, are orphaned: the kit no longer ships a `request.md` template, `init` no longer writes it, and `feature new` neither reads nor changes it (packages 02 and 03). `upgrade` may remove it, for example only when it is unchanged against the lock hash, like a shipped template it replaces. **Open.**
