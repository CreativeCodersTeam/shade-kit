# Code Review — feature/rework-file-content-handling vs. main

**Date:** 2026-10-24
**Mode:** branch (`main...HEAD`, merge base `ae578a4`)
**Commits reviewed:**

| Commit | Message |
|---|---|
| `79f8954` | refactor!: remove `TextFileLine`; improve `.editorconfig` handling, Git attribute resolution, and line ending logic |
| `28c4265` | refactor: standardize object creation formatting across all code files |
| `6ed145d` | refactor!: remove unused store and test classes; introduce Git and directory writing abstractions |
| `1e4c0a4` | refactor!: reorganize exceptions and warnings into distinct namespaces to improve clarity and maintainability |

**Diff size:** 209 files, 9469 insertions, 1810 deletions
**Target framework:** net10.0
**Tools run:** none. Build, test, and format were not run as part of this review.
**Exclusions:** findings already in the earlier reports (`docs/reviews/2026-10-23-…-uncommitted.md`, `docs/reviews/2026-10-24-…-uncommitted.md`) are not repeated. This includes the decisions the user accepted there: whole files are rewritten with one line ending, and F4 (`text`/`text=auto` without `core.eol` is left to `.editorconfig`).
**Verification:** each finding was checked against the source at the line given. Finding 1 also rests on a decompiled `EditorConfig.Core` 0.18.0 `GlobMatcher.BraceExpand` (`ilspycmd`). The memory use it describes is estimated, not measured.

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 1 |
| Minor | 4 |
| Suggestion | 3 |
| Nitpick | 1 |

| # | Severity | Location | Summary |
|---|---|---|---|
| 1 | Major | `Files/EditorConfigLineEndingResolver.cs:168` | Large numeric-range globs below `Array.MaxLength` are fully expanded on every write |
| 2 | Minor | `Store/KitStore.cs:354` | Versioned JSON files are not read as strict UTF-8 |
| 3 | Minor | `Features/FeatureStore.cs:416` | A lone CR survives into `request.md`, so line endings are mixed |
| 4 | Minor | `Git/GitLineEndingResolver.cs:120` | Every git failure is swallowed without a warning and is not cached |
| 5 | Minor | `Store/KitStore.cs:234` | `ReadFileAsync` returns `null` for a directory and hides `PathNotAFileException` |
| 6 | Suggestion | `Directory.Build.props:10` | `<NoWarn>IDE0090</NoWarn>` replaces `NoWarn` and is set at the wrong level |
| 7 | Suggestion | `Features/FeatureStore.cs:103` | `feature new` starts three `git check-attr` processes instead of using `PrepareAsync` |
| 8 | Suggestion | `Store/KitStore.cs:177` | The same path is validated and normalised about five times per write |
| 9 | Nitpick | `Files/TextFileContent.cs:192` | "Immutable" `TextFileContent` exposes a mutable list as `Lines` |

**Top risks:**

1. A crafted or careless `.editorconfig` in a cloned repository can make `shade-kit init` or `feature new` use gigabytes of memory or hang (finding 1).
2. Two read paths in the same store treat broken encodings differently. A JSON file is damaged silently instead of being reported (finding 2).
3. When git refuses to run (e.g. `safe.directory`), the line ending that `.gitattributes` asks for is ignored without any sign (finding 4).

## Decisions

The user went through every finding on 2026-10-24 and chose a solution for each:

| # | Decision |
|---|---|
| 1 | Check section headers before parsing; the product of numeric-range lengths is capped at 10 000, and a header above the cap gives `EditorConfigInvalidWarning`. The `OutOfMemoryException` catch is removed. |
| 2 | One shared strict read path: `ReadJsonAsync` decodes with `TextFileEncoding.DecodeUtf8` and the `InvalidKitFileException` factory, like `ReadFileAsync`. |
| 3 | `SplitBlock` splits at `\r\n`, a lone `\r`, and `\n`. Unicode line separators (U+0085, U+2028, U+2029) are kept as text. |
| 4 | A git that exists but fails gives a `git-attributes-unavailable` warning once per run, and the failure is cached per repository root. A missing git stays silent (also cached). A canceled call is not cached. |
| 5 | The `Directory.Exists` branch in `ReadFileAsync` is removed: a directory gives `PathNotAFileException`, a missing path still gives `null`. |
| 6 | Both: `csharp_style_implicit_object_creation_when_type_is_apparent = false:warning` in `.editorconfig`, and `NoWarn` becomes `$(NoWarn);IDE0090`. |
| 7 | `feature new` calls `PrepareAsync` once with the three final paths. |
| 8 | A `RepositoryFile` value type is resolved once at the boundary and passed down to the writer and the resolvers, on this branch. |
| 9 | `TextFileContent` stores its lines as `ImmutableArray<string>`. |

