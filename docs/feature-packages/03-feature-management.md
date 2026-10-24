# 03 · Feature management

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (`feature drop` confirmation, `status` in `feature current`, `feature list` implemented), 2026-10-21 (implementation state; implementation clarification of 2026-10-20; code review rework: `git` only from absolute `PATH` entries, exit codes 2 and 130, rules for file sources and titles, atomic `new` and `drop`, folder name as id, unusable override, `overrideAffected`, Ctrl+C, warning codes; second code review: exit code 35 `feature-io-failed` for a feature folder that cannot be created or renamed, execute bit for `git` on Unix), 2026-10-22 (code review verification: exact folder name match, tracker prefix validation, heading detection, override check through the format layer, shared display path, concurrent `drop` as open point), and 2026-10-23 (rework of file content handling: line endings of `request.md` and the JSON files, `editorconfig-unreadable` and `editorconfig-invalid` warnings). Implemented except the phase fields and the approval history (package 04; FP03-AC11 "`done` after `phase complete`" waits for it) and trackers beyond GitHub (**Open**). |
| Source | Concept §3.1, §3.2, §4.1–4.3, §5.1 (`new`, `drop`), §11.4 |
| Depends on | 01, 02 |
| Enables | 04 and every phase package |

Labels follow the concept: **Decided**, **Proposed**, **Open**, **Unverified**, **Implemented**.

## Benefit

Several developers can create features in parallel on their own branches without id collisions. The engine always knows which feature a developer is working on, from the branch name or a local override, so neither the human nor the agent has to repeat it in every session.

## Scope

### In scope

- Feature ids.
- The feature folder and the identity part of `feature.json`.
- `feature new`, `feature list`, `feature current`, `feature set`, `feature show`, `feature drop`.
- Resolving the current feature.

### Out of scope

- Phases, phase states, approvals, and history in `feature.json` (package 04). `feature new` sets the first phase through package 04's rules once that package exists.
- Fetching issue content from a tracker. The agent does that in the `new` skill (package 09); the engine only receives the result.
- The separate spec store (later, concept §4.4).

## Functional description

**Implemented** in the Core namespaces `CreativeCoders.ShadeKit.Core.Features` (`IFeatureStore` / `FeatureStore`, `ICurrentFeatureResolver` / `CurrentFeatureResolver`, `IFeatureIdFactory` / `FeatureIdFactory`) and `CreativeCoders.ShadeKit.Core.Git`, and in the CLI commands under `Commands/Feature/`. All services are singletons registered by `AddShadeKitCore()`, together with `TimeProvider.System`.

### Feature ids

- No sequential numbers. **Decided** (concept §4.2).
- With an issue: `<tracker prefix>-<number>-<slug>`, for example `gh-142-login-sso`. The prefix comes from the `trackers` mapping in `config.json` (package 02). **Decided** (format, concept §4.2), **Proposed** (mapping). **Implemented** (`FeatureIdFactory`). An invalid prefix (not lowercase letters a-z and digits joined by single hyphens) makes `config.json` invalid (exit code 22, `invalid-file`), so `new` stops for every source kind until the configuration is fixed. **Decided** on 2026-10-22 (code review verification, finding 2). **Implemented** (`KitConfig`).
- Without an issue: `<yyyymmdd>-<hhmm>-<slug>`, for example `20261013-1530-login-sso`. **Decided** (concept §4.2). The time is the local time of the machine. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureIdFactory`, `TimeProvider.GetLocalNow()`).
- The slug is built from the title, at most 40 characters. **Decided** on 2026-10-20 (implementation clarification); rules in [Id, title, and source rules](#id-title-and-source-rules). **Implemented** (`FeatureSlug`).
- A valid feature id matches `^[a-z0-9]+(-[a-z0-9]+)*$`. Every id from user input (`set`, `show`, `drop`, the branch name, the override) is checked against it before a path is built from it. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureIds`).
- A feature exists if and only if its folder `.shadekit/features/<id>/` exists. **Decided** on 2026-10-20 (implementation clarification).
- The folder name is the feature id in every command (`list`, `current`, `show`, `set`, `drop`, and the paths). If the `id` in `feature.json` differs, for example after a folder was copied or renamed, `list`, `show`, and `current` add the warning `feature-id-mismatch` and leave the file unchanged. **Decided** on 2026-10-21 (code review rework); before, `show` and `current` returned the `id` from the file with the paths of the folder. **Implemented** (`FeatureStore`).
- Folders in `features/` whose names start with `.` are not features: they are the staging and dropping folders of `new` and `drop`, also those a failed run left behind. `list` ignores them silently; their names are never valid ids, so `set`, `show`, and `drop` reject them (exit code 32) and the branch and the override never select them. A folder whose name is not a valid id is skipped by `list` with the warning `feature-folder-invalid`. **Decided** on 2026-10-21 (code review rework). **Implemented** (`FeatureStore.ListAsync`).
- `set`, `show`, `drop`, the existence check of `new`, and the branch and override lookup match the folder name exactly (ordinal comparison with the real folder names in `features/`, the same source `list` uses). A folder whose name differs from the id only in case, for example `20261022-1424-LOGIN-SSO` for the id `20261022-1424-login-sso`, is not that feature: `set`, `show`, and `drop` stop with exit code 31 (`feature-not-found`), and the branch and the override do not select it. On a case-insensitive file system (default on macOS and Windows) `new` still ends with exit code 30 (`feature-already-exists`) for such a folder, because the move into place sees the destination as existing; that is a limit of the file system. **Decided** on 2026-10-22 (code review verification, finding 3); before, the path check ignored case on such file systems, so these commands used a folder that `list` skips as `feature-folder-invalid`. **Implemented** (`KitStoreFeatureExtensions.FeatureExists`).
- If the id already exists, `new` stops with exit code 30 (`feature-already-exists`) and changes nothing. It does not add a suffix silently, because an existing id usually means the feature was already created on another branch. **Proposed**, **Implemented** (`FeatureAlreadyExistsException`).

