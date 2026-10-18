# 02 · Repository initialisation

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (template conventions) |
| Source | Concept §3.1 (`init`), §4.1, [§9](../../research/shade-kit-concept.md#9-version-drift) (§9.5) |
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
- Installing skills (packages 07, 08). Skills are never written into the repository (concept §9.6, **Decided**).

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

The team owns the values; the engine owns the schema (concept §9.1). **Decided.** v1 content, **Decided**:

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

- Shipped templates are copied into `.shadekit/templates/` as plain files. Agents other than Claude Code need them as files in the repository (concept §9.5). **Decided.**
- No engine code parses the structure of a template or of an artifact created from it (concept §9.5). **Decided.** Every check against artifact content is done by an agent.
- Initial set, owned by the listed packages. Until its owner package is built, a template is a skeleton with headings only; templates change freely until the first release, so this causes no lock-file churn for users.
- In the kit's source, every template is a file `templates/<name>.template.md`, so it cannot be mistaken for an artifact. The source folder has the same subfolders as `.shadekit/templates/` (the checklists are in `templates/checks/`). `init` writes each template to its path in the repository, given in the table below. The engine keeps this mapping as an explicit list; it does not derive paths from file names. **Proposed.** Paths in `templates.lock.json` are repository paths.

| Template in the repository (`.shadekit/templates/`) | Source in the kit (`templates/`) | Owner package |
|---|---|---|
| `request.md` | `request.template.md` | 09 |
| `discussion.md` | `discussion.template.md` | 09 |
| `spec.md` | `spec.template.md` | 10 |
| `researches.md` | `researches.template.md` | 06 (shared format), sections filled in 10 and 11 |
| `plan.md`, `tasks.md` | `plan.template.md`, `tasks.template.md` | 12 |
| `review.md` | `review.template.md` | 13 |
| `verification.md`, `playbook.md` | `verification.template.md`, `playbook.template.md` | 14 |
| `report.md` | `report.template.md` | 15 |
| `diagnosis.md` | `diagnosis.template.md` | 16 |
| `checks/definition-of-ready.md`, `checks/definition-of-done.md` | `checks/definition-of-ready.template.md`, `checks/definition-of-done.template.md` | 05 |

### Template conventions

Every template follows the same conventions. **Decided** in the template interview on 2026-10-17.

- **Guidance for the agent** is written in HTML comments (`<!-- … -->`), one per template and one per section where needed. Every agent reads the same file, so the rules work the same in Claude Code, Copilot, and Codex, and teams that edit a template see the rules next to the structure.
- **Examples** appear only inside a guidance comment, at most one per section, so they cannot be left behind as content.
- **Placeholders** are written `{{name}}`; a choice of values is written `{{a | b}}`, with the pipe escaped as `\|` inside table cells. Leftover placeholders can be found by a plain text search.
- **Header:** a title, the feature id, and relative links to the related artifacts. No status or phase: `feature.json` is the only source of the feature state.
- **IDs** are stable and never renumbered: `REQ`, `AC`, `A`, `Q` (package 10), `D`, `T` (package 12), `F` (package 13), `DOR`, `DOD` (package 05).
- When an artifact is filled in, every placeholder is replaced and every guidance comment is removed. Checklist copies (package 05) are the exception: they keep their guidance comment, because they are rechecked on every run.

### `templates.lock.json`

- One entry per shipped template: path, kit version, and content hash at install time. **Decided** (concept §9.5).
- `init` writes it; package 18 uses it to tell changed from unchanged templates.

### Managed instruction section

- A short section in `CLAUDE.md` and `AGENTS.md`, delimited by begin and end markers so the engine can find and replace exactly that section later. **Proposed.**
- Content: the repository uses SHADE kit, the kit data lives in `.shadekit/`, how to start (`shade-kit-next-phase` from package 06), and that the engine is called as `shade-kit … --json`.
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
- **FP02-AC8** `init` writes every shipped template to its repository path from the template table, for example `spec.template.md` to `.shadekit/templates/spec.md` and `checks/definition-of-ready.template.md` to `.shadekit/templates/checks/definition-of-ready.md`.

## Open points

- None beyond the unverified Copilot instruction file (resolved in package 08).