## Implementation

All nine decisions are implemented in the working tree (not committed). Verification: `dotnet build` with 0 warnings and 0 errors; `./build.sh -t pack` succeeded with 2254 core tests and 620 CLI tests, 0 failed, 1 skip that was there before; a CLI smoke test in temporary repositories (see the end of this section).

| # | Status | Where | Deviation or side effect |
|---|---|---|---|
| 1 | Done | `EditorConfigGlobExpansion` (new), `EditorConfigLineEndingResolver` | The count follows `GlobMatcher.BraceExpand` of EditorConfig.Core 0.18.0 and covers alternatives (`{a,b}` × n) as well, not only numeric ranges, because they grow exponentially too. The parser gets an empty stand-in for a file whose glob is too large, so the other files are still read and an unreadable file keeps its precedence (existing test `GetLineEnding_ParentEditorConfigUnreadableAndNestedMalformed_ReturnsUnreadableWarning`). `OutOfMemoryException` is no longer caught. |
| 2 | Done | `KitStore.ReadVersionedFileAsync` | Applies to the format check of a write as well, so `Replace` access replaces a JSON file that is not UTF-8 and `Write`/`Upgrade` stop. |
| 3 | Done | `TextFileContent.SplitBlock` / `FromText` | Applies to every file that is read, not only `request.md`. Side effect: the `templates.lock` hash of a template that contains a lone CR changes (lone CR now counts as LF). Shipped templates contain none. |
| 4 | Done | `GitLineEndingResolver`, `GitNotFoundException` (new), `GitAttributesUnavailableWarning` (new), CLI code `git-attributes-unavailable` | Failed `git config` reads are now kept as a failure as well; the earlier tests "reads again on next call" were replaced. The warning names only the git subcommand (`git check-attr`, `git config`), because the full `check-attr` command line of `init` lists every file. The CLI end-to-end tests use an empty `.git` folder, which git rejects, so `init` now reports the warning there; `RunAsync_InitPlain_PrintsSummaryAndReturns0` accepts it before the summary. |
| 5 | Done | `KitStore.ReadFileAsync` | `feature new` stops with exit code 14 (`path-not-a-file`) when `.shadekit/templates/request.md` is a directory. |
| 6 | Done | `.editorconfig`, `Directory.Build.props` | — |
| 7 | Done | `IKitStore.PrepareLineEndingsAsync`, `ITextFileWriter.PrepareAsync` (both new), `FeatureStore.CreateAsync` | `RepositoryInitializer` still calls `ILineEndingResolver.PrepareAsync` directly, so there are two entry points for the same preparation. |
| 8 | Done | `RepositoryFile` (new), `ITextFileWriter`, `ILineEndingResolver`, `IGitLineEndingResolver`, `KitStore`, `RepositoryInitializer` | `RepositoryFile` is a `sealed record` class instead of the `record struct` of the proposal: `default(RepositoryFile)` of a struct would bypass the check that the type exists for. The git command runs in the normalized repository root. `RepositoryFilePath.GetFullPath` and `GetGitPath` were removed. The path validation tests moved from the writer and resolver tests to `RepositoryFileTests`. |
| 9 | Done | `TextFileContent` | — |

Smoke test of the built CLI (`shade-kit.dll`) in temporary repositories:

- Real repository: `init` and `feature new` without warnings.
- Broken `.git/config`: `init` succeeds with one `git-attributes-unavailable` warning ("fatal: bad config line 8 in file .git/config").
- `.editorconfig` with `[{1..20000}{1..20000}]` and no global git configuration: `init` succeeds in under one second with `editorconfig-invalid`.
- File source with CR-only line endings: `request.md` contains no CR.
- `.shadekit/templates/request.md` as a directory: `feature new` ends with exit code 14 `path-not-a-file`.

## Findings — src/CreativeCoders.ShadeKit.Core/Files/EditorConfigLineEndingResolver.cs

### 1. [Major][Robustness] src/CreativeCoders.ShadeKit.Core/Files/EditorConfigLineEndingResolver.cs:168

`IsInvalidContentException` catches `OutOfMemoryException` because the parser "asks for the oversized array before it allocates anything, so the process is not short of memory". That holds only when the requested list size is above `Array.MaxLength`. Ranges whose product stays below that limit are expanded in full.

`GlobMatcher.BraceExpand` calls `new List<string>(Math.Abs(num3 + num4 - num2) * list2.Count)` and then adds every expanded string. A section header such as `[{1..20000}{1..20000}]` (4·10⁸ entries) or `[{1..100000000}]` (10⁸ entries) stays below `Array.MaxLength`, so the fast "too large" path never triggers. Unless git decides the line ending, every write in `shade-kit init` (16 files) or `feature new` rebuilds these strings. The process either thrashes for minutes or hits a real out-of-memory condition. In that case the catch reports it as `editorconfig-invalid`, although the process really was out of memory, which contradicts the comment.