### Id, title, and source rules

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureIdFactory`, `FeatureSlug`).

- Source kind of `--source` (one value): an absolute `http` or `https` URI is `url`; otherwise a path (relative to the working directory, or absolute) for which `File.Exists` is true is `file`; everything else, including a directory or a missing path, is `text`. An empty or whitespace-only source stops with exit code 33 (`invalid-source`). **Decided** on 2026-10-21 (code review rework) for `File.Exists`: on Unix it also counts devices, FIFOs, sockets, and symbolic links whose target is missing as files; only documented .NET APIs are used, so they are not told apart.
- Issue-based id: the URL host is matched against the keys of `trackers` case-insensitively and exactly (no `www.` stripping). The issue number is the last path segment, which must consist only of ASCII digits (`/issues/142`, `/pull/142`, `/-/issues/142`; `/issues/142/comments` has no issue number); query and fragment are ignored. A host in `trackers` whose last path segment is not an issue number stops with exit code 33. A host not in `trackers` gives a time-based id; the source kind stays `url`.
- Title: `--title` wins. Without it: `text` → the first non-empty line, trimmed; `file` → the first non-empty level-1 Markdown heading (`# …`), otherwise the file name without extension; `url` → no title can be derived, so `--title` is required (exit code 34, `invalid-title`).
- Heading of a `file` source: a line indented by at most 3 spaces that starts with `#` and a space or tab, as a CommonMark ATX heading. Lines in fenced code blocks (```` ``` ```` or `~~~`, closed by a fence of the same character at least as long) are skipped; a fence that is never closed runs to the end of the file, so no heading after it counts. A leading front matter block is skipped: it starts with a first line `---` and ends with the next line `---` or `...` (trailing whitespace allowed); without such an end line the file has no front matter. A closing sequence of `#` preceded by a space or tab is not part of the title: `# Login with SSO #` → `Login with SSO`, while `# C#` stays `C#`; a heading of only a closing sequence (`# #`) is empty and skipped. **Decided** on 2026-10-22 (code review verification, finding 7); before, `# ` lines in code blocks and front matter became the title and a closing `#` stayed in it. **Implemented** (`FeatureIdFactory`, `MarkdownCodeFences`).
- Every title, from `--title` after trimming, the first line, the heading, or the file name, must not contain a control character (`char.IsControl`, including CR, LF, and TAB); otherwise exit code 34. The message names the character and the origin of the title. **Decided** on 2026-10-21 (code review rework). **Implemented** (`FeatureIdFactory`).
- Slug: diacritics are removed (`ä` → `a`), the text is lower-cased, every run of characters other than `a-z` and `0-9` becomes one `-`, and leading and trailing `-` are removed. A slug longer than 40 characters is cut at the last `-` within the limit, or after 40 characters if there is none. A title that gives an empty slug stops with exit code 34; the message points to `--title`.
- Profile: `--profile`, else `defaultProfile` from `config.json` (the defaults apply if `config.json` is missing). It must be `feature`, `bugfix`, or `quick`, else exit code 40 (`unknown-profile`). **Implemented** (`WorkflowProfiles`, `UnknownProfileException`). Package 04 owns the profiles; until it exists, this package validates the names.

### File sources

**Decided** on 2026-10-21 (code review rework, with the user's correction of the same day: no internal runtime API, a size limit instead of a file type check). **Implemented** (`FeatureIdFactory`, `TextFileEncoding`). Every check runs before anything is written, and every failure stops with exit code 33 (`invalid-source`) and a message with the reason.

- Size: at most `FeatureIdFactory.MaxSourceFileSize`, 1 MiB (1,048,576 bytes), counted on the raw bytes including a UTF-8 byte order mark. The file is read through a stream, at most the limit plus one byte, without trusting the reported length, so a larger file or an endless device such as `/dev/zero` is refused without being read completely. `/dev/null` is an empty file source (title `null`).
- Read errors: a file that cannot be opened or read (`IOException`, `UnauthorizedAccessException`, for example missing permissions, a lock, a symbolic link whose target is missing, a link loop, or a socket) stops with "The file cannot be read: …".
- Encoding: UTF-8 with or without byte order mark is accepted. A UTF-16 or UTF-32 byte order mark, a NUL byte (UTF-16 or UTF-32 without byte order mark, or a binary file), or bytes that are not valid UTF-8 stop the command. The detection is shared with package 02 (`Files/TextFileEncoding`, also used by `TextFileContent`, whose behaviour is unchanged).
- Residual risk: a FIFO (named pipe) is not detected. Opening a FIFO that no process writes to blocks, so `feature new` waits until a writer opens it.

### Feature folder

`.shadekit/features/<feature-id>/` with:

- `feature.json`: `schemaVersion`, `minEngineVersion`, `id`, `title`, `profile`, `source` (kind and value), `createdAt`, `createdBy`, `status`. Package 04 adds the phase fields. **Implemented** (`FeatureDocument`, `FeatureSource`, `FeatureCreator`, `FeatureStatus`; format id `feature`, version 1).
- `request.md`: created from the `request.md` template (package 09 owns its content). The engine fills the placeholders it owns; the `new` skill completes the file. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureRequestTemplate`); details in [`request.md`](#requestmd).

Feature `status`: `active` or `done`. **Proposed.** A missing `status` is read as `active`. **Implemented** (`FeatureStatus`). A dropped feature has no status, because its folder is deleted.

`createdBy` and every other "who" in the kit is the git user (`user.name` and `user.email`) of the repository. **Proposed.** **Implemented** (`GitRepositoryInfo`); `new` stops with exit code 36 (`git-identity-missing`) if either is not set.

### `feature.json` (v1)

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureDocument`).

```json
{
  "schemaVersion": 1,
  "minEngineVersion": "0.1.0",
  "id": "gh-142-login-sso",
  "title": "Login SSO",
  "profile": "feature",
  "source": { "kind": "url", "value": "https://github.com/acme/shop/issues/142" },
  "createdAt": "2026-10-20T15:30:00+02:00",
  "createdBy": { "name": "Jane Doe", "email": "jane@example.com" },
  "status": "active"
}
```

- `source.kind` is `text`, `url`, or `file`. `source.value` is the URL as given for `url`; for `file` the path relative to the repository root with `/` as separator, or the absolute path if the file is outside the repository; for `text` it is absent.
- `createdAt` is ISO 8601 with the local offset, in whole seconds.
- Unknown fields are kept at the top level, in `source`, and in `createdBy` (package 01).

### `current-feature.json` (v1)

The local override, `.shadekit/features/current-feature.json`, gitignored (package 02). **Implemented** (`CurrentFeatureDocument`; format id `current-feature`, version 1).

```json
{
  "schemaVersion": 1,
  "minEngineVersion": "0.1.0",
  "featureId": "gh-142-login-sso"
}
```

Clearing the override deletes the file.

An override that cannot be used because of its own format (an invalid file, a format that is too new, or an outdated format when `outdatedFormatOnRead` is `stop`, package 01) holds no data but the pointer, so it does not stop the main workflow. **Decided** on 2026-10-21 (code review rework); before, it stopped `new`, `current`, and `set`, and only deleting it by hand helped. **Implemented** (`CurrentFeatureOverride`):

- `current` ignores it with the warning `current-feature-invalid`, whose message gives the reason, and may still resolve the feature from the branch. The file is left unchanged.
- `set` and `new` replace it (delete, then write) without a warning.
- `drop` leaves it alone.

In `current`, `set`, and `new`, a format problem of any other file, for example of `config.json` while the policy is read, still stops the command.

### `request.md`

**Decided** on 2026-10-20 (implementation clarification), replacing "filled with the original input" of this package and "the agent fills `request.md`" of package 09. **Implemented** (`FeatureRequestTemplate`).

- The engine reads the repository template `.shadekit/templates/request.md`; if it is missing, it uses the shipped template (`request.template.md`, package 02). A directory at that path does not count as a missing template: `new` stops with exit code 14 (`path-not-a-file`, package 02) before the feature folder appears, so a customised template is never ignored without notice. **Decided** on 2026-10-24 (code review of the branch, finding 5), replacing "a directory counts as a missing file". **Implemented** (`KitStore.ReadFileAsync`, `ReadFileAsync_PathIsDirectory_ThrowsPathNotAFileException`, `CreateAsync_RequestTemplateIsDirectory_ThrowsPathNotAFileExceptionAndCreatesNoFeature`).
- It replaces exactly these placeholders, every occurrence, in one pass, so a value that contains a placeholder is never replaced again:

  | Placeholder | Value |
  |---|---|
  | `{{feature-title}}` | The title. |
  | `{{feature-id}}` | The id. |
  | `{{source kind and source reference}}` | `<kind>: <value>`, for example `url: https://github.com/acme/shop/issues/142`; `text: n/a` for text. |
  | `{{original input, unchanged}}` | The text, the file content (UTF-8; its line endings are not kept, see below), or the URL. |
  | `{{created at and created by}}` | `<createdAt> by <name> <<email>>`. |

- Everything else, including the guidance comment, stays. The `new` skill (package 09) completes the file, for example with the issue title and body for a URL source, and removes the guidance comment.
- Written as UTF-8 without byte order mark.
- Line endings: `request.md`, `feature.json`, and `current-feature.json` are written as a whole with the line ending decided for each file when it is written (git attributes and configuration for the path, then `.editorconfig`, then the operating system; package 02), so the original input in `request.md` takes that line ending too. If an `.editorconfig` file cannot be read or evaluated, the `.editorconfig` files do not decide and the warning `editorconfig-unreadable` or `editorconfig-invalid` (package 02) is reported, which `new` and `set` report once, after the format and feature warnings. **Decided** on 2026-10-23 (rework of file content handling), replacing "the original input keeps its line endings". **Implemented** (`FeatureStore`, `IKitStore.WriteFileAsync` / `WriteJsonAsync` through `ITextFileWriter`, `CreateAsync_FileSource_FillsRequestWithRepositoryRelativePathAndNormalisedFileContent`, `CreateAsync_StoreWritesWithWarnings_ReturnsFormatWarningsFirstAndEachStoreWarningOnce`, `SetCurrentAsync_StoreWritesWithWarnings_ReturnsMismatchWarningFirstAndEachStoreWarningOnce`). `feature.json` and `request.md` are written into the hidden staging folder, but their line ending is decided for their final path `.shadekit/features/<id>/...`, so git attributes and `.editorconfig` sections for the feature folder apply; a result carries only the warnings of its own writes, not those of earlier operations on the same store. **Decided** on 2026-10-23 (code review of the rework). **Implemented** (`IKitStore.CreateDirectoryWriter`, which `CreateDirectoryExclusiveAsync` passes to the folder content, and `lineEndingFile` of `ITextFileWriter.WriteAsync`, `CreateAsync_NewFeature_ResolvesLineEndingOfEachFileForItsFinalPathNotTheStagingPath`, `CreateAsync_FinalFeaturePathPrescribesOtherLineEnding_WritesFeatureFilesWithLineEndingOfFinalPath`, `CreateAsync_StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation`, `SetCurrentAsync_StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation`). A lone CR in the original input or in the repository template ends a line as well, so `request.md` never has mixed line endings; other Unicode line separators stay part of a line. `new` reads the git attributes of the three files (`feature.json` and `request.md` for their final path, and `current-feature.json`) with one `git check-attr` before the first write (`IKitStore.PrepareLineEndingsAsync`). **Decided** on 2026-10-24 (code review of the branch, findings 3 and 7). **Implemented** (`TextFileContent`, `ReadAsync_LoneCarriageReturn_EndsLine`, `CreateAsync_NewFeature_PreparesLineEndingsOfAllFinalPathsOnceBeforeFirstWrite`).

### Commands

| Command | Behaviour |
|---|---|
| `feature new --source <text\|url\|file> [--profile <p>] [--title <t>] [--json]` | Creates the folder, `feature.json`, and `request.md`, staged in a hidden folder and moved into place in one step. The profile defaults to `defaultProfile` from `config.json`. A URL whose host is in `trackers` gives an issue-based id; the number comes from the URL, the slug from `--title`. Any other source gives a time-based id. For a file source, the file content is copied into `request.md`. Writes the local override so the new feature is current right away. Returns the id, paths, and suggested branch. **Proposed** details. **Implemented** (`FeatureNewCommand`, `FeatureStore.CreateAsync`); short options `-s`, `-p`, `-t`. |
| `feature list [--json]` | Lists features with id, title, profile, status, and (once package 04 exists) the current phase. Folders in ordinal order. Folders whose names start with `.` are ignored; a folder whose name is not a valid id is skipped with the warning `feature-folder-invalid`; a folder without `feature.json` is skipped with the warning `feature-file-missing`; an invalid or too new `feature.json` stops the command (exit code 22 or 20, package 01). **Implemented** (`FeatureListCommand`, `FeatureStore.ListAsync`). |
| `feature current [--json]` | Returns the current feature: id, title, profile, feature `status` (`active` or `done`), where it was resolved from, artifact paths, and (once package 04 exists) current phase and phase status. If none can be resolved, returns "not resolved" as a result with exit code 0, not as an error (package 01 contract). **Implemented** (`FeatureCurrentCommand`, `FeatureStore.GetCurrentAsync`). |
| `feature set <id> [--json]` | Writes `features/current-feature.json`. Stops if the id is invalid (exit code 32) or does not exist (exit code 31). Warns with `current-feature-mismatch` if the current branch selects another feature, because the branch wins. The branch is read before the override is written, so a failing git leaves the override unchanged. **Implemented** (`FeatureSetCommand`, `FeatureStore.SetCurrentAsync`). |
| `feature show <id> [--json]` | Returns the complete state of a feature: id, title, profile, status, source, created at and by, paths, and (once package 04 exists) phases and the approval history. Needed by the report in package 15. **Proposed.** **Implemented** without the package 04 fields (`FeatureShowCommand`, `FeatureStore.ShowAsync`). |
| `feature drop <id> [--yes] [--json]` | Deletes the feature folder and clears the local override if it points to this feature. Possible in every phase. With `--yes` it deletes at once. Without `--yes` it first describes what it would delete, including whether the override would be cleared (`overrideAffected`); then, only in plain mode (no `--json`) with an interactive standard input, it asks for a confirmation in the console and deletes after a yes. In JSON mode or with redirected standard input it stops after the description and deletes nothing, so an agent can never hang on the question. An agent calls it with `--json`: first without `--yes` to get the description it shows the human, and after the human's yes in the conversation with `--yes`. **Decided** on 2026-10-18. Every mode without `--yes` ends with exit code 0; the question defaults to no. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureDropCommand`, `FeatureStore.PreviewDropAsync` / `DropAsync`, `IConsoleConfirmation` / `ConsoleConfirmation`); short option `-y`. |

`show` and `current` on a feature folder without `feature.json` stop with exit code 22 (`invalid-file`), with a reason that names the missing file. **Decided** on 2026-10-20 (implementation clarification). `drop` works with a missing or broken `feature.json`, so such a folder can always be removed. **Implemented** (`FeatureStore`). `show` reports feature warnings (`feature-id-mismatch`) like `list` and `current`. **Decided** on 2026-10-21 (code review rework). **Implemented** (`FeatureShowResult`).

### Order of work and failures for `new`

**Decided** on 2026-10-20 (implementation clarification); staging, the atomic move, and the rollback of the override write **Decided** on 2026-10-21 (code review rework), because two concurrent runs with the same id both passed the existence check and the rollback of one could delete the feature the other had written. **Implemented** (`FeatureStore.CreateAsync`, `IKitStore.MoveDirectory`).

1. Read `config.json`; check the profile (exit code 40).
2. Source, title, and id (exit codes 33, 34).
3. The feature folder must not exist (exit code 30).
4. Read an existing `current-feature.json` for writing: its format is checked without migration, and `config.json` is not read. An override that cannot be used because of its own format (invalid, too new, or outdated) is replaced in step 7, so no format problem stops `new` here. Only an override that cannot be read at all (an IO error or access denied) stops now; that failure is not mapped and ends with exit code 255 (`host-error`). **Decided** on 2026-10-22 (code review verification, finding 5). **Implemented** (`FeatureStore.CreateAsync`).
5. Read the git identity (exit codes 36, 16).
6. Create the hidden staging folder `features/.<id>.<8 hex digits>.tmp/`, write `feature.json`, then `request.md` into it, and move it to `features/<id>/` in a single rename that refuses an existing file or folder (residual risk in the [open points](#open-points)). If the destination appeared in the meantime, for example from a concurrent run, `new` stops with exit code 30 and leaves that folder untouched, also when the move failed with access denied. If the staging folder cannot be created or the move fails with an IO error or access denied for another reason (for example a `features/` folder that may not be written, or a file in the staging folder that another program holds open), `new` stops with exit code 35 (`feature-io-failed`); see below.
7. Write the override.

A failure before step 6 changes nothing. If step 6 fails or `new` is canceled, `new` deletes only its own staging folder (best effort) and fails. A failure to create the staging folder or to move it into place ends with exit code 35 (`feature-io-failed`): nothing was changed, the staging folder is removed, and the override is untouched; the message names the folder, the operation (`create`), and the cause, and points to the write permissions of `features/` and to programs that hold files open. A write failure inside the staging folder (`feature.json` or `request.md`) is not mapped and ends with exit code 255 (`host-error`). **Decided** by the user on 2026-10-21 (second code review, finding N01); before, these failures ended with exit code 255 and a stack trace. There is no retry, also not on Windows. **Implemented** (`FeatureIoFailedException`, `FeatureStore.CreateAsync`). If step 7 fails or `new` is canceled during it, `new` moves the feature back to a hidden folder and deletes it (best effort), so the override never points to a removed feature; if even the move back fails, the complete feature stays without override. The feature folder did not exist before, so nothing is lost and the id can be used again. A staging folder left behind starts with `.`, so it is ignored and does not block the id.

A file (not a folder) at `features/<id>` passes step 3 and makes the move in step 6 fail with an IO error that is not mapped (exit code 255, `host-error`); the staging folder is removed.

### `feature drop`

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureStore.DropAsync`, `FeatureDropCommand`).

- Checks the id (exit code 32) and that the folder exists (exit code 31).
- Reads the override before changing anything: `overrideAffected` is true if its `featureId` is the dropped id. An override that cannot be read is left alone.
- Renames `features/<id>/` to the hidden folder `features/.<id>.<8 hex digits>.dropping/`, deletes that folder, then deletes `current-feature.json` if `overrideAffected` is true (`overrideCleared`). The rename is atomic: if it fails with an IO error or access denied, for example because a file is locked or `features/` may not be written, nothing is deleted, the override is untouched, and `drop` stops with exit code 35 (`feature-io-failed`, operation `drop`; **Decided** by the user on 2026-10-21, second code review, finding N01; before, exit code 255 with a stack trace). After it, the feature no longer exists for `list`, `show`, or any other command. If deleting the hidden folder fails, the override is still cleared (best effort) and the error is reported (an IO error is not mapped: exit code 255, `host-error`); only the hidden leftover remains, which is ignored. **Decided** on 2026-10-21 (code review rework); before, a recursive delete in place could fail halfway and leave a half-deleted feature with the override untouched. **Implemented** (`FeatureStore.DropAsync`, `IKitStore.MoveDirectory`).
- Without `--yes`: JSON mode returns the preview with `dropped: false` and exit code 0. Plain mode with redirected standard input prints the preview and a hint to run with `--yes`, exit code 0, without waiting. Plain mode with an interactive console asks `Drop feature <id>?` (default no); after a no it prints `Nothing was dropped.` with exit code 0.

### Resolving the current feature

Order (**Decided**, concept §4.3). **Implemented** (`CurrentFeatureResolver`):

1. The current branch name. The id is the part after the last `/` (`feature/gh-142-login-sso` → `gh-142-login-sso`). It counts only if it is a valid id and a feature folder with that id exists. **Proposed** (matching rule), the valid-id check **Decided** on 2026-10-20 (implementation clarification). A detached HEAD gives no branch and no warning.
2. The local override `features/current-feature.json`. An override that names an invalid id or a feature whose folder does not exist is ignored with the warning `current-feature-missing` (also when the branch wins); the file is left unchanged. **Decided** on 2026-10-20 (implementation clarification). An override that cannot be used because of its own format is ignored with the warning `current-feature-invalid`; the branch may still resolve. **Decided** on 2026-10-21 (code review rework); see [`current-feature.json`](#current-featurejson-v1).
3. Not resolved; the agent asks the human and calls `feature set`.

If 1 and 2 both give a feature and they differ, the branch wins and the command adds the warning `current-feature-mismatch`. **Proposed** in the concept, adopted here. **Implemented**.

`current-feature.json` is gitignored (package 02) and has its own `schemaVersion` like every JSON file (package 01).

### Git access

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`IGitCommandRunner` / `GitCommandRunner`, `IGitRepositoryInfo` / `GitRepositoryInfo`, `IGitExecutableLocator` / `GitExecutableLocator`).

- The engine starts the `git` executable in the repository root; there is no git library.
- The executable is resolved only from absolute `PATH` entries; empty and relative entries are skipped, and the first entry that contains the file wins. On Unix the file must also have at least one execute bit (owner, group, or others), like in the `PATH` search of a shell; a file without one is skipped and the next entry is checked (**Decided** on 2026-10-21, second code review, finding N02; before, a non-executable `git` in an earlier entry stopped the command with exit code 16). Windows: separator `;`, file `git.exe`, double-quoted entries supported; Unix: separator `:`, file `git`. `PATHEXT` is not used, so a `git.cmd` shim is not found. `GitCommandRunner` starts the resolved absolute path and refuses a relative one. If no entry contains a usable `git`, the command stops with exit code 16. **Decided** on 2026-10-21 (code review rework, finding F01): starting `git` by its bare name let Windows search the current directory, usually the repository, before `PATH`, so a `git.exe` committed to a cloned repository would have run.
- Branch (`current`, `set`): `git symbolic-ref --short -q HEAD`; exit code 1 with empty output means a detached HEAD.
- Identity (`new`): `git config user.name` and `git config user.email`; exit code 1 means not set (exit code 36).
- If `git` cannot be started or fails unexpectedly, the command stops with exit code 16 (`git-unavailable`).
- `list`, `show`, and `drop` do not call git.

### Branches

The engine does not create or switch branches in v1. `feature new` returns the suggested branch name `feature/<id>` so the skill can offer to create it. **Proposed.** **Implemented** (`suggestedBranch`).

### Output

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`Feature<X>CommandData`, `FeatureCommandOutput`).

- `--json`: one envelope (package 01). Paths are relative to the repository root with `/` as separator; `null` values are written. `data` per command:

  | Command | `data` |
  |---|---|
  | `new` | `{ id, title, profile, status, source: { kind, value }, paths, suggestedBranch }` |
  | `list` | `{ features: [ { id, title, profile, status } ] }` |
  | `current` | `{ resolved, resolvedFrom, feature: { id, title, profile, status, paths } }`; `resolvedFrom` is `branch` or `override`; not resolved: `{ resolved: false, resolvedFrom: null, feature: null }` |
  | `set` | `{ id }` |
  | `show` | `{ id, title, profile, status, source, createdAt, createdBy: { name, email }, paths }` |
  | `drop` | `{ id, dropped, files, overrideAffected, overrideCleared }`; `files` lists every file in the folder; `overrideAffected` (the override points to this feature, so it is or would be cleared) is set in the preview and after the drop; `overrideCleared` is true only after the override was deleted (`overrideAffected` **Decided** on 2026-10-21, code review rework; before, the JSON preview could not tell that the override would be cleared) |

  `paths` is `{ featureDir, featureFile, request }`, for example:

  ```json
  {
    "featureDir": ".shadekit/features/gh-142-login-sso/",
    "featureFile": ".shadekit/features/gh-142-login-sso/feature.json",
    "request": ".shadekit/features/gh-142-login-sso/request.md"
  }
  ```

  `warnings` holds the format warnings (`upgrade-needed`, package 01) first, then the feature warnings, then the warnings of the written files (`editorconfig-unreadable`, `editorconfig-invalid`, see [request.md](#requestmd)).
- Plain: each warning as `Warning: <message>` before the result, then:
  - `new`: `Created feature <id> (<profile>).`, the paths of `feature.json` and `request.md`, `Suggested branch: feature/<id>`.
  - `list`: one line `<id>  <status>  <profile>  <title>` per feature, or `No features found.`
  - `current`: `<id> (<profile>, <status>)` and `Resolved from: <branch|override>`, or `No current feature.` with a hint to run `shade-kit feature set <id>`.
  - `set`: `Current feature: <id>.`
  - `show`: one `Key: value` line each for id, title, profile, status, source, created, and the three paths.
  - `drop`: the preview (`Feature <id> would be dropped. Files:` with the files, and `The local override points to this feature and would be cleared.` if `overrideAffected`), then the question or, with redirected standard input, `Nothing was dropped. Run 'shade-kit feature drop <id> --yes' to drop it.`; after a no `Nothing was dropped.`; after the drop `Dropped feature <id>.` and, if the override was cleared, `The local override pointed to this feature and was cleared.`

  All text is escaped for Spectre.Console markup.

### Cancellation

**Decided** on 2026-10-21 (code review rework); before, Ctrl+C ended the process at once, so the rollback of `new` could not run from the CLI. **Implemented** (`ICliCancellation` / `ConsoleCancellation`, `Program`).

- The first Ctrl+C of a run keeps the process alive and cancels the run's token. Every feature command and `init` pass the token to the core, so `new` and `init` stop at their next check and roll back what they created in this run.
- A second Ctrl+C uses the runtime's default handling and ends the process at once, so a command that does not stop can still be ended; no rollback runs then. A staging or dropping folder left behind starts with `.` and is ignored.
- A canceled run ends with exit code 130 (`canceled`), `The command was canceled.`, and no stack trace; in JSON mode with one failure envelope. **Decided** on 2026-10-21 (code review rework).

### Errors

Exit codes owned by this package. **Decided** on 2026-10-20 (implementation clarification); 2 and 130 added in the code review rework on 2026-10-21, 35 after the second code review on 2026-10-21. **Implemented** (`ShadeKitCliExitCodes`, `ShadeKitCliErrorCodes`, `CliErrorMapper`).

| Exit code | Error code | When | Files changed |
|---|---|---|---|
| 2 | `invalid-arguments` | Every command: the option parser rejects the arguments, for example a missing required argument (`feature show` without an id, `feature new` without `--source`). The message is `Invalid arguments: <detail>. Run 'shade-kit <command> --help' for usage.`, without a stack trace. Outside the area ranges (package 01). **Decided** on 2026-10-21 (code review rework); before, exit code 255 with a stack trace. **Implemented** (`CliErrorMapper.MapArgumentError`, `Program`). | None |
| 16 | `git-unavailable` | `git` is not found in an absolute `PATH` entry, cannot be started, or fails unexpectedly (`GitUnavailableException`). | None |
| 30 | `feature-already-exists` | `new`: the folder of the id exists, also when a concurrent run created it before the move, even if the move then failed with access denied (`FeatureAlreadyExistsException`). | None; the other run's folder is untouched |
| 31 | `feature-not-found` | `set`, `show`, `drop`: no folder for the id (`FeatureNotFoundException`). | None |
| 32 | `invalid-feature-id` | The id does not match `^[a-z0-9]+(-[a-z0-9]+)*$` (`InvalidFeatureIdException`). | None |
| 33 | `invalid-source` | `new`: empty `--source`; a URL whose host is in `trackers` and whose last path segment is not an issue number; a file source larger than 1 MiB, that cannot be read, or that is not UTF-8 text ([File sources](#file-sources)) (`InvalidFeatureSourceException`). | None |
| 34 | `invalid-title` | `new`: a URL source without `--title`, a title with a control character, or a title that gives an empty slug (`InvalidFeatureTitleException`). | None |
| 35 | `feature-io-failed` | `new`: the hidden staging folder cannot be created or cannot be moved to `features/<id>/`; `drop`: `features/<id>/` cannot be renamed to its hidden dropping folder. In each case the file system reports an IO error or access denied (`IOException`, including `DirectoryNotFoundException`, or `UnauthorizedAccessException`), unless the destination folder of `new` exists by then (exit code 30) or a file is at `features/<id>` (exit code 255). The message names the folder, the operation (`create` or `drop`), and the cause, and points to the write permissions of `features/` and to programs that hold a file open, such as an editor, a virus scanner, or a search indexer (`FeatureIoFailedException`). **Decided** by the user on 2026-10-21 (second code review, finding N01). | None; a staging folder that was already created is removed, the override is untouched |
| 36 | `git-identity-missing` | `new`: `user.name` or `user.email` is not set (`GitIdentityNotConfiguredException`). | None |
| 40 | `unknown-profile` | `new`: the profile from `--profile` or `defaultProfile` is not `feature`, `bugfix`, or `quick` (`UnknownProfileException`). In package 04's range, because profiles belong to it; this package owns the check until 04 exists. | None |
| 130 | `canceled` | Every command: the run was canceled with Ctrl+C. The message is `The command was canceled.`, without a stack trace. 128 + SIGINT, outside the area ranges like 2 (package 01). **Decided** on 2026-10-21 (code review rework). **Implemented** (`CliErrorMapper.MapCancellation`, `ICliCancellation` / `ConsoleCancellation`). | `new` and `init` (package 02) remove what they created in this run (best effort) |

Reused codes: 10 (`kit-root-not-found`), 12 (`not-a-git-repository`), and 15 (`kit-root-not-a-directory`) when the store cannot be opened; 20 (`format-too-new`), 21 (`upgrade-needed`), and 22 (`invalid-file`) for kit files, following package 01. Any other failure is not mapped and ends with exit code 255 (`host-error`), for example an IO error while `drop` deletes the hidden folder after the rename (the override is still cleared), an IO error while `new` writes `feature.json` or `request.md` into its staging folder, a file (not a folder) at `features/<id>` during `new`, or an option class the parser cannot create (a defect of the CLI).

### Warnings

**Decided** on 2026-10-20 (implementation clarification); `current-feature-invalid`, `feature-folder-invalid`, and `feature-id-mismatch` on 2026-10-21 (code review rework). **Implemented** (one `KitWarning` record per warning in `Features/`, `ShadeKitCliWarningCodes`).

| Warning code | When |
|---|---|
| `current-feature-mismatch` | Branch and override select different existing features; the branch wins. Also from `feature set` when the current branch selects another feature. |
| `current-feature-missing` | The override names an invalid id or a feature whose folder does not exist; it is ignored and left unchanged. |
| `current-feature-invalid` | `feature current`: the override cannot be used because of its own format (invalid file, too new, or outdated under `outdatedFormatOnRead: stop`); it is ignored and left unchanged, and the message gives the reason. The branch may still resolve. |
| `feature-file-missing` | `feature list` found a feature folder without `feature.json`; the folder is skipped. |
| `feature-folder-invalid` | `feature list` found a folder whose name is not a valid id; the folder is skipped. |
| `feature-id-mismatch` | `feature list`, `show`, or `current`: the `id` in `feature.json` differs from the folder name; the folder name is used and the file is left unchanged. |

### Store additions

`IKitStore` gained `ReadFileAsync`, `FileExists`, `DirectoryExists`, `GetFiles` (recursive, kit-relative paths with `/`, ordinal order; does not follow directory links), `DeleteFile`, and `DeleteDirectory`; all check that the path stays inside the kit root. `KitStorePaths` gained `FeatureFileName` and `RequestFileName`. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`IKitStore`, `KitStore`, `KitStorePaths`).

The code review rework added `IKitStore.MoveDirectory` (a single rename that refuses an existing destination), `KitStorePaths.CurrentFeatureFilePath`, `KitStorePaths.GetFeatureDirPath`, and `FeatureTextFormat` (the shared `createdAt` format `yyyy-MM-ddTHH:mm:sszzz` of `request.md` and `show`), so each of these is written down once. **Decided** on 2026-10-21 (code review rework). **Implemented** (`IKitStore`, `KitStore`, `KitStorePaths`, `FeatureTextFormat`).

`KitStorePaths.GetDisplayPath` builds the repository path that messages and the file path of format exceptions show (`.shadekit/` plus the kit-relative path with `/`). The store uses it for every file path it reports, and the read of the local override compares a format exception's file path with it to tell a problem of the override from a problem of `config.json`, so the two strings cannot drift apart. **Decided** on 2026-10-22 (code review verification, finding 6). **Implemented** (`KitStorePaths`, `KitStore`, `CurrentFeatureOverride`).

### Deviations from the draft

The implementation on 2026-10-21 changed these statements of the earlier draft. All were decided by the user in the implementation clarification on 2026-10-20.

| Draft | Now | Reason |
|---|---|---|
| `feature list` "**Implemented** with feature names only"; JSON `features` was a list of strings. | Returns id, title, profile, and status; `features` is a list of objects. | Package scope; breaking change of the earlier `feature list --json` contract. |
| This package: the engine fills `request.md` "with the original input"; package 09: the agent fills `request.md`. | The engine fills the five placeholders it owns; the `new` skill completes the file. | The two packages contradicted each other. |
| The slug is "limited to a short length". | At most 40 characters, cut at a `-`. | Unspecified. |
| Time-based id without a time zone. | Local time. | Unspecified. |
| `feature drop` without `--yes`: exit code not stated. | Exit code 0 in every mode. | Unspecified. |

### Review

The implementation was reviewed on 2026-10-21 ([review report](../reviews/2026-10-21-feature-03-feature-management-uncommitted.md)): 1 Major, 10 Minor, 2 Suggestion, and 6 Nitpick findings (F01–F19). Every finding except F03 is fixed in the code review rework of 2026-10-21:

- F01 (Major): `git` is resolved only from absolute `PATH` entries ([Git access](#git-access)).
- F02: `GitExecutableLocatorTests` and `GitCommandRunnerTests` cover the executable lookup and the refusal of a relative path without starting a process; the process run itself stays untested ([open points](#open-points)).
- F04: `overrideAffected` in the `drop` data, in the preview and after the drop ([Output](#output)).
- F05: Ctrl+C cancels the run's token, so the rollback of `new` runs ([Cancellation](#cancellation)).
- F06: option parser failures end with exit code 2 (`invalid-arguments`) ([Errors](#errors)).
- F07, F08, F09: size limit, read errors, and encodings of file sources ([File sources](#file-sources)); control characters in titles ([Id, title, and source rules](#id-title-and-source-rules)).
- F10: an unusable `current-feature.json` no longer stops `new`, `current`, or `set` ([`current-feature.json`](#current-featurejson-v1)).
- F11: `new` stages the feature and moves it into place atomically ([Order of work](#order-of-work-and-failures-for-new)).
- F12: the folder name is the id in every command, with the warning `feature-id-mismatch` ([Feature ids](#feature-ids)).
- F13: `drop` renames the folder before it deletes it ([`feature drop`](#feature-drop)).
- F14: duplicated knowledge moved into `KitStorePaths`, `FeatureTextFormat`, and `TextFileEncoding` ([Store additions](#store-additions)).
- F15: `list` skips folders whose names are not valid ids with the warning `feature-folder-invalid`.
- F16: the issue-number rule is stated precisely ([Id, title, and source rules](#id-title-and-source-rules)).
- F17: the frontmatter of the `shade-kit-drop` skill names the local override.
- F18: the guidance comment of the shipped `request.md` skeleton names the source kinds the engine writes (`text`, `url`, `file`); the agent may refine the reference when it completes the file.
- F19: the null-id test of `FeatureStoreTests` is a theory with four cases.

F03 (the standard output and error read tasks of `GitCommandRunner` are not observed after the process is killed on cancellation) was accepted as is by the user on 2026-10-21.

Exit code 130 (`canceled`) for a canceled run was set in the rework and confirmed by the user on 2026-10-21, together with the other decisions of the rework (override write failing after the move in `new` moves the feature back and deletes it; a failing delete after the rename in `drop` still clears the override; `set` and `new` replace an unusable override without a warning; a file at `features/<id>` stays exit code 255; an option class the parser cannot create stays 255).

The rework was reviewed a second time on 2026-10-21 ([second review report](../reviews/2026-10-21-feature-03-feature-management-uncommitted-2.md)): 0 Critical, 0 Major, 2 Minor, 1 Suggestion, and 4 Nitpick findings (N01–N07). It found F01, F02 (within the decided scope), and F04–F19 resolved; F03 stays accepted. The user decided on 2026-10-21:

- N01 (Minor), fixed: a feature folder that `new` cannot create or move into place, or that `drop` cannot rename, ends with exit code 35 (`feature-io-failed`) instead of 255 ([Errors](#errors)). Without a retry on Windows.
- N02 (Minor), fixed: on Unix, `git` is accepted only with an execute bit; otherwise the next `PATH` entry is checked ([Git access](#git-access)).
- N03 (Suggestion), N04, N05, N06, and N07 (Nitpicks): accepted as is ([open points](#open-points)).

After this second rework, the build has 0 warnings and `dotnet test` runs 2029 tests: 2028 passed, 1 skipped (a Windows-only test), 0 failed. The user waived a third review run on 2026-10-21.

The branch was reviewed against `main` on 2026-10-22: a `/code-review` reported 10 findings, which were verified in [the verification report](../reviews/2026-10-22-feature-03-feature-management-branch-verification.md). After the verification, 3 are Minor, 1 a Suggestion, and 4 Nitpicks; finding 9 is true but should not be acted on, and finding 1 was refuted: Spectre.Console already treats a redirected standard output as non-interactive, so `drop` does not ask. The user decided on 2026-10-22:

- Finding 3 (Minor), fixed: `set`, `show`, `drop`, the existence check of `new`, and the branch and override lookup match the folder name exactly ([Feature ids](#feature-ids)).
- Finding 2 (Minor), fixed: every id prefix in `trackers` is validated when `config.json` is read; a bad prefix makes the file invalid (exit code 22) ([Feature ids](#feature-ids); package 02, [`config.json`](02-repository-initialisation.md#configjson-v1)).
- Finding 7 (Minor), fixed: the heading of a `file` source skips fenced code blocks and a leading front matter block and strips a closing `#` sequence ([Id, title, and source rules](#id-title-and-source-rules)).
- Finding 10 (Nitpick), fixed: the JSON names of enum values have one entry point, the extension method `JsonEnumNames.ToJsonName()`; `CliJsonWriter.GetEnumName` is removed.
- Finding 5 (Nitpick), fixed: `new` checks the override through `ReadJsonAsync` with write access instead of a raw file read ([Order of work](#order-of-work-and-failures-for-new)).
- Finding 6 (Suggestion), fixed: `KitStorePaths.GetDisplayPath` builds the display path for the store and for the override check ([Store additions](#store-additions)).
- Finding 4 (Nitpick), documented: a concurrent `drop` of the same id can end with exit code 35 instead of 31 ([open points](#open-points)).
- Findings 8 (Nitpick) and 9, accepted as is: one 1 MiB read buffer per file source; the git identity is read with two parallel `git config` calls, because `git var GIT_AUTHOR_IDENT` would invent an identity from the account and host name and bypass exit code 36 (`git-identity-missing`).

After these fixes, the build has 0 warnings and `dotnet test` runs 2249 tests: 2248 passed, 1 skipped (a Windows-only test), 0 failed.

## Acceptance criteria

Every criterion is **Implemented**, FP03-AC11 only partly. The tests that prove it carry `[Trait("Requirement", "FP03-ACn")]`. Git is faked in every test; the real `git` process and the redirected-input path of `feature drop` were checked with the built CLI on macOS on 2026-10-21.

- **FP03-AC1** `feature new` with a GitHub issue URL and a title creates `gh-<number>-<slug>`. Proven by `FeatureIdFactoryTests`, `FeatureStoreCreateTests`, `FeatureNewCommandTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC2** `feature new` with plain text creates a time-based id `<yyyymmdd>-<hhmm>-<slug>`. Proven by `FeatureIdFactoryTests`, `FeatureStoreCreateTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC3** `feature new` with an id that exists already stops with an exit code and changes nothing. Proven by `FeatureStoreCreateTests`, `FeatureNewCommandTests`, `CliErrorMapperTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC4** After `feature new`, `feature current` returns the new feature even on a branch whose name matches no feature. Proven by `FeatureStoreCreateTests`, `CurrentFeatureResolverTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC5** On branch `feature/<id>` with an existing folder `<id>`, `feature current` returns `<id>`, also when the local override points to another feature; the output then contains a warning. Proven by `CurrentFeatureResolverTests`, `FeatureStoreTests`, `FeatureCurrentCommandTests`, `CliErrorMapperTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC6** Without a matching branch and without an override, `feature current --json` returns "not resolved" with exit code 0. Proven by `CurrentFeatureResolverTests`, `FeatureStoreTests`, `FeatureCurrentCommandTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC7** `feature set` with an unknown id stops with an exit code. Proven by `FeatureStoreTests`, `FeatureSetCommandTests`, `FeatureDropCommandTests`, `CliErrorMapperTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC8** `feature drop <id> --json` without `--yes` deletes nothing and returns what it would delete; with `--yes` it deletes the folder and clears an override that points to it. Proven by `FeatureStoreTests`, `FeatureDropCommandTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC9** `feature.json` records the git user as `createdBy`. Proven by `FeatureStoreCreateTests`, `FeatureShowCommandTests`, `FeatureCommandsEndToEndTests`.
- **FP03-AC10** `feature drop <id>` in plain mode with an interactive standard input and without `--yes` asks for a confirmation and deletes only after a yes; with redirected standard input it deletes nothing and does not wait for input. Proven by `FeatureDropCommandTests`, `FeatureCommandsEndToEndTests` (confirmation faked); the redirected-input path also with the built CLI on macOS on 2026-10-21.
- **FP03-AC11** `feature current --json` returns the feature `status`, and `done` after `phase complete` (package 04). **Implemented** in part: `active` after `feature new`, and a `done` in `feature.json` is returned. Proven by `FeatureStoreCreateTests`, `FeatureStoreTests`, `FeatureCurrentCommandTests`, `FeatureCommandsEndToEndTests`. "After `phase complete`" waits for package 04.

## Open points

- Trackers other than GitHub only work if the team adds them to `trackers` in `config.json`. Whether v1 needs URL patterns beyond the host (for example Jira keys) is **Open**.
- FP03-AC11 "`done` after `phase complete`" and the phase fields in `feature list`, `feature current`, and `feature show` wait for package 04.
- Known gaps and residual risks after the code review rework on 2026-10-21, all **Open**:
  - The process run of `GitCommandRunner` (output capture, exit codes, killing the process on cancellation) is not covered by tests, because no test starts the real `git` (decided); the lookup and the refusal of a relative path are tested. The process run was checked with the built CLI on macOS on 2026-10-21, before the rework.
  - After a kill on cancellation, the standard output and error read tasks of `GitCommandRunner` are not observed (F03, accepted by the user on 2026-10-21).
  - A FIFO given as `--source` is not detected; opening one without a writer blocks `feature new` ([File sources](#file-sources)).
  - `drop` deletes the override if it pointed to the dropped feature when it was read, even if it was changed to another feature between that check and the delete.
  - On some systems, the rename of `new` or `drop` can replace an empty folder that was created at the destination at exactly the wrong moment.
  - A file (not a folder) at `features/<id>` makes `feature new` fail with exit code 255 (unmapped IO error); the staging folder is removed.
  - A rename that fails only for a moment, for example on Windows while a virus scanner or search indexer holds the files that `new` has just written (**Unverified**, no Windows host), is not retried; `new` or `drop` ends with exit code 35 and can be run again.
- Findings of the second code review accepted as is by the user on 2026-10-21, not fixed:
  - N03: a title may contain Unicode format characters such as the bidi overrides and isolates (U+202A–U+202E, U+2066–U+2069, U+200E, U+200F, U+061C) and the line and paragraph separators U+2028 and U+2029, because the title check uses `char.IsControl` only. Such a title can show reversed text in the plain `list` and `show` output. The slug is not affected.
  - N04: [Cancellation](#cancellation) and the 130 row of [Errors](#errors) say that `init` rolls back, or removes, what it created in this run. `init` removes only the `.shadekit/` it created; a `.gitignore`, `CLAUDE.md`, or `AGENTS.md` written or changed before the cancellation stays, and the next `init` recognises it (package 02).
  - N05: not only Ctrl+C cancels a run with exit code 130: Ctrl+Break on Windows and `Ctrl+\` (SIGQUIT) on Unix do too, because the handler does not check the key.
  - N06: the `invalid-title` message ends with "Pass such a title with --title.", which is circular when the title came from `--title`.
  - N07: the test `ConsoleCancellationTests.Dispose_TokenTakenBefore_KeepsItsState` branches with `if (canceled)` in its Arrange section.
  - Unknown extra arguments are ignored silently: no options class sets `AllArgsMustMatch`, so `NotAllArgumentsMatchException` (exit code 2) is not raised today.
  - `git` is found only as `git.exe` (Windows) or `git` (Unix) in an absolute `PATH` entry; `PATHEXT` is not used, so a `git.cmd` shim is not found.
  - Package 09 replaces the `request.md` skeleton and its guidance comment.
- Residual risk accepted by the user on 2026-10-22 (code review verification, finding 4), not fixed:
  - When two `drop` runs for the same id run at the same time, both pass the existence check. The second one's rename of `features/<id>/` to its hidden dropping folder fails with `DirectoryNotFoundException`, so it ends with exit code 35 (`feature-io-failed`), whose message points to the write permissions of `features/` and to programs that hold a file open, instead of exit code 31 (`feature-not-found`). Nothing is lost: the first run drops the feature, and the second changes nothing.
