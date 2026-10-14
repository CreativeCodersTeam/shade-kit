# 02 · Repository initialisation

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §3.1 (`init`), §4.1, §9; [ADR 0001](../adr/0001-shadekit-version-drift.md) decision 5 |
| Depends on | 01 |
| Enables | 03, 05, 18 and every phase package (they ship templates through this mechanism) |

## Benefit

A team can turn any git repository into a SHADE kit repository with one command. Afterwards the repository holds everything the team owns and may adapt (configuration, templates), and agents that open the repository learn from a short instruction section that the kit is in use.

## Scope

### In scope

- `shade-kit init`.
- `config.json` with its v1 content.
- The set of shipped templates and the way they are copied into the repository.
- `templates.lock.json`.
- `.gitignore` entries for local-only files.
- A managed SHADE kit section in `CLAUDE.md` and `AGENTS.md`.

### Out of scope

- `gates.json` content and the checklist templates (package 05; `init` writes them through the same mechanism).
- The final wording of each artifact template. Each phase package owns the content of its templates; this package ships the mechanism and the initial set.
- Updating templates or the instruction section in an existing repository (package 18).
- Installing skills (packages 07, 08). Skills are never written into the repository (ADR 0001, **Decided**).

## Functional description

### `shade-kit init`

- Runs in a git repository. Outside a git repository it stops with an exit code. **Proposed**, because feature resolution (package 03) depends on git branches.
- Creates `.shadekit/` with:
  - `config.json`
  - `gates.json` (defaults from package 05; until package 05 is built, only `schemaVersion`)
  - `templates/` with every shipped template
  - `templates.lock.json`
  - `features/` (empty)
- Adds `.gitignore` entries for `.shadekit/local.json` and `.shadekit/features/current-feature.json`. **Decided** in the concept tree. `local.json` itself is not created in v1, because nothing in v1 reads it (separate spec store is later).
- Adds or updates the managed SHADE kit section in `CLAUDE.md` and `AGENTS.md` (see below).
- In an already initialised repository, `init` changes nothing and reports that the repository is initialised and that `shade-kit upgrade` is the way to update. **Proposed.**
- Prints a summary of what it created. Supports `--json`.

### `config.json` (v1)

The team owns the values; the engine owns the schema (ADR 0001). **Decided.** v1 content, **Decided**:

| Field | Purpose |
|---|---|
| `schemaVersion` | Format version (package 01). |
| `minEngineVersion` | First engine version that supports this format version, stamped by the engine on every write (package 01). |
| `kitVersion` | Kit version that initialised or last upgraded the repository. Informational only; format checks use `schemaVersion`. |
| `defaultProfile` | Profile used by `feature new` when none is given. Default `feature`. |
| `outdatedFormatOnRead` | What read commands do with a file in an older format (package 01): `migrate-and-warn` (default) or `stop`. |
| `trackers` | Mapping from issue URL host to id prefix for feature ids. Default: `github.com` → `gh` (package 03). |
| `coverageThreshold` | Optional. Minimum coverage that `verify` requires (package 14). Not written by `init`; absent means "measure and record only". |

`config.json` as written by `init` (version strings depend on the release):

```json
{
  "schemaVersion": 1,
  "minEngineVersion": "<first engine version with format 1>",
  "kitVersion": "<installing kit version>",
  "defaultProfile": "feature",
  "outdatedFormatOnRead": "migrate-and-warn",
  "trackers": { "github.com": "gh" }
}
```

### Templates

- Shipped templates are copied into `.shadekit/templates/` as plain files. Agents other than Claude Code need them as files in the repository (ADR 0001). **Decided.**
- No engine code parses the structure of a template or of an artifact created from it (ADR 0001). **Decided.** Every check against artifact content is done by an agent.
- Initial set, owned by the listed packages. Until its owner package is built, a template is a skeleton with headings only; templates change freely until the first release, so this causes no lock-file churn for users.

| Template | Owner package |
|---|---|
| `request.md` | 09 |
| `discussion.md` | 09 |
| `spec.md` | 10 |
| `researches.md` | 06 (shared format), sections filled in 10 and 11 |
| `plan.md`, `tasks.md` | 12 |
| `review.md` | 13 |
| `verification.md`, `playbook.md` | 14 |
| `report.md` | 15 |
| `diagnosis.md` | 16 |
| `checks/dor.md`, `checks/dod.md` | 05 |

### `templates.lock.json`

- One entry per shipped template: path, kit version, and content hash at install time. **Decided** (ADR 0001).
- `init` writes it; package 18 uses it to tell changed from unchanged templates.

### Managed instruction section

- A short section in `CLAUDE.md` and `AGENTS.md`, delimited by begin and end markers so the engine can find and replace exactly that section later. **Proposed.**
- Content: the repository uses SHADE kit, the kit data lives in `.shadekit/`, how to start (the router skill from package 06), and that the engine is called as `shade-kit … --json`.
- If a file does not exist, `init` creates it with only the section. If it exists, `init` appends the section and leaves the rest unchanged.
- **Unverified:** whether GitHub Copilot reads `AGENTS.md` or needs `.github/copilot-instructions.md`. Package 08 resolves this and may add a third target file.

## Acceptance criteria

- **FP02-AC1** `init` in a git repository without `.shadekit/` creates `config.json`, `gates.json`, `templates/` with every shipped template, `templates.lock.json`, and `features/`.
- **FP02-AC2** Every JSON file `init` writes carries `schemaVersion`.
- **FP02-AC3** `templates.lock.json` contains one entry per shipped template whose hash matches the written file.
- **FP02-AC4** `.gitignore` contains the entries for `local.json` and `current-feature.json` exactly once, also when `init` runs in a repository whose `.gitignore` already has them.
- **FP02-AC5** `CLAUDE.md` and `AGENTS.md` contain exactly one managed section; existing content outside the section is unchanged.
- **FP02-AC6** `init` in an initialised repository changes no file and points to `shade-kit upgrade`.
- **FP02-AC7** `init` outside a git repository stops with an exit code and creates nothing.

## Open points

- None beyond the unverified Copilot instruction file (resolved in package 08).