**Fix:** check section headers before handing the file to the parser. Cap the product of all numeric-range lengths (for example at 10 000) and report a header above the cap as `EditorConfigInvalidWarning`. After that, the `OutOfMemoryException` catch can be removed.

```csharp
// Before parsing: reject headers whose brace expansion would be too large
private const long MaxBraceExpansion = 10_000;

private static bool HasOversizedNumericRanges(string sectionHeader)
{
    long product = 1;
    foreach (Match match in NumericRangeRegex().Matches(sectionHeader))
    {
        if (!long.TryParse(match.Groups[1].Value, out var from) ||
            !long.TryParse(match.Groups[2].Value, out var to))
        {
            return true;
        }

        product *= Math.Abs(to - from) + 1;
        if (product > MaxBraceExpansion)
        {
            return true;
        }
    }

    return false;
}

[GeneratedRegex(@"\{(-?\d+)\.\.(-?\d+)\}")]
private static partial Regex NumericRangeRegex();
```

Add a test with `[{1..20000}{1..20000}]` that expects the warning and a fast return.

## Findings — src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs

### 2. [Minor][Correctness] src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs:354

`ReadJsonAsync` still reads with `File.ReadAllTextAsync`. That call accepts UTF-16 and UTF-32 byte order marks and replaces invalid bytes with U+FFFD. It goes against the CLAUDE.md rule "Files are read as strict UTF-8" and against `ReadFileAsync`, which this branch made strict.

**Failure scenario:** a `config.json` or `feature.json` contains a Latin-1 `ä` in a title, or an editor saved it as UTF-16 LE. It is read without an error: the `ä` becomes U+FFFD, and the UTF-16 file is accepted silently. A later write with `Upgrade` access (or `Replace` for `current-feature.json`) saves the U+FFFD back to disk, so the data is damaged instead of being reported as `InvalidKitFileException` (exit code 2x). In the same store, `templates/request.md` with the same bytes does stop with `InvalidKitFileException`, so the two read paths behave differently.

**Fix:** read the bytes and decode them with `TextFileEncoding.DecodeUtf8` and the `InvalidKitFileException` factory, as `ReadFileAsync` does. Add a test for invalid UTF-8 and for a UTF-16 BOM in a JSON file.

### 5. [Minor][Correctness] src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs:234

`ReadFileAsync` returns `null` when the path is a directory. This skips the `PathNotAFileException` that `TextFileContent.ReadAsync` would throw, and that exception has its own exit code mapping.

**Failure scenario:** `.shadekit/templates/request.md` exists as a directory (a bad merge or a tool bug). `FeatureStore.ReadRequestTemplateAsync` gets `null` and silently falls back to the shipped template, so the repository's own request template is ignored without notice.

**Fix:** drop the `Directory.Exists` check, or throw `PathNotAFileException` there. Return `null` only for a missing path.

### 8. [Suggestion][Maintainability] src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs:177

For each `WriteJsonAsync`/`WriteFileAsync`, `relativePath` and `lineEndingPath` are validated and normalised about five times:

1. `KitStore.GetFullPath`
2. `KitStore.GetRepositoryRelativePath`, which calls `GetFullPath` again
3. `RepositoryFilePath.GetFullPath` in `TextFileWriter`
4. again in `LineEndingResolver`
5. `RepositoryFilePath.Resolve` in `GitLineEndingResolver.GetGitPath`

The runtime cost is small. The real cost is maintenance: five places have to keep the same rules and parameter names, and each has its own "validate before anything" comment. Earlier review rounds already found drift between them (the `ParamName` finding in the 2026-10-24 report).

**Proposal:** resolve the path once at the boundary into a small value type (repository root, full path, normalised relative path, git path) and pass that to the writer and the resolvers.

