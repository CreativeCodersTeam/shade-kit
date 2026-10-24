# 02 · Repository initialisation

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-17 (template conventions), 2026-10-18 (implementation state; code review rework: exit codes 14 and 24, `.gitignore` rule, separator line), 2026-10-22 (code review verification of package 03: `trackers` id prefixes validated), and 2026-10-23 (rework of file content handling: line endings of `.gitignore`, `CLAUDE.md`, and `AGENTS.md`, strict UTF-8, warnings; code review: `editorconfig-invalid`, legacy `crlf` attribute, `text=input`). Implemented except the final content of the templates owned by later packages (skeletons until then), the `gates.json` defaults (package 05), and a possible Copilot instruction file (package 08). |
| Source | Concept §3.1 (`init`), §4.1, [§9](../../research/shade-kit-concept.md#9-version-drift) (§9.5) |
| Depends on | 01 |
| Enables | 03, 05, 18 and every phase package (they ship templates through this mechanism) |

Labels follow the concept: **Decided**, **Proposed**, **Open**, **Unverified**, **Implemented**.

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

**Implemented** (`InitCommand`, `IRepositoryInitializer` / `RepositoryInitializer`).

- Runs in a git repository. The repository root is the closest directory, from the working directory upwards, that contains a `.git` directory or file; `init` always works at that root, also when it is called from a subdirectory. Outside a git repository it stops with exit code 12 (`not-a-git-repository`) and creates nothing. **Decided** on 2026-10-18 (implementation clarification), because feature resolution (package 03) depends on git branches. **Implemented** (`IGitRepositoryLocator` / `GitRepositoryLocator`).
- Creates `.shadekit/` with:
  - `config.json`
  - `gates.json` (defaults from package 05; until package 05 is built, only `schemaVersion` and `minEngineVersion`). **Implemented** (`GatesConfig`, no fields of its own yet).
  - `templates/` with every shipped template
  - `templates.lock.json`
  - `features/` (empty). Git does not keep an empty directory, so a clone of a freshly initialised repository has no `features/`; a missing `features/` counts as "no features" (package 01).
- Adds `.gitignore` entries for `.shadekit/local.json` and `.shadekit/features/current-feature.json`. **Decided** in the concept tree. `local.json` itself is not created in v1, because nothing in v1 reads it (separate spec store is later). Rules in [`.gitignore`](#gitignore). **Implemented** (`GitIgnorePlanner`).
- Adds or updates the managed SHADE kit section in `CLAUDE.md` and `AGENTS.md` (see [below](#managed-instruction-section)). **Implemented** (`InstructionSectionPlanner`).
- In an already initialised repository, `init` changes nothing, stops with exit code 13 (`already-initialized`), and points to `shade-kit upgrade` as the way to update. The repository counts as initialised when a `.shadekit` directory (also an empty one) exists at its root. **Decided** on 2026-10-18 (implementation clarification). **Implemented** (`KitAlreadyInitializedException`).
- A `.shadekit` file at the repository root is neither initialised nor free: `init` and every command that opens the store (for example `feature list`) stop with exit code 15 (`kit-root-not-a-directory`) and ask to move or remove the file and run `init`. **Decided** on 2026-10-19 (code review rework); before, `init` treated the file as initialised and `feature list` as missing, so each command pointed to the other. **Implemented** (`KitRootNotADirectoryException`, `RepositoryInitializer`, `KitStoreFactory.Create`).
- Prints a summary of what it created or changed. Supports `--json`. Output contract in [Output](#output). **Implemented** (`InitCommandData`, `InitializedFile`).

### Order of work and failures

`init` works in this order. **Decided** on 2026-10-18 (implementation clarification). **Implemented** (`RepositoryInitializer`).

1. Preconditions, before anything is written:
   1. the git repository root (exit code 12);
   2. no `.shadekit` file (exit code 15) and no existing `.shadekit` directory (exit code 13);
   3. `.gitignore`, then `CLAUDE.md`, then `AGENTS.md` at the repository root, each checked in this order: the path is not a directory (exit code 14), the file neither starts with a UTF-16 or UTF-32 byte order mark nor contains a NUL byte nor bytes that are not valid UTF-8 (exit code 24), and, for `CLAUDE.md` and `AGENTS.md`, the section markers are valid (exit code 23). A missing file passes. **Decided** on 2026-10-18 (code review rework) for the directory and encoding checks; the NUL byte check on 2026-10-19; the strict UTF-8 check on 2026-10-23. **Implemented** (`IGitIgnorePlanner.PlanAsync`, `IInstructionSectionPlanner.PlanAsync`, `TextFileContent`): planning computes the new content of all three files before anything is written, so the checks and the writes use the same code.
2. `.shadekit/`: `config.json`, `gates.json`, the templates in the order of the template table, `templates.lock.json`, `features/`.
3. `.gitignore`.
4. `CLAUDE.md`, then `AGENTS.md`.

The first failing precondition stops `init`; nothing is written. A cancellation is checked again right before the first write, and `.shadekit/` is created only with its first file (`config.json`), so a cancelled `init` leaves no `.shadekit/` behind that would block the next run. **Implemented** (`RepositoryInitializer`, `InitializeAsync_TokenCanceledDuringPreconditions_ThrowsOperationCanceledExceptionAndWritesNothing`). Since the code review rework of package 03 on 2026-10-21, the first Ctrl+C cancels this token, and a canceled `init` ends with exit code 130 (`canceled`, **Decided** on 2026-10-21; package 03, [Cancellation](03-feature-management.md#cancellation)).

If a write fails after the preconditions, for example because of an IO error, or `init` is cancelled while it writes, `init` removes the `.shadekit/` it created in this run and fails (a host error, exit code 255, for an IO error). `.shadekit/` did not exist before, so nothing is lost. `.gitignore`, `CLAUDE.md`, or `AGENTS.md` written before the failure stay in place; a second `init` recognises their changes (entries present, section identical) and does not repeat them, so running `init` again after fixing the cause completes the initialisation. Removing `.shadekit/` is best effort: if it fails, `.shadekit/` stays and a second `init` stops with exit code 13. **Decided** on 2026-10-19 by the user (code review rework), replacing "no rollback" from 2026-10-18, which left a `.shadekit/` behind that blocked every later `init` and pointed to the not yet existing `shade-kit upgrade`. **Implemented** (`RepositoryInitializer`, `InitializeAsync_LastWriteFails_RemovesKitRootAndKeepsRepositoryFilesWrittenBefore`, `InitializeAsync_AfterLastWriteFailed_SecondRunCompletesOnlyTheMissingChanges`, `InitializeAsync_WriteInKitRootFails_RemovesKitRootAndWritesNoRepositoryFile`, `InitializeAsync_CanceledWhileWritingKitRoot_RemovesKitRoot`). Known failures that the preconditions do not catch are listed in the [open points](#open-points).

### Output

**Decided** on 2026-10-18 (implementation clarification). **Implemented** (`InitCommand`, `InitCommandData`).

- `--json`: the `data` of the envelope (package 01) lists the repository root and every file or directory that was created or changed, in write order:

  ```json
  {
    "repositoryRoot": "/home/dev/src/shop",
    "files": [
      { "path": ".shadekit/config.json", "action": "created" },
      { "path": ".shadekit/gates.json", "action": "created" },
      { "path": ".shadekit/templates/request.md", "action": "created" },
      { "path": ".shadekit/templates.lock.json", "action": "created" },
      { "path": ".shadekit/features/", "action": "created" },
      { "path": ".gitignore", "action": "appended" },
      { "path": "CLAUDE.md", "action": "updated" },
      { "path": "AGENTS.md", "action": "created" }
    ]
  }
  ```

  The example shortens the template list to one entry. `repositoryRoot` is an absolute path. `path` is relative to the repository root with `/` as separator; a directory ends with `/`. `action` is `created`, `appended`, or `updated`. A file that already had the required content is not listed.
- Plain: `Initialised SHADE kit in <root>.`, one line `<action>  <path>` per file, and a hint to commit `.shadekit/` and the changed files together with the code.
- Warnings: a warning that occurs while a file of `.shadekit/`, `.gitignore`, `CLAUDE.md`, or `AGENTS.md` is written, for now `editorconfig-unreadable` (an `.editorconfig` file cannot be read) and `editorconfig-invalid` (an `.editorconfig` file is read but cannot be evaluated because of its content, for example a section header with a numeric range such as `[{1..99999999999}]`), in both cases with the line ending of the operating system, is reported once per `init`, even if several files hit it: in JSON mode in the `warnings` of the envelope (not in `data`), in plain mode as one line `Warning: <message>` before the summary. **Decided** on 2026-10-23 (rework of file content handling). **Implemented** (`RepositoryInitResult.Warnings`, `InitCommand`, `InitializeAsync_WriterReturnsWarnings_ReturnsEachDistinctWarningOnceInFirstOccurrenceOrder`, `InitializeAsync_KitStoreAndRepositoryWritesReturnWarnings_ReturnsStoreWarningsFirstEachOnce`). The result carries only the warnings of its own writes: `IKitStore.WriteFileAsync` / `WriteJsonAsync` return the warnings of each write, and the store keeps none. **Decided** on 2026-10-23 (code review of the rework, replacing the per-store collection `IKitStore.Warnings`). **Implemented** (`InitializeAsync_KitStoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierWrite`). `editorconfig-invalid` **Decided** on 2026-10-23 (code review of the rework). **Implemented** (`EditorConfigInvalidWarning`, `GetLineEnding_EditorConfigWithMalformedNumericRange_ReturnsNoLineEndingAndInvalidWarning`, `MapWarning_EditorConfigInvalid_ReturnsInvalidWarningNamingFileReasonAndFallback`). `git-attributes-unavailable`: `git` is installed but fails to read the attributes or the configuration of the repository (for example `safe.directory`, or a broken `.git/config`), so git does not decide; reported like the other warnings. A missing `git` gives no warning. **Decided** on 2026-10-24 (code review of the branch, finding 4). **Implemented** (`GitAttributesUnavailableWarning`, `GetLineEndingAsync_CheckAttrFails_ReturnsNullAndWarningWithExitCodeAndStandardError`, `MapWarning_GitAttributesUnavailable_ReturnsWarningNamingRepositoryCommandReasonAndFallback`).

### Errors

Exit codes owned by this package. **Implemented** (`ShadeKitCliExitCodes`, `ShadeKitCliErrorCodes`, `CliErrorMapper`).

| Exit code | Error code | When | Files changed |
|---|---|---|---|
| 12 | `not-a-git-repository` | The working directory is not inside a git repository. | None |
| 13 | `already-initialized` | `.shadekit` already exists at the repository root. The message points to `shade-kit upgrade`. | None |
| 14 | `path-not-a-file` | `.gitignore`, `CLAUDE.md`, or `AGENTS.md` at the repository root is a directory (`PathNotAFileException`). The message names the path. **Decided** on 2026-10-18 (code review rework). | None |
| 15 | `kit-root-not-a-directory` | `.shadekit` at the repository root is a file (`KitRootNotADirectoryException`). Also returned by every command that opens the store. The message names the path and points to `shade-kit init`. **Decided** on 2026-10-19 (code review rework). | None |
| 23 | `invalid-managed-section` | The section markers in `CLAUDE.md` or `AGENTS.md` are invalid (see [below](#managed-instruction-section)). The message names the file and the problem. | None |
| 24 | `unsupported-encoding` | `.gitignore`, `CLAUDE.md`, or `AGENTS.md` starts with a UTF-16 LE, UTF-16 BE, UTF-32 LE, or UTF-32 BE byte order mark, or contains a NUL byte, which UTF-16 and UTF-32 without byte order mark have for every ASCII character, or contains bytes that are not valid UTF-8 (`UnsupportedFileEncodingException`); UTF-8 with or without byte order mark is accepted. The message names the file and its encoding, `UTF-16 or UTF-32 without byte order mark` for a file with a NUL byte, `an unknown encoding with bytes that are not valid UTF-8` for any other file that is not valid UTF-8. **Decided** on 2026-10-18 (code review rework), the NUL byte check on 2026-10-19, the strict UTF-8 check on 2026-10-23. | None |

Any other failure, for example a file that cannot be read or written, is not mapped and ends with exit code 255 (`host-error`).

### `config.json` (v1)

The team owns the values; the engine owns the schema. **Decided** (concept §9.1). v1 content: **Proposed**, except `schemaVersion`, `minEngineVersion`, and `outdatedFormatOnRead` (**Decided**, concept §9.1–9.2) and `report` (**Decided** on 2026-10-18). **Implemented** (`KitConfig`, `ReportPolicy`): every field below is a typed property; fields the engine does not know are kept (package 01).

| Field | Purpose |
|---|---|
| `schemaVersion` | Format version (package 01). |
| `minEngineVersion` | First engine version that supports this format version, stamped by the engine on every write (package 01). |
| `kitVersion` | Kit version that initialised or last upgraded the repository. Informational only; format checks use `schemaVersion`. `init` writes the engine version. |
| `defaultProfile` | Profile used by `feature new` when none is given. Default `feature`. `feature new` stops with exit code 40 (`unknown-profile`) if it is not `feature`, `bugfix`, or `quick` (package 03). **Implemented** (`KitConfig.DefaultProfile`, `FeatureStore`). |
| `outdatedFormatOnRead` | What read commands do with a file in an older format (package 01): `migrate-and-warn` (default) or `stop`. |
| `trackers` | Mapping from issue URL host to id prefix for feature ids. Default: `github.com` → `gh`, which also applies when the field is missing; `{}` turns issue-based ids off, `null` makes the file invalid. **Decided** on 2026-10-19 (code review rework). Every id prefix must consist of lowercase letters a-z and digits joined by single hyphens (`^[a-z0-9]+(-[a-z0-9]+)*$`, the pattern of a feature id), for example `gh` or `jira-1`; otherwise the file is invalid (exit code 22, `invalid-file`), and the message names the host and the prefix. **Decided** on 2026-10-22 (code review verification of package 03, finding 2); before, a prefix such as `GH` or `""` only failed in `feature new` with a misleading `invalid-feature-id`. **Implemented** (`KitConfig.Trackers`); `feature new` uses it for issue-based ids (package 03, **Implemented**). |
| `coverageThreshold` | Optional. Minimum coverage that `verify` requires (package 14). Not written by `init`; absent means "measure and record only". |
| `report` | Whether `finish` writes `report.md` (package 15): `ask` (default, `finish` asks the human), `always`, or `never`. Absent means `ask`. **Decided** on 2026-10-18 (findings clarification). |

`config.json` as written by `init` (version strings depend on the release):

```json
{
  "schemaVersion": 1,
  "minEngineVersion": "<first engine version with format 1>",
  "kitVersion": "<installing kit version>",
  "defaultProfile": "feature",
  "outdatedFormatOnRead": "migrate-and-warn",
  "trackers": { "github.com": "gh" },
  "report": "ask"
}
```

### Templates

- Shipped templates are copied into `.shadekit/templates/` as plain files. Agents other than Claude Code need them as files in the repository (concept §9.5). **Decided.** **Implemented** (`RepositoryInitializer`).
- No engine code parses the structure of a template or of an artifact created from it (concept §9.5). **Decided.** Every check against artifact content is done by an agent.
- Initial set, owned by the listed packages. Until its owner package is built, a template is a skeleton with headings only; templates change freely until the first release, so this causes no lock-file churn for users. **Implemented:** `spec`, `plan`, `tasks`, and the two checklists come from the template interview (2026-10-17); `request`, `discussion`, `researches`, `review`, `verification`, `playbook`, `report`, and `diagnosis` are skeletons whose guidance comment names the owner package.
- In the kit's source, every template is a file `<name>.template.md` in `src/CreativeCoders.ShadeKit.Core/Templates/Resources/`, so it cannot be mistaken for an artifact. The source folder has the same subfolders as `.shadekit/templates/` (the checklists are in `checks/`). The files are embedded into the Core assembly as resources named `templates/<subfolder>/<name>.template.md` (for example `templates/spec.template.md`, `templates/checks/definition-of-ready.template.md`), with `/` on every OS, so the `dotnet` tool and the single-file binaries carry them without extra files. **Decided** on 2026-10-18 (implementation clarification). **Implemented** (`EmbeddedResource` in `CreativeCoders.ShadeKit.Core.csproj`, `IShippedTemplateReader` / `ShippedTemplateReader`).
- `init` writes each template to its path in the repository, given in the table below. The engine keeps this mapping as an explicit list; it does not derive paths from file names. **Decided** on 2026-10-18 (implementation clarification). **Implemented** (`ShippedTemplate`, `ShippedTemplates.All`). Paths in `templates.lock.json` are repository paths.
- Every template is written as UTF-8 without byte order mark. The reader splits the source into lines at every line ending (CRLF, a lone CR, and the other line endings `string.ReplaceLineEndings` recognises), whatever line endings the checkout of the kit's source used. **Decided** on 2026-10-18 (implementation clarification). **Implemented** (`ShippedTemplateReader`, `RepositoryInitializer`).
- The templates and the JSON files of `.shadekit/` (`config.json`, `gates.json`, `templates.lock.json`) are written with the line ending that is decided for each file when it is written, by the same rules as for [`.gitignore`](#gitignore) (git attributes and configuration for the path `.shadekit/...`, then `.editorconfig`, then the operating system); the JSON files end with a line ending. **Decided** on 2026-10-23 (rework of file content handling), replacing "templates are always written with LF line endings" from 2026-10-18. **Implemented** (`IKitStore.WriteFileAsync` / `WriteJsonAsync` through `ITextFileWriter`, `RepositoryInitializer`, `InitializeAsync_KitStoreLineEnding_WritesTemplatesAndJsonFilesWithIt`).

| Template in the repository (`.shadekit/templates/`) | Source in the kit (`src/CreativeCoders.ShadeKit.Core/Templates/Resources/`) | Owner package |
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

- One entry per shipped template: path, kit version, and content hash at install time. **Decided** (concept §9.5). **Implemented** (`TemplatesLock`, `TemplatesLockEntry`).
- `init` writes it; package 18 uses it to tell changed from unchanged templates.
- Shape: **Decided** on 2026-10-18 (implementation clarification).

  ```json
  {
    "schemaVersion": 1,
    "minEngineVersion": "<first engine version with format 1>",
    "templates": [
      {
        "path": ".shadekit/templates/checks/definition-of-done.md",
        "kitVersion": "<installing kit version>",
        "hash": "sha256:<64 lowercase hex digits>"
      }
    ]
  }
  ```

  - `path` is the repository path with `/` as separator. Entries are sorted ordinally by `path`.
  - `kitVersion` is the engine version that wrote the template.
  - `hash` is `sha256:` followed by the SHA-256, as 64 lowercase hex digits, of the template's lines joined with LF (UTF-8, with a final LF if the file ends with a line ending, and with the UTF-8 byte order mark if the file has one). The hash is independent of the line ending the template is written with or that Git checks it out with (for example CRLF on Windows with `core.autocrlf`), so an unchanged template matches its entry. **Decided** on 2026-10-23 (rework of file content handling), replacing "the exact bytes written, with CRLF counted as LF" from 2026-10-18; the result is the same, so existing lock entries stay valid. **Implemented** (`TemplatesLockEntry.ComputeHash(TextFileContent)`, `ComputeHash_ShippedTemplates_EqualsHashOfWrittenBytesWithCrLfCountedAsLf`, `InitializeAsync_KitStoreLineEndingCrLf_LockHashesEqualThoseOfLf`).
  - Unknown fields are kept at the top level and in every entry (package 01).

### `.gitignore`

**Decided** on 2026-10-18 (implementation clarification; presence rule and separator line changed in the code review rework the same day). **Implemented** (`IGitIgnorePlanner` / `GitIgnorePlanner`; the entries are `KitGitIgnoreEntries.All`).

- The file is `.gitignore` at the repository root. If it does not exist, `init` creates it with the comment line and both entries.
- An entry counts as present when a line, with its trailing spaces removed, equals the entry or the entry with a leading `/`. This follows git, which ignores trailing spaces of a pattern but treats leading whitespace and trailing tabs as part of it: a line with leading whitespace or a trailing tab does not count, and neither does a line ending in an escaped space (`\ `), because the escaped space stays part of the pattern. Patterns that only match the files indirectly, such as `*.json`, do not count either.
- Missing entries are appended without a leading `/`, under the comment line `# SHADE kit: local-only files`. The comment is written only when something is appended, after one blank line if the file has content and does not already end with an empty line. If nothing is missing, the file is not changed.
- In an existing file every existing line is kept: a missing final line break is added before appending, and a leading UTF-8 byte order mark is kept. A new file is UTF-8 without byte order mark. A file that starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains bytes that are not valid UTF-8 is not changed (exit code 24).
- Line endings: a file that `init` creates or changes is written as a whole with one line ending, so the existing lines of a changed file are normalised too. The line ending is decided when the file is written, first match wins: the git checkout configuration for the file, with the attributes evaluated as git does (`-text`, also through the `binary` macro or the legacy `-crlf`, means no decision; otherwise the `eol` attribute, then `text=input` or the legacy `crlf=input` for LF, then `core.autocrlf` (`true`, `yes`, `on`, or an integer other than `0` for CRLF, `input` for LF), then `core.eol` for text files; the legacy `crlf` counts only while `text` is unspecified, and a `text` after `binary` makes the file text again), then `end_of_line` (`lf` or `crlf`) of the `.editorconfig` files, then the operating system (CRLF on Windows, LF elsewhere). If an `.editorconfig` file cannot be read or evaluated, the `.editorconfig` files do not decide and the warning `editorconfig-unreadable` or `editorconfig-invalid` is reported (see [Output](#output)). `core.autocrlf` and `core.eol` are read at most once per repository and command run; `init` reads the attributes of all written files with one `git check-attr` per 100 files. A `git` that fails or is missing is not started again for that repository in the same command run; a failing one reports `git-attributes-unavailable` (see [Output](#output)). An `.editorconfig` file with a section glob whose braces expand into more than 10 000 patterns (for example `[{1..20000}{1..20000}]`) is reported as `editorconfig-invalid` before the parser expands it, so it cannot exhaust the memory. **Decided** on 2026-10-24 (code review of the branch, findings 1 and 4), replacing "`git check-attr` runs once per written file". **Implemented** (`GitLineEndingResolver`, `EditorConfigGlobExpansion`, `GetLineEndingAsync_GitFailedForRepository_ReturnsSameWarningForLaterFileWithoutRunningGit`, `GetLineEnding_SectionGlobTooLarge_ReturnsWarningNamingGlobWithoutExpandingIt`). A file that needs no change is not written, whatever its line endings. **Decided** on 2026-10-23 (rework of file content handling), replacing "existing bytes stay unchanged, appended lines use CRLF if the file contains CRLF" from 2026-10-18. **Implemented** (`TextFileContent`, `ITextFileWriter` / `TextFileWriter`, `ILineEndingResolver` / `LineEndingResolver`, `PlanAsync_FileWithLineEndings_WholeFileIsWrittenWithTargetLineEnding`). Legacy `crlf` attribute, `text=input`, integer `core.autocrlf` values, `editorconfig-invalid`, and the per-run configuration cache **Decided** on 2026-10-23 (code review of the rework). **Implemented** (`GitLineEndingResolver`, `GetLineEndingAsync_CrLfInputAndTextUnspecified_ReturnsLfWithoutReadingConfiguration`, `GetLineEndingAsync_BinaryMacroOverriddenByText_TreatsFileAsText`, `GetLineEndingAsync_SeveralFilesOfOneRepository_ReadsEachConfigurationValueOnce`).

### Managed instruction section

- A short section in `CLAUDE.md` and `AGENTS.md` at the repository root, delimited by begin and end markers so the engine can find and replace exactly that section later. **Decided** on 2026-10-18 (implementation clarification). **Implemented** (`IInstructionSectionPlanner` / `InstructionSectionPlanner`; the text and the markers are `ManagedInstructionSection.Text`, `.BeginMarkerPrefix`, and `.EndMarker`).
- Content: the repository uses SHADE kit, the kit data lives in `.shadekit/`, how to start (`shade-kit-next-phase` from package 06), and that the engine is called as `shade-kit … --json`. The text `init` writes:

  ```markdown
  <!-- shade-kit:begin - managed by shade-kit, content between the markers is replaced on upgrade -->
  ## SHADE kit

  This repository uses SHADE kit. Its data lives in `.shadekit/` (configuration, templates, features) and is committed with the code.

  - To start or continue work on a feature, use the `shade-kit-next-phase` skill.
  - Call the engine as `shade-kit <command> --json` and act on its JSON result.
  - Change feature state only through `shade-kit` commands, never by editing `.shadekit/features/` by hand.
  <!-- shade-kit:end -->
  ```

- Markers: the begin marker is a line whose text, without leading and trailing whitespace, starts with `<!-- shade-kit:begin` followed by whitespace or `-->`, so `<!-- shade-kit:beginning` is no begin marker; the end marker is a line whose trimmed text is `<!-- shade-kit:end -->`. Lines in a fenced code block are no markers, so a file can show the markers in a code example: a block starts with a line of at least three backticks or tildes, indented by at most three spaces (a backtick fence must not have a backtick in its info string), and ends with a line of only the same character, at least as many times, or at the end of the file (CommonMark). A block that is still open at the end of the file makes `init` stop with exit code 23, because a section appended after it would be part of the block and would be appended again by every later `init`. The markers are invalid when a file has more than one begin or end marker, a begin marker without an end marker, an end marker without a begin marker, or the end marker before the begin marker. **Decided** on 2026-10-19 (code review rework) for the code blocks, the unclosed block, and the word end of the begin marker. **Implemented** (`InstructionSectionPlanner`, `PlanAsync_MarkerLinesInCodeFenceOrNotMarkers_AppendsSectionAndKeepsThem`, `PlanAsync_SectionOutsideCodeFences_ReplacesOnlyThatSection`).
- Behaviour per file:

  | File | `init` | Reported action |
  |---|---|---|
  | Does not exist | Creates it with only the section. | `created` |
  | Exists, no markers | Appends one blank line and the section; no blank line is added if the file already ends with an empty line. The rest stays unchanged. | `appended` |
  | Exists, exactly one valid pair of markers | Replaces the lines from the begin marker to the end marker with the section; the rest stays unchanged. | `updated`; not listed if the section was already identical |
  | Exists, invalid markers | Stops with exit code 23 (`invalid-managed-section`). Both files are checked before anything is written, so no file is changed. | — |

- In an existing file every line outside the inserted or replaced part is kept: a missing final line break is added before appending, and a leading UTF-8 byte order mark is kept. A new file is UTF-8 without byte order mark. A file that starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains bytes that are not valid UTF-8 is not changed (exit code 24).
- Line endings follow the same rules as for [`.gitignore`](#gitignore): a changed file is written as a whole with the line ending decided at write time (git attributes and configuration, then `.editorconfig`, then the operating system), so its other lines are normalised too. The section counts as identical when its lines are identical, whatever line endings the file has: such a file is not written and not listed. **Decided** on 2026-10-23 (rework of file content handling), replacing "the section uses CRLF if the file contains CRLF" from 2026-10-18. **Implemented** (`InstructionSectionPlanner`, `TextFileWriter`, `PlanAsync_CurrentSectionWithOtherOrMixedLineEndings_ReturnsNullAndLeavesFileUnchanged`, `PlanAsync_OutdatedSectionInMixedFile_WritesWholeFileWithTargetLineEnding`).
- `AGENTS.md` as a symbolic link to `CLAUDE.md` works: the section is written once into `CLAUDE.md`, the link is kept, and `AGENTS.md` is not listed, because a link to a file that is already planned is skipped. **Implemented** (`InitializeAsync_AgentsMdIsSymlinkToClaudeMd_AppendsExactlyOneSectionAndKeepsSymlink` and `InitializeAsync_AgentsMdIsSymlinkToClaudeMdWithOutdatedSection_UpdatesSectionOnceAndKeepsSymlink`, skipped where symbolic links cannot be created).
- **Unverified:** whether GitHub Copilot reads `AGENTS.md` or needs `.github/copilot-instructions.md`. Package 08 resolves this and may add a third target file.

### Deviations from the draft

The implementation on 2026-10-18 changed these statements of the earlier draft. All were decided by the user in the implementation clarification on 2026-10-18.

| Draft | Now | Reason |
|---|---|---|
| "If it exists, `init` appends the section and leaves the rest unchanged." | A file that already has a valid section gets it replaced, not appended a second time. | Appending to a file that has the section would break FP02-AC5 ("exactly one managed section") and contradicted "Adds or updates" in the `init` list. |
| Template sources in `templates/` at the repository root (`templates/<name>.template.md`, `templates/checks/`). | Sources in `src/CreativeCoders.ShadeKit.Core/Templates/Resources/`, embedded into the Core assembly. | The engine reads the templates from its own assembly, so both distribution forms carry them. |
| `features/` is created empty (no statement about a missing one); package 01's code stopped with exit code 11 (`features-dir-not-found`) when it was missing. | A missing `features/` counts as empty. Exit code 11 and `features-dir-not-found` are removed; 11 is free. | Git does not keep the empty `features/` that `init` creates, so every fresh clone of an initialised repository would have failed. |
| `gates.json` with "only `schemaVersion`" until package 05. | `schemaVersion` and `minEngineVersion`. | Package 01 stamps `minEngineVersion` on every JSON file the engine writes (FP01-AC9). |

### Review

The implementation was reviewed on 2026-10-18 ([review report](../reviews/2026-10-18-feature-cli-init-repo-uncommitted.md)). Findings #1–#7 and #9–#11 are fixed; #8 (`AllSatisfy` instead of a loop in the FP02-AC2 test) was accepted as is by the user.

## Acceptance criteria

Every criterion is **Implemented**. The tests that prove it carry `[Trait("Requirement", "FP02-ACn")]`.

- **FP02-AC1** `init` in a git repository without `.shadekit/` creates `config.json`, `gates.json`, `templates/` with every shipped template, `templates.lock.json`, and `features/`. Proven by `RepositoryInitializerTests`, `InitCommandTests`, `InitCommandEndToEndTests`.
- **FP02-AC2** Every JSON file `init` writes carries `schemaVersion`. Proven by `RepositoryInitializerTests`.
- **FP02-AC3** `templates.lock.json` contains one entry per shipped template whose hash matches the written file. Proven by `RepositoryInitializerTests`.
- **FP02-AC4** `.gitignore` contains the entries for `local.json` and `current-feature.json` exactly once, also when `init` runs in a repository whose `.gitignore` already has them. Proven by `GitIgnorePlannerTests`, `RepositoryInitializerTests`, `InitCommandEndToEndTests`.
- **FP02-AC5** `CLAUDE.md` and `AGENTS.md` contain exactly one managed section; existing content outside the section is unchanged. Proven by `InstructionSectionPlannerTests`, `RepositoryInitializerTests`, `InitCommandEndToEndTests`.
- **FP02-AC6** `init` in an initialised repository changes no file and points to `shade-kit upgrade`. Proven by `RepositoryInitializerTests`, `InitCommandTests`, `InitCommandEndToEndTests`, `CliErrorMapperTests`.
- **FP02-AC7** `init` outside a git repository stops with an exit code and creates nothing. Proven by `RepositoryInitializerTests`, `InitCommandTests`, `InitCommandEndToEndTests`.
- **FP02-AC8** `init` writes every shipped template to its repository path from the template table, for example `spec.template.md` to `.shadekit/templates/spec.md` and `checks/definition-of-ready.template.md` to `.shadekit/templates/checks/definition-of-ready.md`. Proven by `RepositoryInitializerTests`, `InitCommandEndToEndTests`, and `ShippedTemplatesTests` (`All_Fp02Ac8Examples_MapToRepositoryPathFromTemplateTable`, without the trait).

## Open points

- The Copilot instruction file is **Unverified** (resolved in package 08).
- Follow-ups found during the implementation on 2026-10-18, all **Open**:
  - Plain output is wrapped at 80 columns by Spectre.Console when stdout is not a terminal, so long paths in the plain output of `init` and `feature list` are split across lines. JSON output is not affected.
  - The shared enum converter of `VersionedJsonSerializer` (package 01) accepts comma-separated values such as `"always, never"` and reads them as a combined value that is not a defined member, for example in `report` and `outdatedFormatOnRead`.
  - The skeleton templates name their owner package in their guidance comment, so that text is copied into every repository. Each owner package must replace its skeleton when it is built.
  - If `.gitignore` already contains the comment line `# SHADE kit: local-only files` but an entry is missing, `init` appends a second comment line before the missing entry.
- Follow-ups found in the code review rework on 2026-10-18, all **Open** (left open by the user on 2026-10-18):
  - A dangling symbolic link, for example `AGENTS.md` pointing to a missing `CLAUDE.md`, or a `.gitignore` pointing to a missing file, makes `init` fail during the preconditions with an unmapped `FileNotFoundException`: exit code 255 (`host-error`), nothing written. Checked on macOS with the built CLI on 2026-10-18 for both cases.
  - A read-only `.gitignore`, `CLAUDE.md`, or `AGENTS.md` passes the preconditions and fails only when it is written: exit code 255 (`host-error`). Since 2026-10-19, `init` then removes the `.shadekit/` it created, so a second `init` after making the file writable completes the initialisation (checked on macOS with the built CLI on 2026-10-19 with a read-only `444` `CLAUDE.md`). A file that cannot be read fails already in the preconditions with exit code 255 and nothing written. Checked on macOS with the built CLI on 2026-10-18 with an unreadable (`000`) `CLAUDE.md`.
  - The CLI messages for exit codes 14, 23, and 24 tell the user to run `shade-kit init` again. Package 18 reuses the planners (`IGitIgnorePlanner`, `IInstructionSectionPlanner`), so these messages need wording that does not name one command.
