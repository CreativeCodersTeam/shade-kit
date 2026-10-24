# .NET Code Review — feature/rework-file-content-handling (uncommitted)

**Date:** 2026-10-24
**Mode:** uncommitted (working tree vs. HEAD, including untracked files) — origin: provided
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (all five projects target net10.0; `resolved_from: csproj`)
**Checklist:** review-checklist-net10.md, plus the general security, performance, architecture, and code-quality checklists
**Tools run:** build=N · format=N · test=N — origin: provided. The calling workflow reported build 0 warnings / 0 errors and 2776 tests, 0 failed, 1 pre-existing skip. The reviewer did not re-run these.
**Report language:** English — origin: provided
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**; the first-round report `docs/reviews/2026-10-23-feature-rework-file-content-handling-uncommitted.md` is part of the diff but was not reviewed
**Review strategy:** chunked (auto-selected, non-interactive; the diff exceeds the large-diff gate with 71 files and 8237 changed LOC)
**Diff size:** 71 files, 8237 changed LOC
**Review round:** re-review after the Gate 5 rework (Tasks #4 and #5) of the findings F1–F9 from the first-round report

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 1 |
| Suggestion | 0 |
| Nitpick | 1 |

**Resolution of the first-round findings:**

| Finding | Severity (round 1) | Status | Evidence |
|---|---|---|---|
| F1 malformed `.editorconfig` aborts the command | Major | **Resolved** | `EditorConfigLineEndingResolver.IsInvalidContentException` (`OverflowException`, `ArgumentOutOfRangeException`, `OutOfMemoryException`) maps parser failures to the new `EditorConfigInvalidWarning`, CLI code `editorconfig-invalid`. `FindInvalidEditorConfigPath` evaluates each file that was read on its own to name the failing file. Tests cover all four probe cases plus nested, parent, non-matching, above-root, and fixed/broken-between-calls cases. The catch set matches what the package can throw (**FACT**): `GlobMatcher.BraceExpand` uses `int.Parse` only on regex digit groups, so `FormatException` cannot occur, and `GlobMatcher.Create`/`Parse` build no `Regex`. `OperationCanceledException` is never caught. |
| F2 line ending resolved for the staging path | Minor | **Resolved** | `lineEndingPath` on `ITextFileWriter.WriteAsync`, `IKitStore.WriteFileAsync`, and `IKitStore.WriteJsonAsync`. `FeatureStore.WriteFeatureFilesAsync` passes `features/<id>/feature.json` and `features/<id>/request.md`. Tests: `CreateAsync_NewFeature_ResolvesLineEndingOfEachFileForItsFinalPathNotTheStagingPath`, `CreateAsync_FinalFeaturePathPrescribesOtherLineEnding_…`, and writer/store tests with `staging/` vs `final/`. |
| F3 attribute evaluation differs from `convert_attrs` | Minor | **Resolved** | `binary` is no longer read. `crlf` is requested and counts only while `text` is undefined. `input` → LF. `eol` wins over `input`/`auto` but not over `-text`. Checked step by step against git's `git_path_check_crlf`/`convert_attrs`/`output_eol`/`text_eol_is_crlf`: `text`, `-text`, `text=input`, `text=auto`, undefined + `core.autocrlf` true/input/false, and `eol` with each `text` state all give the same line ending as git. |
| F4 `text`/`text=auto` without `core.eol` → `.editorconfig` | Minor | **Accepted by the user** (not re-raised) | Gate-2 step 4 stays as decided. |
| F5 results carry every warning the store collected | Suggestion | **Resolved** | `IKitStore.Warnings` was removed. Each write returns its own warnings. `FeatureStore.CreateAsync`/`SetCurrentAsync` and `RepositoryInitializer` gather only the warnings of their own writes. Tests `…StoreUsedBeforeWithWarnings_ReturnsNoWarningOfEarlierOperation` and `InitializeAsync_KitStoreUsedBeforeWithWarnings_…`. |
| F6 two or three `git` processes per file | Suggestion | **Resolved** | `core.autocrlf`/`core.eol` are cached per normalised repository root in a `ConcurrentDictionary` on the singleton. Failed or canceled reads are not cached. `git check-attr` still runs once per file, which the decision allows ("and/or"). There are no stale values across runs (**FACT**): `Program.RunAsync` builds a new `CliHostBuilder` host, and so a new container, per run. Tests also cover separate instances, separate containers, failure, and cancellation. |
| F7 integer `core.autocrlf` values | Nitpick | **Resolved** | `IsGitTrue`/`TryParseGitInteger` follow `git_parse_signed`: sign, decimal/`0x`/octal, `k`/`m`/`g` unit, int range. Edge cases checked (`0`, `08`, `0x`, `k`, `-0x1`) give the same result as `strtoimax` plus the unit check. |
| F8 warning message repeats the path / empty reason | Nitpick | **Resolved** | `CliErrorMapper.CreateEditorConfigMessage` names the path once (the path is dropped when the reason already quotes it) and builds a sentence without a reason when the reason is empty. |
| F9 warning path untested on Windows, no malformed-glob test | Suggestion | **Resolved** | `GetLineEnding_EditorConfigLockedExclusively_ReturnsNoLineEndingAndWarning` and `…ParentEditorConfigUnreadableAndNestedMalformed_…` use a `FileShare.None` lock, which also works on Windows. They skip only when `DOTNET_SYSTEM_IO_DISABLEFILELOCKING` is set. Malformed-range theory with four headers. |

**Top risks:**
1. An invalid `lineEndingPath` throws an `ArgumentException` whose `ParamName` is `relativePath`, so callers and logs point at the wrong argument (Minor).
2. When the failing `.editorconfig` file is not known, the warning message calls the written file "the .editorconfig file". With the current parser this path is practically unreachable (Nitpick).
3. Accepted residual risk (F4, no finding): for files with `text`/`text=auto` and no `core.eol`, `.editorconfig` decides where git would use `native`.

**Overall:** All eight findings the user chose to fix are resolved. The rework is precise: git attribute semantics now match `convert.c`, parser failures fall back with a dedicated warning, and every write returns its own warnings. The tests cover each case without the real git executable and independently of the OS. The new defects are small: an exception parameter name and a message wording in a fallback branch.

Verified as correct (no finding): `TextFileWriter`, `KitStore.WriteFileAsync`, and `KitStore.WriteJsonAsync` validate `lineEndingPath` before they resolve, delete (`Replace` access), or write anything, and the tests prove this for the writer and the store. `KitStore` maps `lineEndingPath` to a repository-relative path the same way as `relativePath`. `FeatureStore.CreateFeatureDirAsync` collects the warnings in a list that the single `populate` call fills (`CreateDirectoryExclusiveAsync` calls `populate` exactly once). `KitWarningOrder.Arrange` keeps format warnings first, removes duplicates, and keeps write order. `SetCurrentAsync` returns the mismatch warning before the override warnings, as documented in package 03. `RepositoryInitializer` gathers the warnings of the kit root first, then those of the repository files, and removes duplicates. The cache in `GitLineEndingResolver` is thread-safe (`TryGetValue`/`TryAdd`; at worst two concurrent first reads, as documented). No test of the line-ending logic starts the real `git` (the fake `IGitCommandRunner` is used in `GitLineEndingResolverTests`). The only OS-dependent expectations are computed from `OperatingSystem.IsWindows()` for the public constructors. `CLAUDE.md` and the feature-package docs 02/03 describe the implemented behaviour.

## Findings — src/CreativeCoders.ShadeKit.Core/Files/TextFileWriter.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Files/TextFileWriter.cs:27
An invalid `lineEndingPath` throws an `ArgumentException` whose `ParamName` is `relativePath`. The same applies to `KitStore.cs:156` (`WriteJsonAsync`) and `KitStore.cs:183` (`WriteFileAsync`).

The new argument is validated by reusing `RepositoryFilePath.GetFullPath(repositoryRootPath, lineEndingPath)` and `KitStore.GetFullPath(lineEndingPath)`/`GetRepositoryRelativePath(lineEndingPath)`. Both helpers name their own parameter: `Ensure.IsNotNullOrWhitespace(relativePath)` and `nameof(relativePath)`. For an empty, rooted, or escaping `lineEndingPath`, the exception therefore reports `ParamName == "relativePath"`, and the message says "The path '…' must be relative to the repository root" without saying which argument was wrong. The XML docs of `ITextFileWriter.WriteAsync`, `IKitStore.WriteFileAsync`, and `IKitStore.WriteJsonAsync` document the exception for `lineEndingPath` as its own case. The tests (`WriteAsync_InvalidLineEndingPath_…`, `WriteFileAsync_InvalidLineEndingPath_…`) assert only the exception type, so they do not catch this. Pass the parameter name through the helpers and assert it in the tests.

```csharp
// RepositoryFilePath: let the caller name the argument
public static string GetFullPath(string repositoryRootPath, string relativePath,
    [CallerArgumentExpression(nameof(relativePath))] string paramName = "relativePath")
{
    return Resolve(repositoryRootPath, relativePath, paramName).FullPath;
}

private static (string FullPath, string NormalizedRelativePath) Resolve(string repositoryRootPath,
    string relativePath, string paramName)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
    ArgumentException.ThrowIfNullOrWhiteSpace(relativePath, paramName);

    if (Path.IsPathRooted(relativePath))
    {
        throw new ArgumentException(
            $"The path '{relativePath}' must be relative to the repository root, but it is rooted.", paramName);
    }
    // ... same for the "below the repository root" check
}

// TextFileWriter.WriteAsync
if (lineEndingPath is not null)
{
    RepositoryFilePath.GetFullPath(repositoryRootPath, lineEndingPath); // ParamName is now "lineEndingPath"
}

// KitStore: same pattern for GetFullPath / GetRepositoryRelativePath (add a paramName parameter)

// Tests
await act.Should().ThrowAsync<ArgumentException>()
    .WithParameterName("lineEndingPath");
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs:415
When the failing `.editorconfig` file is not known, the message calls the written file "the .editorconfig file".

`EditorConfigUnreadableWarning.FilePath` and `EditorConfigInvalidWarning.FilePath` are documented to hold the path of the file whose line ending was resolved when the failing `.editorconfig` is unknown. `EditorConfigLineEndingResolver.cs:66` and `:72` fall back to `filePath`. `CreateEditorConfigMessage` always writes "The .editorconfig file '<FilePath>' …", which then gives, for example, "The .editorconfig file '/repo/.shadekit/templates/spec.md' cannot be evaluated …". With `editorconfig` 0.18.0 the fallback is practically unreachable: reads go through the factory, which records the path, and matching failures are traced to a file by `FindInvalidEditorConfigPath`. The wording is still wrong for a documented state. Either word the message neutrally when the path is not an `.editorconfig` file, or carry the distinction in the warning.

```csharp
private static string CreateEditorConfigMessage(string filePath, string problem, string reason)
{
    // FilePath is the .editorconfig file, or the written file when the failing .editorconfig is not known.
    var subject = string.Equals(Path.GetFileName(filePath), ".editorconfig", StringComparison.OrdinalIgnoreCase)
        ? $"The .editorconfig file '{filePath}'"
        : $"An .editorconfig file that applies to '{filePath}'";

    // ... build the sentence from `subject` as today
}
```

## Tool Output Appendix

### dotnet build
- Skipped (not requested). The calling workflow reported 0 warnings and 0 errors.

### dotnet test
- Skipped (not requested). The calling workflow reported 2776 tests, 0 failed, 1 pre-existing skip.

### dotnet format
- Skipped (not requested).

### Reviewer verification (not a tool run)
- Read the decompiled `editorconfig` 0.18.0 members in the session scratchpad (`GlobMatcher.Create`, `GlobMatcher.Parse`, `GlobMatcher.BraceExpand`) and the earlier probe outputs (`ecprobe3.out`, `ecprobe3b.out`): the only exceptions the parser throws on content are `OverflowException`, `ArgumentOutOfRangeException`, and `OutOfMemoryException`, which matches `IsInvalidContentException`.
- Checked `GitLineEndingResolver` against git's `convert.c` (`git_path_check_crlf`, `git_path_check_eol`, `convert_attrs`, `output_eol`, `text_eol_is_crlf`) and `git_parse_signed`.
- Confirmed in `src/CreativeCoders.ShadeKit.Cli/Program.cs` that every `RunAsync` builds its own host and service container, so the per-instance configuration cache lives for exactly one command run.
