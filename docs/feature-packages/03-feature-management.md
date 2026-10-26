# 03 · Feature management

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (`feature drop` confirmation, `status` in `feature current`, `feature list` implemented), 2026-10-21 (implementation state; implementation clarification of 2026-10-20; code review rework: `git` only from absolute `PATH` entries, exit codes 2 and 130, rules for file sources and titles, atomic `new` and `drop`, folder name as id, unusable override, `overrideAffected`, Ctrl+C, warning codes; second code review: exit code 35 `feature-io-failed` for a feature folder that cannot be created or renamed, execute bit for `git` on Unix), 2026-10-22 (code review verification: exact folder name match, tracker prefix validation, heading detection, override check through the format layer, shared display path, concurrent `drop` as open point), 2026-10-23 (rework of file content handling: line endings of `request.md` and the JSON files, `editorconfig-unreadable` and `editorconfig-invalid` warnings), and 2026-10-24 (documentation check: git access for line endings, write warnings in the warnings table, `request.md` template that is not UTF-8 text, reused code 14), and 2026-10-25 (rework of `feature new`: no `request.md` template, `request.md` holds the request content that the engine loads, tracker issues read with `gh`, other URLs read with an HTTP GET, `--title` optional for tracker URLs, the text of a text source in `source.value`, `Source: text` in `show`; code review of the rework: git identity and override checked before the request content is loaded, exit-33 hints by cause, one-line issue heading and no trailing line breaks of issue and comment bodies in `request.md`; simplification of the rework: `FeatureSourceReader` loads the content, the URL reader in `Core.Web`, one tool runner and one locator base for `git` and `gh`, exit-33 hints by `FeatureSourceProblem`), and 2026-10-26 (code review of the branch: a URL followed by words is text, redirects followed by the HTTP client, `utf8` and `us-ascii` charsets, size limit of an issue, neutral `gh` hint, no fallback mapping of the reader exceptions). Implemented except the phase fields and the approval history (package 04; FP03-AC11 "`done` after `phase complete`" waits for it) and trackers beyond GitHub (**Open**). |
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
- Loading the request content of `feature new`: the issue of a tracker URL through the GitHub CLI `gh`, the content of any other URL through an HTTP GET, and the content of a file. **Decided** on 2026-10-25 (rework of `feature new`), replacing "fetching issue content from a tracker is out of scope; the agent does that in the `new` skill (package 09)".

### Out of scope