```csharp
public readonly record struct RepositoryFile(string RepositoryRootPath, string FullPath, string RelativePath)
{
    public string GitPath => RelativePath.Replace(Path.DirectorySeparatorChar, '/');

    public static RepositoryFile Resolve(string repositoryRootPath, string relativePath,
        [CallerArgumentExpression(nameof(relativePath))] string paramName = "relativePath")
    {
        // the one place that validates and normalises
    }
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs

### 3. [Minor][Correctness] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:416

`request.md` is meant to be written "as a whole with one line ending" (comment at line 414, package 03). But `TextFileContent.FromText` → `SplitBlock` (`TextFileContent.cs:282`) splits only at `\n` and strips a trailing `\r`. A lone CR stays inside a line.

**Failure scenario:** `shade-kit feature new --source notes.txt`, where `notes.txt` uses classic Mac CR-only line endings or has stray CRs. `FeatureRequestTemplate.Fill` inserts the text, every `\r` stays inside one "line", and `request.md` ends up with CR in some places and LF/CRLF in others. `ShippedTemplateReader` (`ShippedTemplateReader.cs:46`) normalises with `ReplaceLineEndings("\n")`, so the shipped template and the repository-template/source path behave differently.

**Fix:** normalise with `ReplaceLineEndings("\n")` before `FromText`, or treat a lone `\r` as a line break in `SplitBlock`. The second option also covers the other callers (`AppendBlock`, `ReplaceLines`). Note that `ReplaceLineEndings` also converts U+0085, U+2028, and U+2029. Decide whether that is wanted for user input.

```csharp
private static string[] SplitBlock(string text)
{
    return text.ReplaceLineEndings("\n").Split('\n');
}
```

Add a test with `"a\rb\r\nc\n"` as the source.

### 7. [Suggestion][Performance] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:103

`feature new` starts a separate `git check-attr` for `feature.json`, `request.md`, and `current-feature.json`, plus up to two `git config` reads. `ILineEndingResolver.PrepareAsync` exists for exactly this, and `RepositoryInitializer` (`RepositoryInitializer.cs:95`) uses it. All three final paths are known before `CreateDirectoryExclusiveAsync`, so one `PrepareAsync` call would replace them with a single process. On Windows, where starting a process costs tens of milliseconds, an agent loop that creates features pays for this on every call.

## Findings — src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs

### 4. [Minor][Correctness] src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs:120

`GetLineEndingAsync` catches `GitUnavailableException` and returns `null`. That exception is thrown both when git cannot be started (`GitCommandRunner.cs:34/40/53`) and for an unexpected exit code (`GitCommandRunnerExtensions.cs:46`). The comment says "git is missing or fails", so every git failure is swallowed, with no warning and no cache.

**Failure scenario:** the repository has `* text eol=crlf` in `.gitattributes`, but git refuses to run (exit 128 "detected dubious ownership" / `safe.directory`, or a broken include in `.git/config`). `GitRepositoryLocator` only looks for `.git` in the file system, so `shade-kit init` still runs. Each `check-attr` fails, `GetLineEndingAsync` returns `null`, and `.gitignore`, `CLAUDE.md`, `AGENTS.md`, and all `.shadekit/` files get the OS line ending (LF on macOS/Linux) without any notice, while `.editorconfig` problems do produce warnings. The failure is not cached, so each of the ~19 writes starts another failing git process.

**Fix:**

- Report a warning in `LineEndingResolution` when git exists but fails, e.g. `git-attributes-unavailable` in `ShadeKitCliWarningCodes`. A missing git executable can stay silent.
- Remember the failure per repository root for the resolver's lifetime (one command run), as is already done for the `core.*` values.
- Add tests for both: one warning per run, and only one git start after the failure.

## Findings — Directory.Build.props

### 6. [Suggestion][Build] Directory.Build.props:10

`<NoWarn>IDE0090</NoWarn>` has two problems:

1. It replaces `NoWarn` instead of appending to it. Any value set before this file (SDK defaults, an earlier import) is lost. The usual form is `<NoWarn>$(NoWarn);IDE0090</NoWarn>`.
2. It is set at the wrong level. Commit `28c4265` changed every `new()` to `new T()` and then switched IDE0090 off for the build only. IDEs that read `.editorconfig` still suggest `new()`, so contributors will bring the target-typed form back.

**Fix:** set the style in `.editorconfig` and remove the `NoWarn`:

```ini
[*.cs]
csharp_style_implicit_object_creation_when_type_is_apparent = false:warning
```

## Findings — src/CreativeCoders.ShadeKit.Core/Files/TextFileContent.cs

### 9. [Nitpick][API design] src/CreativeCoders.ShadeKit.Core/Files/TextFileContent.cs:192

`TextFileContent` is documented as immutable ("Instances are immutable"), but `AppendBlock` and the private constructor store the `List<string>` or array they built directly as `Lines`, without copying or wrapping it. A caller can write `((List<string>)content.AppendBlock(block).Lines).Add("x")` and change a `PlannedFileChange` after planning. `HasSameContent` and `TemplatesLockEntry.ComputeHash` would then see different lines than were planned. Today all callers are internal, so this is latent, but `TextFileContent` and `Lines` are public.

**Fix:** store an `ImmutableArray<string>` (or wrap with `AsReadOnly()`) in the constructor.

## Not covered

- No build, test, or `dotnet format` run was part of this review. Run `./build.sh -t pack` before the pull request.
- The memory use in finding 1 is estimated from the decompiled parser code, not measured.
- Test changes were read only where a finding needed them. Test coverage of the 209 changed files was not assessed as a whole.