- Phases, phase states, approvals, and history in `feature.json` (package 04). `feature new` sets the first phase through package 04's rules once that package exists.
- Tracker access other than through `gh`, for example a tracker API library ([open points](#open-points)).
- The separate spec store (later, concept §4.4).

## Functional description

**Implemented** in the Core namespaces `CreativeCoders.ShadeKit.Core.Features` (`IFeatureStore` / `FeatureStore`, `ICurrentFeatureResolver` / `CurrentFeatureResolver`, `IFeatureIdFactory` / `FeatureIdFactory`, `IFeatureSourceReader` / `FeatureSourceReader`), `CreativeCoders.ShadeKit.Core.Git`, `CreativeCoders.ShadeKit.Core.Trackers` (`ITrackerIssueReader` / `GitHubCliIssueReader`, `IGhCommandRunner` / `GhCommandRunner`, `IGhExecutableLocator` / `GhExecutableLocator`), and `CreativeCoders.ShadeKit.Core.Web` (`IUrlContentReader` / `HttpUrlContentReader`), and in the CLI commands under `Commands/Feature/`. All services are singletons registered by `AddShadeKitCore()`, together with `TimeProvider.System` and the named HTTP client of `HttpUrlContentReader` ([Other URLs](#other-urls-http-get)).

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

- Source kind of `--source` (one value): an absolute `http` or `https` URI is `url`, unless the source has whitespace between its leading and trailing whitespace: `https://example.com/login is broken` is `text`, and it sends no request (**Decided** on 2026-10-26, code review of the branch, finding 1; before, `Uri` escaped the spaces and the text was read as a URL); otherwise a path (relative to the working directory, or absolute) for which `File.Exists` is true is `file`; everything else, including a directory or a missing path, is `text`. An empty or whitespace-only source stops with exit code 33 (`invalid-source`). **Decided** on 2026-10-21 (code review rework) for `File.Exists`: on Unix it also counts devices, FIFOs, sockets, and symbolic links whose target is missing as files; only documented .NET APIs are used, so they are not told apart.
- Issue-based id: the URL host is matched against the keys of `trackers` case-insensitively and exactly (no `www.` stripping). The issue number is the last path segment, which must consist only of ASCII digits (`/issues/142`, `/pull/142`, `/-/issues/142`; `/issues/142/comments` has no issue number); query and fragment are ignored. A host in `trackers` whose last path segment is not an issue number stops with exit code 33, before `gh` runs. The issue of a host in `trackers` is read with `gh` ([Tracker issues](#tracker-issues-gh)). A host not in `trackers` gives a time-based id and its content is read with an HTTP GET ([Other URLs](#other-urls-http-get)); the source kind stays `url`.
- Title: `--title` wins. Without it: `text` → the first non-empty line, trimmed; `file` → the first non-empty level-1 Markdown heading (`# …`), otherwise the file name without extension; `url` whose host is in `trackers` → the title of the issue, trimmed; any other `url` → no title can be derived, so `--title` is required (exit code 34, `invalid-title`). **Decided** on 2026-10-25 (rework of `feature new`), replacing "`url` → `--title` is required" for tracker URLs. **Implemented** (`FeatureIdFactory`, `CreateAsync_TrackerUrlWithoutTitle_UsesTrimmedIssueTitle`, `CreateAsync_TrackerUrlWithTitle_UsesTitleOverIssueTitle`, `CreateAsync_WebUrlWithoutTitle_ThrowsInvalidFeatureTitleExceptionWithoutReadingUrl`).
- A title passed with `--title` is checked (control characters, empty slug) before `gh` runs or the HTTP GET is sent, so a title that cannot be used costs no `gh` call and no request; for any other URL, a missing `--title` is reported before the request too. The issue title is checked after the read with the same rules, and the message names it as "taken from the issue title". **Implemented** (`CreateAsync_TrackerUrlWithUnusableTitle_ThrowsInvalidFeatureTitleExceptionWithoutReadingIssue`, `CreateAsync_WebUrlWithMissingOrUnusableTitle_ThrowsInvalidFeatureTitleExceptionWithoutReadingUrl`, `CreateAsync_IssueTitleWithControlCharacter_ThrowsInvalidFeatureTitleExceptionNamingIssueTitle`, `CreateAsync_IssueTitleWithoutSlug_ThrowsInvalidFeatureTitleException`).
- Heading of a `file` source: a line indented by at most 3 spaces that starts with `#` and a space or tab, as a CommonMark ATX heading. Lines in fenced code blocks (```` ``` ```` or `~~~`, closed by a fence of the same character at least as long) are skipped; a fence that is never closed runs to the end of the file, so no heading after it counts. A leading front matter block is skipped: it starts with a first line `---` and ends with the next line `---` or `...` (trailing whitespace allowed); without such an end line the file has no front matter. A closing sequence of `#` preceded by a space or tab is not part of the title: `# Login with SSO #` → `Login with SSO`, while `# C#` stays `C#`; a heading of only a closing sequence (`# #`) is empty and skipped. **Decided** on 2026-10-22 (code review verification, finding 7); before, `# ` lines in code blocks and front matter became the title and a closing `#` stayed in it. **Implemented** (`FeatureIdFactory`, `MarkdownCodeFences`).
- Every title, from `--title` after trimming, the first line, the heading, or the file name, must not contain a control character (`char.IsControl`, including CR, LF, and TAB); otherwise exit code 34. The message names the character and the origin of the title. **Decided** on 2026-10-21 (code review rework). **Implemented** (`FeatureIdFactory`).
- Slug: diacritics are removed (`ä` → `a`), the text is lower-cased, every run of characters other than `a-z` and `0-9` becomes one `-`, and leading and trailing `-` are removed. A slug longer than 40 characters is cut at the last `-` within the limit, or after 40 characters if there is none. A title that gives an empty slug stops with exit code 34; the message points to `--title`.
- Profile: `--profile`, else `defaultProfile` from `config.json` (the defaults apply if `config.json` is missing). It must be `feature`, `bugfix`, or `quick`, else exit code 40 (`unknown-profile`). **Implemented** (`WorkflowProfiles`, `UnknownProfileException`). Package 04 owns the profiles; until it exists, this package validates the names.

### File sources

**Decided** on 2026-10-21 (code review rework, with the user's correction of the same day: no internal runtime API, a size limit instead of a file type check). **Implemented** (`FeatureIdFactory`, `TextFileEncoding`). Every check runs before anything is written, and every failure stops with exit code 33 (`invalid-source`) and a message with the reason.

- Size: at most `FeatureSourceReader.MaxSourceLength`, 1 MiB (1,048,576 bytes), counted on the raw bytes including a UTF-8 byte order mark. The file is read through a stream, at most the limit plus one byte, without trusting the reported length, so a larger file or an endless device such as `/dev/zero` is refused without being read completely. `/dev/null` is an empty file source (title `null`).
- Read errors: a file that cannot be opened or read (`IOException`, `UnauthorizedAccessException`, for example missing permissions, a lock, a symbolic link whose target is missing, a link loop, or a socket) stops with "The file cannot be read: …".
- Encoding: UTF-8 with or without byte order mark is accepted. A UTF-16 or UTF-32 byte order mark, a NUL byte (UTF-16 or UTF-32 without byte order mark, or a binary file), or bytes that are not valid UTF-8 stop the command. The detection is shared with package 02 (`Files/TextFileEncoding`, also used by `TextFileContent`, package 02).
- Residual risk: a FIFO (named pipe) is not detected. Opening a FIFO that no process writes to blocks, so `feature new` waits until a writer opens it.

### Feature folder

`.shadekit/features/<feature-id>/` with:

- `feature.json`: `schemaVersion`, `minEngineVersion`, `id`, `title`, `profile`, `source` (kind and value), `createdAt`, `createdBy`, `status`. Package 04 adds the phase fields. **Implemented** (`FeatureDocument`, `FeatureSource`, `FeatureCreator`, `FeatureStatus`; format id `feature`, version 1).
- `request.md`: the request content that the engine loaded from the source, without a template. **Decided** on 2026-10-25 (rework of `feature new`), replacing "created from the `request.md` template; the engine fills the placeholders it owns, the `new` skill completes the file" of 2026-10-20. **Implemented** (`FeatureIdFactory`, `FeatureStore`); details in [`request.md`](#requestmd).

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

- `source.kind` is `text`, `url`, or `file`. `source.value` is the URL as given for `url`; for `file` the path relative to the repository root with `/` as separator, or the absolute path if the file is outside the repository; for `text` the text exactly as passed with `--source`, not trimmed. **Decided** on 2026-10-25 (rework of `feature new`), replacing "for `text` it is absent". The format stays version 1 without a migration: a `text` source is read with or without `value`, so features created before keep working. **Implemented** (`FeatureSource`, `FeatureIdFactory`, `CreateAsync_TextSource_WritesTextUnchangedAsRequestAndAsSourceValue`).
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

**Decided** on 2026-10-25 (rework of `feature new`, approved by the user), replacing the template filling **Decided** on 2026-10-20 (implementation clarification: the engine filled the placeholders `{{feature-title}}`, `{{feature-id}}`, `{{source kind and source reference}}`, `{{original input, unchanged}}`, and `{{created at and created by}}` of the `request.md` template, and the `new` skill completed the file) and the rule for a directory at the repository template **Decided** on 2026-10-24 (exit code 14). **Implemented** (`FeatureIdFactory`, `FeatureIdentity.RequestContent`, `FeatureStore`, `TrackerIssueMarkdown`).

- There is no `request.md` template. `request.template.md`, `FeatureRequestTemplate`, and `ShippedTemplates.Request` are removed; `init` no longer writes `.shadekit/templates/request.md` (package 02). A `.shadekit/templates/request.md` that an older version wrote is neither read nor changed by `new`, also when it is a directory or not UTF-8 text, so it no longer causes exit code 14 or 22. **Implemented** (`CreateAsync_RepositoryRequestTemplateExists_IgnoresAndKeepsIt`, `CreateAsync_RequestTemplatePathIsDirectory_IgnoresItAndCreatesFeature`, `CreateAsync_RepositoryRequestTemplateIsNotUtf8_IgnoresIt`, `InitializeAsync_NewRepository_WritesNoRequestTemplate`, `All_Catalog_ContainsNoRequestTemplate`). Removing the orphaned file is an open point of package 18.
- `request.md` always exists, so `paths.request` is always set. Its content per source kind:

  | Source | `request.md` |
  |---|---|
  | `text` | The text exactly as passed. |
  | `file` | The file content, decoded as UTF-8 without its byte order mark ([File sources](#file-sources)). |
  | `url` whose host is in `trackers` | The issue read with `gh`, formatted as Markdown ([Tracker issues](#tracker-issues-gh)). |
  | any other `url` | The body of an HTTP GET, decoded as UTF-8 without its byte order mark ([Other URLs](#other-urls-http-get)). |

  **Implemented** (`CreateAsync_TextSource_WritesTextUnchangedAsRequestAndAsSourceValue`, `CreateAsync_FileSource_WritesFileContentAsRequestWithResolvedLineEnding`, `CreateAsync_FileSourceWithBom_WritesRequestWithoutBom`, `CreateAsync_EmptyFileSource_WritesEmptyRequest`, `CreateAsync_TrackerUrlWithoutTitle_WritesIssueAsRequestAndUsesIssueTitle`, `CreateAsync_WebUrl_WritesUrlContentAsRequest`).
- The content is written unchanged except for its line endings; text that looks like a placeholder, such as `{{feature-id}}`, stays as it is (`CreateAsync_PlaceholdersInRequestContent_KeepsThemUnchanged`). No final line ending is added: a one-line text gives a file without a final line ending, and a content that ends with a line ending keeps one. The Markdown of a tracker issue ends with one line ending, or is empty for an issue without title, body, and comments ([Tracker issues](#tracker-issues-gh)).
- All content is loaded by `FeatureIdFactory` before anything is written ([Order of work](#order-of-work-and-failures-for-new)), so a source that cannot be loaded leaves no feature behind.
- Written as UTF-8 without byte order mark.
- Line endings: `request.md`, `feature.json`, and `current-feature.json` are written as a whole with the line ending decided for each file when it is written (git attributes and configuration for the path, then `.editorconfig`, then the operating system; package 02), so the request content in `request.md` takes that line ending too. If an `.editorconfig` file cannot be read or evaluated, the `.editorconfig` files do not decide and the warning `editorconfig-unreadable` or `editorconfig-invalid` (package 02) is reported, which `new` and `set` report once, after the format and feature warnings. **Decided** on 2026-10-23 (rework of file content handling), replacing "the original input keeps its line endings". **Implemented** (`FeatureStore`, `IKitStore.WriteFileAsync` / `WriteJsonAsync` through `ITextFileWriter`, `CreateAsync_FileSource_WritesFileContentAsRequestWithResolvedLineEnding`, `CreateAsync_RequestContentLineEndings_WritesRequestWithResolvedLineEnding`, `CreateAsync_StoreWritesWithWarnings_ReturnsFormatWarningsFirstAndEachStoreWarningOnce`, `SetCurrentAsync_StoreWritesWithWarnings_ReturnsMismatchWarningFirstAndEachStoreWarningOnce`). `feature.json` and `request.md` are written into the hidden staging folder, but their line ending is decided for their final path `.shadekit/features/<id>/...`, so git attributes and `.editorconfig` sections for the feature folder apply; a result carries only the warnings of its own writes, not those of earlier operations on the same store. **Decided** on 2026-10-23 (code review of the rework). **Implemented** (`IKitStore.CreateDirectoryWriter`, which `CreateDirectoryExclusiveAsync` passes to the folder content, and `lineEndingFile` of `ITextFileWriter.WriteAsync`, `CreateAsync_NewFeature_ResolvesLineEndingOfEachFileForItsFinalPathNotTheStagingPath`, `CreateAsync_FinalFeaturePathPrescribesOtherLineEnding_WritesFeatureFilesWithLineEndingOfFinalPath`, `CreateAsync_StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation`, `SetCurrentAsync_StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation`). A lone CR in the request content ends a line as well, so `request.md` never has mixed line endings; other Unicode line separators stay part of a line. `new` reads the git attributes of the three files (`feature.json` and `request.md` for their final path, and `current-feature.json`) with one `git check-attr` before the first write (`IKitStore.PrepareLineEndingsAsync`). **Decided** on 2026-10-24 (code review of the branch, findings 3 and 7). **Implemented** (`TextFileContent`, `ReadAsync_LoneCarriageReturn_EndsLine`, `CreateAsync_NewFeature_PreparesLineEndingsOfAllFinalPathsOnceBeforeFirstWrite`).

### Tracker issues (`gh`)

**Decided** on 2026-10-25 (rework of `feature new`). **Implemented** (`GitHubCliIssueReader`, `GhCommandRunner`, `GhExecutableLocator`, `TrackerIssueMarkdown`, `GitHubCliIssueReaderTests`, `GhCommandRunnerTests`, `GhExecutableLocatorTests`, `TrackerIssueMarkdownTests`).

- Every host in `trackers` is read with the GitHub CLI `gh`, `github.com` and GitHub Enterprise Server hosts alike; `gh` uses its own authentication for the host. There is no other tracker reader in v1 ([open points](#open-points)).
- The factory reads the issue through `FeatureSourceReader`, which sees only `ITrackerIssueReader`; it returns a provider-neutral `TrackerIssue` (title, body, and comments with author, creation time, and body). Everything specific to `gh` and its JSON is in `GitHubCliIssueReader`, so another reader, for example one built on a tracker API library, can replace it without a change to `FeatureIdFactory`, `FeatureSourceReader`, or `FeatureStore`.
- Command: `gh issue view <url> --json title,body,comments`, with the URL as an absolute, escaped URI (`Uri.AbsoluteUri`), so it can never be read as an option. `gh` runs without a shell in the repository root, inherits the environment, and gets `GH_PROMPT_DISABLED=1`, `GH_NO_UPDATE_NOTIFIER=1`, and `NO_COLOR=1`, so it never waits for input, checks for updates, or writes color codes. Its output is read completely, without a size limit. `gh` is located like `git` ([Git and gh access](#git-and-gh-access)).
- A non-zero exit code of `gh` is a failure whose reason names the exit code and the trimmed standard error. Text on standard error with exit code 0 is ignored.
- The output must be a JSON object with a string `title`. A missing or `null` `body` is an empty body; missing or `null` `comments` are no comments; otherwise `comments` is an array of objects, each with a `createdAt` string in ISO 8601 format (a time without an offset is taken as UTC). A comment `body` follows the rule of the issue body. A comment whose `author` or `author.login` is missing, `null`, empty, or whitespace gets the author `ghost`, the name GitHub shows for deleted accounts. Unknown properties are ignored; any value of an unexpected type, or output that is not valid JSON, is a failure.
- `request.md` of the issue:

  ```markdown
  # <title>

  <body>

  ## Comments

  ### <author login> · <createdAt>

  <comment body>
  ```

  The heading is one line: in the title as `gh` returned it, every run of control characters (`char.IsControl`, tab, CR, and LF included) becomes one space, and leading and trailing whitespace is removed. A title that is empty after this is left out together with its heading line and the empty line after it; that can only happen with `--title`, because without it an empty issue title gives an empty slug (exit code 34). The feature title is the trimmed issue title, or `--title`. `## Comments` appears only if the issue has comments, with one `###` block per comment in the order `gh` returned them. `<createdAt>` is formatted as in `show` (`FeatureTextFormat.FormatCreatedAt`, `yyyy-MM-ddTHH:mm:sszzz`) with the offset from the `gh` output. Blocks are separated by exactly one empty line; an issue or comment body that is empty or only whitespace is left out together with its empty line, and of any other body only the trailing CR and LF characters are removed, so it adds no empty lines; the body is unchanged otherwise. The text ends with exactly one line ending; an issue without title, body, and comments gives an empty text. **Decided** on 2026-10-25 (code review of the rework, findings F4 and F5), replacing "the heading holds the title as `gh` returned it" and "any other body is kept unchanged", because a line break or tab in the title broke the heading and trailing line breaks of a body added empty lines between the blocks and at the end. **Implemented** (`TrackerIssueMarkdown`, `Format_TitleWithControlCharactersOrSurroundingWhitespace_WritesOneLineHeading`, `Format_EmptyTitleAfterCleanup_LeavesOutHeadingLine`, `Format_EmptyTitleAndBodyWithoutComments_ReturnsEmptyText`, `Format_BodyWithMarkdownAndLineEndings_KeepsBodyUnchangedExceptTrailingLineBreaks`, `Format_BodyEndingInLineBreaksFollowedByComments_SeparatesBlocksByOneEmptyLine`, `Format_CommentBodiesEndingInLineBreaks_SeparatesBlocksByOneEmptyLineAndEndsWithOneLineFeed`).
- The Markdown of the issue (the content of `request.md`) may have at most `FeatureSourceReader.MaxSourceLength`, 1 MiB (1,048,576 bytes), counted in UTF-8, the limit of a file or URL source; a larger issue stops `new` with exit code 33 and the reason `The issue is larger than 1,048,576 bytes (1 MiB) as Markdown, the size limit of a request. Save a shortened copy of the issue to a file, and pass the path of the file with --source.` The output of `gh` is read without a limit. **Decided** on 2026-10-26 (code review of the branch, finding 4).
- Every failure, `gh` not found or not startable included, stops `new` with exit code 33 ([Errors](#errors)). A cancellation (Ctrl+C) kills `gh` and its descendants and ends with exit code 130; it is not reported as a failure of the source.

### Other URLs (HTTP GET)

**Decided** on 2026-10-25 (rework of `feature new`). **Implemented** (`HttpUrlContentReader`, `AddShadeKitCore`, `HttpUrlContentReaderTests`, `ShadeKitCoreServiceCollectionExtensionsTests`).

- A URL whose host is not in `trackers` needs `--title`, which is checked before the request is sent.
- The content is read with one HTTP GET through the named client `HttpUrlContentReader.HttpClientName` of `IHttpClientFactory` (package `Microsoft.Extensions.Http`), which the reader creates for every read. `AddShadeKitCore()` registers the client with a timeout of 30 seconds (`HttpUrlContentReader.RequestTimeout`), the header `User-Agent: shade-kit/<engine version>` (only `shade-kit` if the version is empty or not a valid product token; added only when the client has no `User-Agent` yet, so a repeated `AddShadeKitCore()` sends it once). The client keeps the default primary handler of `IHttpClientFactory`; a primary handler configured for the named client before or after `AddShadeKitCore()` is kept. The 30 seconds limit the whole read, redirects and body included.
- Redirects are followed by the primary handler of the client (`AllowAutoRedirect` and `MaxAutomaticRedirections` of the handler, by default on and 50), not by the reader; the reader sends one request. A redirect status code (301, 302, 303, 307, 308) that reaches the reader is a redirect the handler did not follow and a failure whose reason names why: without a valid `Location`, to a scheme other than `http` or `https`, from `https` to `http`, or otherwise (`… and the redirect was not followed, for example because the server redirected too many times.`). A relative `Location` is resolved against the URL of the last request. **Decided** on 2026-10-26 by the user (code review of the branch, finding 2), replacing the redirects that the reader followed itself, at most 10, through a primary `SocketsHttpHandler` with `AllowAutoRedirect = false`; the changed limit and that the refusal of a redirect from `https` to `http` now depends on the handler are accepted.
- Only a success status code (2xx) is accepted; any other status code is a failure that names it.
- The content type must be `text/*`, `application/json`, or `application/xml`, with no `charset` or one of the `charset` labels `utf-8`, `utf8`, and `us-ascii` (case-insensitive; US-ASCII text decodes as UTF-8; **Decided** on 2026-10-26, code review of the branch, finding 8, before only `utf-8`); the reason of another label is `The response has the charset <label>; only utf-8, utf8, and us-ascii can be read.` A missing content type (also of a 204 response) or any other content type or `charset` is a failure.
- The body is read as a stream up to the limit that `FeatureSourceReader` passes, `FeatureSourceReader.MaxSourceLength` (1 MiB), plus one byte, without trusting `Content-Length`; a larger body is a failure with the reason `The response body is larger than 1,048,576 bytes.`. File and URL sources share the bounded read (`LimitedUtf8Reader`), which reads in chunks of 80 KiB, so a short body allocates no large buffer. It is decoded strictly as UTF-8 like a file source (`TextFileEncoding.DecodeUtf8`): a UTF-8 byte order mark is removed; a UTF-16 or UTF-32 byte order mark, a NUL byte, or bytes that are not valid UTF-8 are a failure. An empty body gives an empty `request.md`.
- Every failure, a request that fails (`HttpRequestException`, `IOException`) and the timeout included, stops `new` with exit code 33 ([Errors](#errors)). A cancellation (Ctrl+C) ends with exit code 130.

### Commands

| Command | Behaviour |
|---|---|
| `feature new --source <text\|url\|file> [--profile <p>] [--title <t>] [--json]` | Creates the folder, `feature.json`, and `request.md`, staged in a hidden folder and moved into place in one step. The profile defaults to `defaultProfile` from `config.json`. A URL whose host is in `trackers` gives an issue-based id; the number comes from the URL, the slug from `--title` or else from the issue title; the issue is read with `gh`. Any other source gives a time-based id; any other URL needs `--title`, and its content is read with an HTTP GET. `request.md` holds the text, the file content, the issue as Markdown, or the content of the URL ([`request.md`](#requestmd)); all of it is loaded before anything is written. Writes the local override so the new feature is current right away. Returns the id, paths, and suggested branch. **Proposed** details. **Implemented** (`FeatureNewCommand`, `FeatureStore.CreateAsync`); short options `-s`, `-p`, `-t`. |
| `feature list [--json]` | Lists features with id, title, profile, status, and (once package 04 exists) the current phase. Folders in ordinal order. Folders whose names start with `.` are ignored; a folder whose name is not a valid id is skipped with the warning `feature-folder-invalid`; a folder without `feature.json` is skipped with the warning `feature-file-missing`; an invalid or too new `feature.json` stops the command (exit code 22 or 20, package 01). **Implemented** (`FeatureListCommand`, `FeatureStore.ListAsync`). |
| `feature current [--json]` | Returns the current feature: id, title, profile, feature `status` (`active` or `done`), where it was resolved from, artifact paths, and (once package 04 exists) current phase and phase status. If none can be resolved, returns "not resolved" as a result with exit code 0, not as an error (package 01 contract). **Implemented** (`FeatureCurrentCommand`, `FeatureStore.GetCurrentAsync`). |
| `feature set <id> [--json]` | Writes `features/current-feature.json`. Stops if the id is invalid (exit code 32) or does not exist (exit code 31). Warns with `current-feature-mismatch` if the current branch selects another feature, because the branch wins. The branch is read before the override is written, so a failing git leaves the override unchanged. **Implemented** (`FeatureSetCommand`, `FeatureStore.SetCurrentAsync`). |
| `feature show <id> [--json]` | Returns the complete state of a feature: id, title, profile, status, source, created at and by, paths, and (once package 04 exists) phases and the approval history. Needed by the report in package 15. **Proposed.** **Implemented** without the package 04 fields (`FeatureShowCommand`, `FeatureStore.ShowAsync`). |
| `feature drop <id> [--yes] [--json]` | Deletes the feature folder and clears the local override if it points to this feature. Possible in every phase. With `--yes` it deletes at once. Without `--yes` it first describes what it would delete, including whether the override would be cleared (`overrideAffected`); then, only in plain mode (no `--json`) with an interactive standard input, it asks for a confirmation in the console and deletes after a yes. In JSON mode or with redirected standard input it stops after the description and deletes nothing, so an agent can never hang on the question. An agent calls it with `--json`: first without `--yes` to get the description it shows the human, and after the human's yes in the conversation with `--yes`. **Decided** on 2026-10-18. Every mode without `--yes` ends with exit code 0; the question defaults to no. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`FeatureDropCommand`, `FeatureStore.PreviewDropAsync` / `DropAsync`, `IConsoleConfirmation` / `ConsoleConfirmation`); short option `-y`. |

`show` and `current` on a feature folder without `feature.json` stop with exit code 22 (`invalid-file`), with a reason that names the missing file. **Decided** on 2026-10-20 (implementation clarification). `drop` works with a missing or broken `feature.json`, so such a folder can always be removed. **Implemented** (`FeatureStore`). `show` reports feature warnings (`feature-id-mismatch`) like `list` and `current`. **Decided** on 2026-10-21 (code review rework). **Implemented** (`FeatureShowResult`).

### Order of work and failures for `new`

**Decided** on 2026-10-20 (implementation clarification); staging, the atomic move, and the rollback of the override write **Decided** on 2026-10-21 (code review rework), because two concurrent runs with the same id both passed the existence check and the rollback of one could delete the feature the other had written. **Implemented** (`FeatureStore.CreateAsync`, `IKitStore.MoveDirectory`).

1. Read `config.json`; check the profile (exit code 40).
2. Read an existing `current-feature.json` for writing: its format is checked without migration, and `config.json` is not read. An override that cannot be used because of its own format (invalid, too new, or outdated) is replaced in step 7, so no format problem stops `new` here. Only an override that cannot be read at all (an IO error or access denied) stops now; that failure is not mapped and ends with exit code 255 (`host-error`). **Decided** on 2026-10-22 (code review verification, finding 5). **Implemented** (`FeatureStore.CreateAsync`).
3. Read the git identity (exit codes 36, 16).
4. Source, title, and id, and the request content: the file is read, the issue of a tracker URL is read with `gh`, or the content of any other URL is read with an HTTP GET (exit codes 33, 34). For a tracker URL, the issue number and a `--title` are checked before `gh` runs; for any other URL, `--title` is checked before the request. **Decided** on 2026-10-25 (rework of `feature new`): every content is loaded here, before anything is written, so a source that cannot be loaded leaves no feature behind. **Implemented** (`FeatureIdFactory`, `CreateAsync_IssueCannotBeRead_ThrowsInvalidFeatureSourceExceptionAndChangesNothing`, `CreateAsync_GhNotFound_ThrowsInvalidFeatureSourceExceptionAndChangesNothing`, `CreateAsync_UrlContentCannotBeRead_ThrowsInvalidFeatureSourceExceptionAndChangesNothing`, `CreateAsync_IssueReadCanceled_RethrowsOperationCanceledExceptionAndChangesNothing`).
5. The feature folder must not exist (exit code 30). The id can depend on the loaded content (the issue title without `--title`), so this check stays after step 4, and `new` for an id that exists already still runs `gh` or sends the request once.
6. Create the hidden staging folder `features/.<id>.<8 hex digits>.tmp/`, write `feature.json`, then `request.md` into it, and move it to `features/<id>/` in a single rename that refuses an existing file or folder (residual risk in the [open points](#open-points)). If the destination appeared in the meantime, for example from a concurrent run, `new` stops with exit code 30 and leaves that folder untouched, also when the move failed with access denied. If the staging folder cannot be created or the move fails with an IO error or access denied for another reason (for example a `features/` folder that may not be written, or a file in the staging folder that another program holds open), `new` stops with exit code 35 (`feature-io-failed`); see below.
7. Write the override.

Steps 2 and 3 do not depend on the source, so they run before the content is loaded: an unreadable override or a missing git identity stops `new` before `gh` runs or a request is sent, and a missing git identity (exit code 36) wins over a source or title that cannot be used (exit codes 33, 34). **Decided** on 2026-10-25 (code review of the rework, finding F7), replacing the order of the rework (source, title, id, and content as step 2, then the existence check, the override, and the git identity), in which `new` in a repository without `user.name` or `user.email` still ran `gh` or sent the request, up to 30 seconds, before it failed. **Implemented** (`FeatureStore.CreateAsync`, `CreateAsync_GitIdentityMissing_ThrowsBeforeSourceContentIsRead`, `CreateAsync_GitIdentityMissingAndSourceUnusable_ThrowsGitIdentityNotConfiguredException`, `CreateAsync_OverrideCannotBeRead_ThrowsBeforeSourceContentIsRead`).

A failure before step 6 changes nothing. If step 6 fails or `new` is canceled, `new` deletes only its own staging folder (best effort) and fails. A failure to create the staging folder or to move it into place ends with exit code 35 (`feature-io-failed`): nothing was changed, the staging folder is removed, and the override is untouched; the message names the folder, the operation (`create`), and the cause, and points to the write permissions of `features/` and to programs that hold files open. A write failure inside the staging folder (`feature.json` or `request.md`) is not mapped and ends with exit code 255 (`host-error`). **Decided** by the user on 2026-10-21 (second code review, finding N01); before, these failures ended with exit code 255 and a stack trace. There is no retry, also not on Windows. **Implemented** (`FeatureIoFailedException`, `FeatureStore.CreateAsync`). If step 7 fails or `new` is canceled during it, `new` moves the feature back to a hidden folder and deletes it (best effort), so the override never points to a removed feature; if even the move back fails, the complete feature stays without override. The feature folder did not exist before, so nothing is lost and the id can be used again. A staging folder left behind starts with `.`, so it is ignored and does not block the id.

A file (not a folder) at `features/<id>` passes step 5 and makes the move in step 6 fail with an IO error that is not mapped (exit code 255, `host-error`); the staging folder is removed.

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

### Git and gh access

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`IGitCommandRunner` / `GitCommandRunner`, `IGitRepositoryInfo` / `GitRepositoryInfo`, `IGitExecutableLocator` / `GitExecutableLocator`).

The `PATH` search and the process run are shared by `git` and `gh` since 2026-10-25 (rework of `feature new`): the internal helpers `Processes/PathExecutableSearch` (absolute `PATH` entries only, Windows quoting, Unix execute bit) and `Processes/ExternalProcessRunner` (absolute executable path, no shell, arguments passed one by one, standard output and error read concurrently as UTF-8, the process tree killed on cancellation) were extracted from `GitExecutableLocator` and `GitCommandRunner`; the public git interfaces and their behaviour are unchanged. **Implemented** (`PathExecutableSearch`, `ExternalProcessRunner`, `PathExecutableSearchTests`; the existing git tests unchanged).

Since the simplification of the rework (2026-10-25, approved by the user), the layer above is shared as well: `Processes/ExternalToolRunner` locates the executable, refuses a missing or relative path, starts it, and turns a start failure into the exception of the tool; `GitCommandRunner` and `GhCommandRunner` only pass their `ExternalTool` description. Both runner interfaces return the public `ExternalProcessResult`, whose `CreateUnexpectedExitCodeReason` builds the reason `<tool> exited with the unexpected exit code <n>[: <standard error>.]` for both tools. `GitExecutableLocator` and `GhExecutableLocator` derive from `PathExecutableLocator`, which caches the first path found, and `GitUnavailableException` and `GhUnavailableException` derive from `ExternalToolUnavailableException`, which formats the command text. Changed texts: the reason of a relative `git` path starts with `The git executable path`, a `git` start failure is `The git executable '<path>' could not be started: <message>.`, the standard error in the reason of an unexpected `git` exit code ends with a period, and the reason of a failing `gh` is `gh exited with the unexpected exit code <n>[: <standard error>.]` instead of `The gh command exited with the exit code <n>…`. The tests of the `PATH` rules are in `PathExecutableSearchTests`; the locator tests cover only the executable names and the caching. **Implemented** (`ExternalToolRunner`, `ExternalTool`, `PathExecutableLocator`, `ExternalToolUnavailableException`, `ExternalProcessResult`, `PathExecutableSearchTests`, `GitExecutableLocatorTests`, `GhExecutableLocatorTests`).

- The engine starts the `git` executable in the repository root; there is no git library.
- The executable is resolved only from absolute `PATH` entries; empty and relative entries are skipped, and the first entry that contains the file wins. On Unix the file must also have at least one execute bit (owner, group, or others), like in the `PATH` search of a shell; a file without one is skipped and the next entry is checked (**Decided** on 2026-10-21, second code review, finding N02; before, a non-executable `git` in an earlier entry stopped the command with exit code 16). Windows: separator `;`, file `git.exe`, double-quoted entries supported; Unix: separator `:`, file `git`. `PATHEXT` is not used, so a `git.cmd` shim is not found. `GitCommandRunner` starts the resolved absolute path and refuses a relative one. If no entry contains a usable `git`, the command stops with exit code 16. **Decided** on 2026-10-21 (code review rework, finding F01): starting `git` by its bare name let Windows search the current directory, usually the repository, before `PATH`, so a `git.exe` committed to a cloned repository would have run.
- Branch (`current`, `set`): `git symbolic-ref --short -q HEAD`; exit code 1 with empty output means a detached HEAD.
- Identity (`new`): `git config user.name` and `git config user.email`; exit code 1 means not set (exit code 36).
- Line endings (`new`, `set`, for every file they write): `git check-attr eol text crlf -- <paths>` (at most 100 paths per call) and `git config core.autocrlf` / `git config core.eol` (once per run); rules in package 02, [`.gitignore`](02-repository-initialisation.md#gitignore). If `git` is missing, the line ending is left to `.editorconfig` and the operating system without a warning; if it fails, with the warning `git-attributes-unavailable`. Neither stops the command.
- If `git` cannot be started or fails unexpectedly while the branch or the identity is read, the command stops with exit code 16 (`git-unavailable`).
- `list`, `show`, and `drop` do not call git.
- `gh` (`new` with a URL whose host is in `trackers`; [Tracker issues](#tracker-issues-gh)): resolved by the same rules as `git`, with the file `gh.exe` on Windows and `gh` on Unix; the first path found is cached (`GhExecutableLocator`). `GhCommandRunner` starts only the resolved absolute path and refuses a relative one. If no entry contains a usable `gh` (`GhNotFoundException`), or the path is relative or cannot be started (`GhUnavailableException`, the base class of `GhNotFoundException`, as with `git`), `new` stops with exit code 33 (`invalid-source`), not 16: `gh` is needed only to load this source. **Decided** on 2026-10-25 (rework of `feature new`). **Implemented** (`GhExecutableLocator`, `GhCommandRunner`, `GhUnavailableException`, `GhNotFoundException`, `RunAsync_GhNotFound_ThrowsGhNotFoundException`, `RunAsync_GhLocatorReturnsRelativePath_ThrowsGhUnavailableExceptionWithoutStarting`, `RunAsync_ExecutableCannotBeStarted_ThrowsGhUnavailableExceptionWithStartFailure`).

### Branches

The engine does not create or switch branches in v1. `feature new` returns the suggested branch name `feature/<id>` so the skill can offer to create it. **Proposed.** **Implemented** (`suggestedBranch`).

### Output

**Decided** on 2026-10-20 (implementation clarification). **Implemented** (`Feature<X>CommandData`).

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
  - `show`: one `Key: value` line each for id, title, profile, status, source, created, and the three paths. The source line is `Source: <kind> <value>` for `url` and `file`, and `Source: text` for `text`, because the text can span many lines; it is in `source.value` of the JSON output and in `request.md`. **Decided** on 2026-10-25 (rework of `feature new`). **Implemented** (`FeatureShowCommand`, `ExecuteAsync_PlainModeTextSourceWithText_WritesKindOnly`).
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
| 33 | `invalid-source` | `new`: empty `--source`; a URL whose host is in `trackers` and whose last path segment is not an issue number; a file source larger than 1 MiB, that cannot be read, or that is not UTF-8 text ([File sources](#file-sources)); an issue whose Markdown is larger than 1 MiB; the issue of a URL whose host is in `trackers` cannot be read: `gh` is not found or cannot be started, exits with a non-zero exit code (the reason names it and the standard error of `gh`), or returns JSON that is invalid or not of the expected shape ([Tracker issues](#tracker-issues-gh)); the content of any other URL cannot be read: the request fails or times out, a redirect that the client did not follow, a status code other than 2xx, a content type or `charset` that cannot be read, a body larger than 1 MiB or not UTF-8 text ([Other URLs](#other-urls-http-get)) (`InvalidFeatureSourceException`, whose inner exception is the `GhUnavailableException`, `TrackerIssueReadFailedException`, or `UrlContentReadFailedException` of the reader, and whose `Problem` is `GhUnavailable`, `IssueReadFailed`, `UrlReadFailed`, or `InvalidSource` for every other cause). The message is `The source '<source>' cannot be used: <reason> <hint>`. The reason of a reader failure is `The issue cannot be read: <reason of gh>` or `The content of the URL cannot be read: <reason>`. The hint depends on `Problem`: `GhUnavailable` → "Make sure the GitHub CLI gh is installed, on the PATH, and can be executed, then run the command again." (one hint for `gh` not found and `gh` not startable, whose reason names the cause; **Decided** on 2026-10-26, code review of the branch, finding 6, replacing "Install the GitHub CLI gh and make it available on the PATH, …", which was wrong for an installed `gh` that cannot be started); `IssueReadFailed` → "Check the issue URL and that gh is signed in for its host ('gh auth status'), then run the command again."; `UrlReadFailed` → "Check the URL and run the command again, or save the content to a file and pass the path of the file with --source."; `InvalidSource` → "Pass an issue URL, the path of an existing UTF-8 text file, or the request text with --source." The gh and HTTP causes **Decided** on 2026-10-25 (rework of `feature new`): no new exit codes. The hints by cause and the reason without a repeated "the GitHub CLI gh" **Decided** on 2026-10-25 (code review of the rework, findings F2 and F10), replacing the one hint for every cause, which sent the user to another kind of source also when the URL was valid and only `gh` or the request failed, and the reason "The issue cannot be read, because the GitHub CLI gh cannot be run: …". The hint by `Problem` instead of by the type of the inner exception **Decided** on 2026-10-25 (simplification of the rework, approved by the user), so `CliErrorMapper` no longer depends on how the reader failures are wrapped. `CliErrorMapper` does not map the reader exceptions (`GhUnavailableException`, `GhNotFoundException`, `TrackerIssueReadFailedException`, `UrlContentReadFailedException`) themselves: `FeatureSourceReader` wraps them, so the mapping of a reader failure to its `Problem` is in one place, and a caller that does not wrap them ends with exit code 255 (`host-error`). The AC8 test `Map_EveryConcreteCoreException_ReturnsCliError` excludes these four types. **Decided** on 2026-10-26 by the user (code review of the branch, finding 7), replacing the fallback arm decided on 2026-10-25, which repeated the mapping of `FeatureSourceReader`. **Implemented** (`FeatureIdFactory`, `FeatureSourceReader`, `CliErrorMapper`, `CreateAsync_GhNotFound_ThrowsInvalidFeatureSourceExceptionWithReaderException`, `CreateAsync_TrackerIssueReadFails_ThrowsInvalidFeatureSourceExceptionWithReaderException`, `CreateAsync_UrlContentReadFails_ThrowsInvalidFeatureSourceExceptionWithReaderException`, `CreateAsync_IssueMarkdownOneByteOverMaximumSize_ThrowsInvalidFeatureSourceExceptionNamingSizeLimit`, `Map_EveryConcreteCoreException_ReturnsCliError`, `Map_InvalidFeatureSourceWithGhUnavailableProblem_ReturnsExitCode33WithGhAvailabilityHint`, `Map_InvalidFeatureSourceWithIssueReadFailedProblem_ReturnsExitCode33WithGhSignInHint`, `Map_InvalidFeatureSourceWithUrlReadFailedProblem_ReturnsExitCode33WithUrlHint`, `Map_InvalidFeatureSourceWithInnerExceptionAndDefaultProblem_ReturnsExitCode33WithSourceHint`, and end to end `FeatureNew_JsonIssueCannotBeRead_Returns33WithGhSignInHintAndChangesNothing`, `FeatureNew_PlainIssueCannotBeRead_Returns33WithGhSignInHint`, `FeatureNew_JsonGhNotFound_Returns33WithGhAvailabilityHint`, `FeatureNew_JsonUrlContentCannotBeRead_Returns33WithUrlHintAndChangesNothing`, `FeatureNew_PlainUrlContentCannotBeRead_Returns33WithUrlHint`). | None |
| 34 | `invalid-title` | `new`: a URL source whose host is not in `trackers` without `--title`, a title (from `--title`, the text, the file, or the issue) with a control character, or a title that gives an empty slug (`InvalidFeatureTitleException`). | None |
| 35 | `feature-io-failed` | `new`: the hidden staging folder cannot be created or cannot be moved to `features/<id>/`; `drop`: `features/<id>/` cannot be renamed to its hidden dropping folder. In each case the file system reports an IO error or access denied (`IOException`, including `DirectoryNotFoundException`, or `UnauthorizedAccessException`), unless the destination folder of `new` exists by then (exit code 30) or a file is at `features/<id>` (exit code 255). The message names the folder, the operation (`create` or `drop`), and the cause, and points to the write permissions of `features/` and to programs that hold a file open, such as an editor, a virus scanner, or a search indexer (`FeatureIoFailedException`). **Decided** by the user on 2026-10-21 (second code review, finding N01). | None; a staging folder that was already created is removed, the override is untouched |
| 36 | `git-identity-missing` | `new`: `user.name` or `user.email` is not set (`GitIdentityNotConfiguredException`). Checked before the request content is loaded, so it wins over exit codes 33, 34, and 30 ([Order of work](#order-of-work-and-failures-for-new)); **Decided** on 2026-10-25 (code review of the rework, finding F7). | None |
| 40 | `unknown-profile` | `new`: the profile from `--profile` or `defaultProfile` is not `feature`, `bugfix`, or `quick` (`UnknownProfileException`). In package 04's range, because profiles belong to it; this package owns the check until 04 exists. | None |
| 130 | `canceled` | Every command: the run was canceled with Ctrl+C. The message is `The command was canceled.`, without a stack trace. 128 + SIGINT, outside the area ranges like 2 (package 01). **Decided** on 2026-10-21 (code review rework). **Implemented** (`CliErrorMapper.MapCancellation`, `ICliCancellation` / `ConsoleCancellation`). | `new` and `init` (package 02) remove what they created in this run (best effort) |

Reused codes: 10 (`kit-root-not-found`), 12 (`not-a-git-repository`), and 15 (`kit-root-not-a-directory`) when the store cannot be opened; 20 (`format-too-new`), 21 (`upgrade-needed`), and 22 (`invalid-file`) for kit files, following package 01. Any other failure is not mapped and ends with exit code 255 (`host-error`), for example an IO error while `drop` deletes the hidden folder after the rename (the override is still cleared), an IO error while `new` writes `feature.json` or `request.md` into its staging folder, a file (not a folder) at `features/<id>` during `new`, or an option class the parser cannot create (a defect of the CLI). Exit code 14 (`path-not-a-file`) is no longer returned by `new`: since 2026-10-25 (rework of `feature new`) it reads no `request.md` template, so a directory at `.shadekit/templates/request.md` is ignored ([`request.md`](#requestmd)), replacing the reuse of code 14 **Decided** on 2026-10-24.

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
| `editorconfig-unreadable` | `feature new` or `set`: an `.editorconfig` file cannot be read while a file is written; the line ending comes from the operating system ([package 02](02-repository-initialisation.md#output)). |
| `editorconfig-invalid` | `feature new` or `set`: an `.editorconfig` file cannot be evaluated because of its content while a file is written; the line ending comes from the operating system ([package 02](02-repository-initialisation.md#output)). |
| `git-attributes-unavailable` | `feature new` or `set`: `git` is installed but cannot read the line ending settings of the repository while a file is written; the line ending comes from `.editorconfig` or the operating system ([package 02](02-repository-initialisation.md#output)). |

### Store additions

`IKitStore` gained `ReadFileAsync`, `FileExists`, `DirectoryExists`, `GetFiles` (recursive, kit-relative paths with `/`, ordinal order; does not follow directory links), `DeleteFile`, and `DeleteDirectory`; all check that the path stays inside the kit root. `KitStorePaths` gained `FeatureFileName` and `RequestFileName`. **Decided** on 2026-10-20 (implementation clarification). **Implemented** (`IKitStore`, `KitStore`, `KitStorePaths`).

The code review rework added `IKitStore.MoveDirectory` (a single rename that refuses an existing destination), `KitStorePaths.CurrentFeatureFilePath`, `KitStorePaths.GetFeatureDirPath`, and `FeatureTextFormat` (the shared `createdAt` format `yyyy-MM-ddTHH:mm:sszzz` of `show` and, since 2026-10-25, of the comment headings in the `request.md` of a tracker issue), so each of these is written down once. **Decided** on 2026-10-21 (code review rework). **Implemented** (`IKitStore`, `KitStore`, `KitStorePaths`, `FeatureTextFormat`).

`KitStorePaths.GetDisplayPath` builds the repository path that messages and the file path of format exceptions show (`.shadekit/` plus the kit-relative path with `/`). The store uses it for every file path it reports, and the read of the local override compares a format exception's file path with it to tell a problem of the override from a problem of `config.json`, so the two strings cannot drift apart. **Decided** on 2026-10-22 (code review verification, finding 6). **Implemented** (`KitStorePaths`, `KitStore`, `CurrentFeatureOverride`).

### Deviations from the draft

The implementation on 2026-10-21 changed these statements of the earlier draft. All were decided by the user in the implementation clarification on 2026-10-20.

| Draft | Now | Reason |
|---|---|---|
| `feature list` "**Implemented** with feature names only"; JSON `features` was a list of strings. | Returns id, title, profile, and status; `features` is a list of objects. | Package scope; breaking change of the earlier `feature list --json` contract. |
| This package: the engine fills `request.md` "with the original input"; package 09: the agent fills `request.md`. | The engine fills the five placeholders it owns; the `new` skill completes the file. Replaced on 2026-10-25, see below. | The two packages contradicted each other. |
| The slug is "limited to a short length". | At most 40 characters, cut at a `-`. | Unspecified. |
| Time-based id without a time zone. | Local time. | Unspecified. |
| `feature drop` without `--yes`: exit code not stated. | Exit code 0 in every mode. | Unspecified. |

### Rework of `feature new` (2026-10-25)

The rework of `feature new` changed these statements. The user approved the requirement and every decision of the rework on 2026-10-25.

| Before | Now | Reason |
|---|---|---|
| `request.md` is filled from the `request.md` template (repository template, else shipped template); the `new` skill completes it. | No template; `request.md` holds the request content that the engine loaded ([`request.md`](#requestmd)). | Requirement of the rework. |
| Fetching issue content is out of scope; the agent reads the issue with its own tools. | The engine reads the issue with `gh` ([Tracker issues](#tracker-issues-gh)); the abstraction `ITrackerIssueReader` keeps `gh` replaceable. | Requirement of the rework. |
| A URL whose host is not in `trackers`: the URL is the original input. | Its content is read with an HTTP GET ([Other URLs](#other-urls-http-get)). | Requirement of the rework. |
| `--title` is required for every URL source. | Optional for a URL whose host is in `trackers` (the issue title is used); still required for any other URL. | The issue read with `gh` has a title; any other URL gives none before its content is read. |
| `source.value` is absent for a `text` source. | The text exactly as passed; `show` prints only `Source: text`. Format version 1 unchanged. | Requirement of the rework; the text can span many lines. |
| A directory at `.shadekit/templates/request.md` stops `new` with exit code 14; a template that is not UTF-8 text with exit code 22. | The path is ignored and left in place. | No template is read any more. |

### Review

The implementation was reviewed on 2026-10-21: 1 Major, 10 Minor, 2 Suggestion, and 6 Nitpick findings (F01–F19). Every finding except F03 is fixed in the code review rework of 2026-10-21:

- F01 (Major): `git` is resolved only from absolute `PATH` entries ([Git and gh access](#git-and-gh-access)).
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
- F18: the guidance comment of the shipped `request.md` skeleton names the source kinds the engine writes (`text`, `url`, `file`); the agent may refine the reference when it completes the file. The skeleton was removed on 2026-10-25 ([Rework of `feature new`](#rework-of-feature-new-2026-10-25)).
- F19: the null-id test of `FeatureStoreTests` is a theory with four cases.

F03 (the standard output and error read tasks of `GitCommandRunner` are not observed after the process is killed on cancellation) was accepted as is by the user on 2026-10-21.

Exit code 130 (`canceled`) for a canceled run was set in the rework and confirmed by the user on 2026-10-21, together with the other decisions of the rework (override write failing after the move in `new` moves the feature back and deletes it; a failing delete after the rename in `drop` still clears the override; `set` and `new` replace an unusable override without a warning; a file at `features/<id>` stays exit code 255; an option class the parser cannot create stays 255).

The rework was reviewed a second time on 2026-10-21: 0 Critical, 0 Major, 2 Minor, 1 Suggestion, and 4 Nitpick findings (N01–N07). It found F01, F02 (within the decided scope), and F04–F19 resolved; F03 stays accepted. The user decided on 2026-10-21:

- N01 (Minor), fixed: a feature folder that `new` cannot create or move into place, or that `drop` cannot rename, ends with exit code 35 (`feature-io-failed`) instead of 255 ([Errors](#errors)). Without a retry on Windows.
- N02 (Minor), fixed: on Unix, `git` is accepted only with an execute bit; otherwise the next `PATH` entry is checked ([Git and gh access](#git-and-gh-access)).
- N03 (Suggestion), N04, N05, N06, and N07 (Nitpicks): accepted as is ([open points](#open-points)).

After this second rework, the build has 0 warnings and `dotnet test` runs 2029 tests: 2028 passed, 1 skipped (a Windows-only test), 0 failed. The user waived a third review run on 2026-10-21.

The branch was reviewed against `main` on 2026-10-22: a `/code-review` reported 10 findings, which were verified in a verification report. After the verification, 3 are Minor, 1 a Suggestion, and 4 Nitpicks; finding 9 is true but should not be acted on, and finding 1 was refuted: Spectre.Console already treats a redirected standard output as non-interactive, so `drop` does not ask. The user decided on 2026-10-22:

- Finding 3 (Minor), fixed: `set`, `show`, `drop`, the existence check of `new`, and the branch and override lookup match the folder name exactly ([Feature ids](#feature-ids)).
- Finding 2 (Minor), fixed: every id prefix in `trackers` is validated when `config.json` is read; a bad prefix makes the file invalid (exit code 22) ([Feature ids](#feature-ids); package 02, [`config.json`](02-repository-initialisation.md#configjson-v1)).
- Finding 7 (Minor), fixed: the heading of a `file` source skips fenced code blocks and a leading front matter block and strips a closing `#` sequence ([Id, title, and source rules](#id-title-and-source-rules)).
- Finding 10 (Nitpick), fixed: the JSON names of enum values have one entry point, the extension method `JsonEnumNames.ToJsonName()`; `CliJsonWriter.GetEnumName` is removed.
- Finding 5 (Nitpick), fixed: `new` checks the override through `ReadJsonAsync` with write access instead of a raw file read ([Order of work](#order-of-work-and-failures-for-new)).
- Finding 6 (Suggestion), fixed: `KitStorePaths.GetDisplayPath` builds the display path for the store and for the override check ([Store additions](#store-additions)).
- Finding 4 (Nitpick), documented: a concurrent `drop` of the same id can end with exit code 35 instead of 31 ([open points](#open-points)).
- Findings 8 (Nitpick) and 9, accepted as is: one 1 MiB read buffer per file source; the git identity is read with two parallel `git config` calls, because `git var GIT_AUTHOR_IDENT` would invent an identity from the account and host name and bypass exit code 36 (`git-identity-missing`).

After these fixes, the build has 0 warnings and `dotnet test` runs 2249 tests: 2248 passed, 1 skipped (a Windows-only test), 0 failed.

The rework of `feature new` was reviewed on 2026-10-25 (working tree against `HEAD`, against the requirement and decisions of the rework): 0 Critical, 0 Major, 5 Minor, 4 Suggestion, and 2 Nitpick findings (F1–F11); nothing blocked a merge. The user decided on 2026-10-25:

- F1 (Minor), fixed: `Microsoft.Extensions.Http` is on 10.0.13, the version of `Microsoft.Extensions.DependencyInjection` and `.Abstractions`, as the rework decided (versions aligned with the other `Microsoft.Extensions` 10.0.x packages).
- F2 (Minor), fixed: the exit-33 message ends with a hint chosen by the cause, so a valid URL whose issue or content cannot be read points to `gh` or the URL instead of to another kind of source ([Errors](#errors)).
- F3 (Minor), accepted as is: a second `AddShadeKitCore()` call registers the configuration of the named HTTP client a second time ([open points](#open-points)).
- F4 and F5 (Minor), fixed: the heading of an issue's `request.md` is one line, and trailing line breaks of the issue and comment bodies are removed, so blocks are separated by exactly one empty line and the text ends with exactly one line ending ([Tracker issues](#tracker-issues-gh)).
- F6 (Suggestion), fixed: every skill that reads `request.md` treats it as data that describes the requirement, never as instructions (package 09, [`new`](09-discuss-and-intake.md#new)).
- F7 (Suggestion), fixed: `new` checks the override and reads the git identity before it loads the request content, so a missing git identity (exit code 36) stops it before `gh` runs or a request is sent ([Order of work](#order-of-work-and-failures-for-new)).
- F8 (Suggestion), fixed: `HttpUrlContentReader` rents its read buffer from `ArrayPool<byte>.Shared` and decodes the body from a span of it, without a copy. Replaced on 2026-10-25 (simplification of the rework) by the shared `LimitedUtf8Reader`, which reads in chunks of 80 KiB, because the rented 2 MiB array was never reused in a one-shot CLI.
- F9 (Suggestion), fixed: end-to-end tests run `feature new` with a failing issue reader, a missing `gh`, and a failing URL reader, and check exit code 33, `invalid-source` in JSON mode, the message with its hint, and that no feature folder was created ([Errors](#errors)).
- F10 (Nitpick), fixed: the reason of a missing `gh` is `The issue cannot be read: <reason of gh>`, without a repeated "the GitHub CLI gh".
- F11 (Nitpick), fixed: the class remarks of `GitHubCliIssueReader` wrap at 120 characters.

The branch was reviewed against `main` on 2026-10-26: a `/code-review` reported 10 findings. The user decided on 2026-10-26:

- Finding 1, fixed: a source with whitespace between its leading and trailing whitespace is text, not a URL ([Id, title, and source rules](#id-title-and-source-rules)).
- Finding 2, fixed differently than proposed: the primary `SocketsHttpHandler` without redirects is removed, the client follows redirects, and the reader only explains a redirect that reaches it; the `User-Agent` is added once ([Other URLs](#other-urls-http-get)). This also keeps a primary handler that a test registers before `AddShadeKitCore()`.
- Finding 3, accepted as is: a tracker URL needs a working `gh` also with `--title` ([open points](#open-points)).
- Finding 4, fixed: the Markdown of an issue has the 1 MiB limit of the other sources ([Tracker issues](#tracker-issues-gh)).
- Finding 5, accepted as is: the order of work stays as decided for F7 on 2026-10-25 ([Order of work](#order-of-work-and-failures-for-new)).
- Finding 6, fixed: one neutral hint for a `gh` that is not found or cannot be started ([Errors](#errors)).
- Finding 7, fixed: the fallback arm for the reader exceptions is removed, and the AC8 test excludes them ([Errors](#errors)).
- Finding 8, fixed in part: the `charset` labels `utf8` and `us-ascii` are accepted; automatic decompression stays off ([Other URLs](#other-urls-http-get)).
- Finding 9, accepted as is: `ExternalProcessRunner` and `ExternalToolRunner` both check that the executable path is absolute, the inner check as the guarantee where the process starts.
- Finding 10, fixed: the control-character check of a title is a `foreach` over the span of the title (a `foreach` over the string itself is flagged by Sonar S3267).

## Acceptance criteria

Every criterion is **Implemented**, FP03-AC11 only partly. The tests that prove it carry `[Trait("Requirement", "FP03-ACn")]`. Git is faked in every test, and so are `gh` and HTTP (a fake `IGhCommandRunner` with JSON output, an `HttpClient` over a stub `HttpMessageHandler`, fake readers in the end-to-end tests); the real `git` process and the redirected-input path of `feature drop` were checked with the built CLI on macOS on 2026-10-21.

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
- Every host in `trackers` is read with `gh` in v1 ([Tracker issues](#tracker-issues-gh)). A tracker that `gh` cannot read, for example GitLab or Jira, stops `new` with exit code 33; such a URL can only be used after its host is removed from `trackers`, as a time-based id with `--title` whose content is read with an HTTP GET. A reader per tracker (another tool or a tracker API library behind `ITrackerIssueReader`) is **Open**.
- `gh` and HTTP are faked in every test (decided): no test starts the real `gh`, and no test sends a real request. The process run of `ExternalProcessRunner` (output capture, environment, killing the process tree on cancellation), now shared by `git` and `gh`, is not covered by tests, as before for `GitCommandRunner`. That the JSON of a real `gh issue view --json title,body,comments` has the expected shape (`author.login`, `createdAt`) is **Unverified** in this rework; it was not checked with a real `gh`. **Open.**
- FP03-AC11 "`done` after `phase complete`" and the phase fields in `feature list`, `feature current`, and `feature show` wait for package 04.
- Known gaps and residual risks after the code review rework on 2026-10-21, all **Open**:
  - The process run of `GitCommandRunner` (output capture, exit codes, killing the process on cancellation) is not covered by tests, because no test starts the real `git` (decided); the lookup and the refusal of a relative path are tested. The process run was checked with the built CLI on macOS on 2026-10-21, before the rework.
  - After a kill on cancellation, the standard output and error read tasks of `GitCommandRunner` are not observed (F03, accepted by the user on 2026-10-21). Since 2026-10-25 the same holds for `gh`, because both run through `ExternalProcessRunner`.
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
- Finding of the code review of the rework of `feature new` accepted as is by the user on 2026-10-25, not fixed:
  - F3: fixed on 2026-10-26 (code review of the branch, finding 2): the `User-Agent` is added only when the client has none, so a repeated `AddShadeKitCore()` sends it once (`AddShadeKitCore_CalledTwice_AddsUserAgentOnce`).
- Residual risk accepted by the user on 2026-10-22 (code review verification, finding 4), not fixed:
  - When two `drop` runs for the same id run at the same time, both pass the existence check. The second one's rename of `features/<id>/` to its hidden dropping folder fails with `DirectoryNotFoundException`, so it ends with exit code 35 (`feature-io-failed`), whose message points to the write permissions of `features/` and to programs that hold a file open, instead of exit code 31 (`feature-not-found`). Nothing is lost: the first run drops the feature, and the second changes nothing.
