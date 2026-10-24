# .NET Code Review — feature/rework-file-content-handling (uncommitted)

**Date:** 2026-10-23
**Mode:** uncommitted (working tree vs. HEAD, including untracked files) — origin: provided
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (all five projects target net10.0; `resolved_from: csproj`)
**Checklist:** review-checklist-net10.md, plus the general security, performance, architecture, and code-quality checklists
**Tools run:** build=N · format=N · test=N — origin: provided. The calling workflow reported build 0 warnings / 0 errors and 2652 tests passed, 0 failed, 1 pre-existing skip; these numbers were not re-run by the reviewer.
**Report language:** English — origin: provided
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; the diff exceeds the large-diff gate with 68 files and 6180 changed LOC)
**Diff size:** 68 files, 6180 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 1 |
| Minor | 3 |
| Suggestion | 3 |
| Nitpick | 2 |

**Top risks:**
1. A malformed `.editorconfig` anywhere from the target file's directory up to the file-system root can abort `init` and `feature new` with an unmapped exception. This was confirmed by running the `editorconfig` 0.18.0 package: a numeric-range glob throws `OverflowException` or `OutOfMemoryException`, and neither is caught.
2. Git attribute evaluation differs from git's `convert.c` in some edge cases. A directly checked `binary` attribute overrides a later `text`, the legacy `crlf` attribute is ignored, and with `text`/`text=auto` but no `core.eol` the decision goes to `.editorconfig` even though git uses `native`. The result can be files whose line endings git will rewrite on the next checkout.
3. The line ending of the files that `feature new` writes is resolved for the hidden staging path (`.shadekit/features/.<id>.<hex>.tmp/...`), not for the final feature path. Path-specific git attributes or `.editorconfig` sections can therefore pick the wrong line ending.

**Overall:** A carefully built change. The new parts are well separated (git → `.editorconfig` → OS, one writer that encodes to bytes, immutable `TextFileContent`) and documented in full. The test suite is extensive and independent of the OS, and the `templates.lock` hash stays compatible (verified against the old CRLF→LF algorithm, including lone-CR and `\r\r\n` cases). The one Major issue is a gap in exception handling at the boundary to the third-party `.editorconfig` parser. The other findings are about how closely the code follows git's semantics, plus polish.

Verified as correct (no finding): path validation and normalisation in `RepositoryFilePath` (rooted, `..` escape, other drive on Windows, forward slashes for git); parsing of `check-attr` output from the end of the line (safe for quoted paths or paths that contain `": "`); `core.autocrlf` precedence over `core.eol`; case-sensitive `eol` attribute values and case-insensitive `core.eol`/`core.autocrlf` values (as in git); cancellation is passed on and not swallowed (`GitUnavailableException` is the only exception turned into "no decision"); every new singleton is stateless and therefore thread-safe; warnings are deduplicated by record value equality in `KitStore` and `RepositoryInitResult` and keep first-occurrence order; the CLI warning code is mapped and tested; `KitStore` uses the parent of `RootPath` as repository root, and `KitStoreFactory` always passes `<root>/.shadekit`.

## Findings — src/CreativeCoders.ShadeKit.Core/Files/EditorConfigLineEndingResolver.cs

### [Major][Code-Quality] src/CreativeCoders.ShadeKit.Core/Files/EditorConfigLineEndingResolver.cs:56
Exceptions that the `editorconfig` package throws for malformed content, not for I/O, are not caught. They abort `init` and `feature new` as an unmapped error.

`IsReadException` accepts only `IOException` and `UnauthorizedAccessException`. The reviewer ran a probe against `editorconfig` 0.18.0 (`EditorConfigParser` with the same factory signature). A section header `[{1..99999999999}]` throws `System.OverflowException`, and `[x{1..2147483647}]` throws `System.OutOfMemoryException` ("Array dimensions exceeded supported range"). A large but valid range such as `{1..50000000}` makes the parser expand a huge array. Because the parser walks every directory up to the file-system root, a broken or hostile `.editorconfig` outside the repository is enough. The exception passes through `TextFileWriter` and `KitStore`. In `init` it rolls back the kit root and then reaches `CliErrorMapper.Map`, which returns `null` for it, so the command ends as an unexpected host error instead of falling back to the OS line ending. Decision A2 asks for a fallback plus a warning when `.editorconfig` cannot be used. Treat parse failures like read failures, either with the existing warning or with a separate reason, and add a test with a malformed numeric range.

```csharp
// The errors with which reading or parsing an .editorconfig file fails. Besides I/O errors, the parser throws
// OverflowException / OutOfMemoryException ("Array dimensions exceeded supported range") for numeric-range globs
// such as [{1..99999999999}], and may throw ArgumentException / FormatException for other malformed input.
private static bool IsUnusableEditorConfigException(Exception exception)
{
    // OutOfMemoryException is included only because the parser throws it for an oversized array request before it
    // allocates anything; a real memory exhaustion would surface elsewhere first.
    return exception is IOException or UnauthorizedAccessException
        or OverflowException or ArgumentException or FormatException or OutOfMemoryException;
}

// In GetLineEnding:
catch (Exception exception) when (IsUnusableEditorConfigException(exception))
{
    return new EditorConfigLineEndingResult(null,
        new EditorConfigUnreadableWarning(unreadableEditorConfigPath ?? filePath, exception.Message));
}

// and in the factory, so the failing file is known:
catch (Exception exception) when (IsUnusableEditorConfigException(exception))
{
    unreadableEditorConfigPath = editorConfigPath;
    throw;
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:362
`feature.json` and `request.md` get the line ending for the staging path, not for the path where the feature ends up.

`CreateDirectoryExclusiveAsync` (`KitStoreDirectoryExtensions.cs:89`) passes the hidden staging directory `.shadekit/features/.<id>.<8 hex>.tmp` to `WriteFeatureFilesAsync`. `KitStore.WriteFileAsync` resolves git attributes and `.editorconfig` for that path. Rules that match the final path but not the staging name, for example an `.editorconfig` section `[.shadekit/features/gh-*/**]`, a git attribute pattern for `features/[a-z]*/request.md`, or a rule that excludes hidden directories, are not applied, or the wrong rule is. The documentation (`docs/feature-packages/03-feature-management.md:141`) says the line ending is "decided for each file". Resolve for the final path and write to the staging path. One way is an optional "resolve-as" path on `IKitStore.WriteFileAsync`/`ITextFileWriter.WriteAsync`.

```csharp
// ITextFileWriter
Task<IReadOnlyList<KitWarning>> WriteAsync(string repositoryRootPath, string relativePath, TextFileContent content,
    string? lineEndingPath = null, CancellationToken cancellationToken = default);

// TextFileWriter.WriteAsync
var resolution = await _lineEndingResolver
    .GetLineEndingAsync(repositoryRootPath, lineEndingPath ?? relativePath, cancellationToken)
    .ConfigureAwait(false);

// FeatureStore.WriteFeatureFilesAsync — resolve for the final feature folder
var finalDir = KitStorePaths.GetFeatureDirPath(feature.Id);
await store.WriteFileAsync($"{featureDir}/{KitStorePaths.RequestFileName}", TextFileContent.FromText(content),
    lineEndingPath: $"{finalDir}/{KitStorePaths.RequestFileName}", cancellationToken).ConfigureAwait(false);
```

### [Suggestion][Architecture] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:103
`CreateAsync` and `SetCurrentAsync` (line 205) return every warning the store instance has collected, not only those of the current operation.

`IKitStore.Warnings` accumulates over the lifetime of the store. A caller that runs two operations on the same store (a test, a future composite command, or `new` followed by `set`) gets the warnings of the first write again in the second result. Today each CLI command creates one store, so this does no harm yet. Taking a snapshot of the count before the operation keeps each result tied to its own operation.

```csharp
var warningCountBefore = store.Warnings.Count;

await CreateFeatureDirAsync(store, feature, identity.OriginalInput, cancellationToken).ConfigureAwait(false);
await WriteOverrideOrRemoveFeatureAsync(store, identity.Id, cancellationToken).ConfigureAwait(false);

return new NewFeatureResult(feature,
    KitWarningOrder.Arrange([.. configResult?.Warnings ?? [], .. store.Warnings.Skip(warningCountBefore)]));
```

## Findings — src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs:109
The attribute evaluation differs from git's `convert_attrs()` in three edge cases.

1. **`binary` is checked directly.** git never reads `binary` for conversion. The macro expands to `-diff -merge -text`, and only the resulting `text` state counts. With `*.x binary` followed by `*.x text`, `git check-attr` reports `text: set` and `binary: set`. git converts the file, but this code returns `null`. The `binary` check is redundant anyway, because the macro already makes `text` report `unset`.
2. **The legacy `crlf` attribute is ignored.** git still uses it when `text` is unspecified: `-crlf` means `-text`, `crlf` means `text`, and `crlf=input` means `eol=lf`. `gitattributes(5)` documents this as backwards compatibility, and old repositories still use it.
3. **`text=input`**, the legacy value, means `eol=lf` in git and is ignored here.

Fix: request `crlf` instead of `binary` and map it when `text` is unspecified.

```csharp
private static readonly string[] RequestedAttributes = [EolAttribute, TextAttribute, CrLfAttribute]; // "crlf"

var textAttribute = attributes.GetValueOrDefault(TextAttribute);

// git falls back to the legacy crlf attribute only while text is unspecified.
if (textAttribute is null or "unspecified")
{
    textAttribute = attributes.GetValueOrDefault(CrLfAttribute);
}

if (textAttribute == UnsetAttributeValue)
{
    return null; // -text, -crlf, or the binary macro
}

if (textAttribute == InputValue)
{
    return LineEnding.Lf; // text=input / crlf=input
}
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs:147
For files with `text` or `text=auto` and no `core.eol`, git decides `native`, but this code lets `.editorconfig` decide.

In git, `core.eol` defaults to `native`. `text_eol_is_crlf()` returns the native line ending when `core.eol` is unset, so every file whose `text` attribute is set or `auto` gets a decision from git. The resolver returns `null` when `git config core.eol` exits with 1, and then `.editorconfig` decides. Example: a repository with `* text=auto` and `.editorconfig` `end_of_line = crlf` on Linux. git checks files out with LF, but SHADE kit writes CRLF, and `git status` / `git add --renormalize` then fight over the file. This follows decision step 4 ("`core.eol` explicitly set"), so the owner should decide. If git is meant to be the authority, return the native line ending when `core.eol` is unset.

```csharp
var eol = await ReadConfigValueAsync(repositoryRootPath, EolKey, cancellationToken).ConfigureAwait(false);

return eol?.ToLowerInvariant() switch
{
    LfValue => LineEnding.Lf,
    CrLfValue => LineEnding.CrLf,
    null or NativeValue => _nativeLineEnding, // core.eol defaults to native for text files
    _ => null
};
```

### [Suggestion][Performance] src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs:83
Each written file starts two or three `git` processes. `init` writes about 20 files (14 templates, 3 JSON files, `.gitignore`, `CLAUDE.md`, `AGENTS.md`), so it starts about 40 to 60 `git` processes.

`core.autocrlf` and `core.eol` are the same for every file of a repository during one command, and `git check-attr` accepts several paths at once (or `--stdin`). On Windows, where starting a process costs tens of milliseconds, this adds seconds to `init`. One option is a per-call cache keyed by the repository root, held in a scoped helper so that the singleton stays stateless. Another is a batch API that `KitStore`/`RepositoryInitializer` use.

```csharp
// Scoped per command run (or passed in by the caller), keeps the singleton stateless:
internal sealed class GitConfigSnapshot(IGitCommandRunner runner, string repositoryRootPath)
{
    private readonly Lazy<Task<string?>> _autoCrLf = new(() => ReadAsync(runner, repositoryRootPath, "core.autocrlf"));
    private readonly Lazy<Task<string?>> _eol = new(() => ReadAsync(runner, repositoryRootPath, "core.eol"));

    public Task<string?> AutoCrLf => _autoCrLf.Value;
    public Task<string?> Eol => _eol.Value;
    // ...
}
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Git/GitLineEndingResolver.cs:50
`TrueConfigValues` accepts only `"1"` as a numeric true. `git_config_bool` treats every non-zero integer as true (`autocrlf = 2`, `-1`).

```csharp
private static bool IsGitTrue(string value)
{
    return TrueConfigValues.Contains(value, StringComparer.OrdinalIgnoreCase)
           || (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
               && number != 0);
}
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs:277
The warning message often repeats the path. `Reason` is the message of an `UnauthorizedAccessException` ("Access to the path '/r/.editorconfig' is denied."), which already names the path, so the message reads "The .editorconfig file '/r/.editorconfig' cannot be read: Access to the path '/r/.editorconfig' is denied. …". An empty `Reason` would give "cannot be read: . The …".

```csharp
EditorConfigUnreadableWarning w => new CliJsonMessage(ShadeKitCliWarningCodes.EditorConfigUnreadable,
    $"The .editorconfig file '{w.FilePath}' cannot be read" +
    (string.IsNullOrWhiteSpace(w.Reason) ? "." : $" ({w.Reason.TrimEnd('.')}).") +
    " The .editorconfig files were ignored and the line ending of the operating system was used instead."),
```

## Findings — tests/CreativeCoders.ShadeKit.Core.Tests/Files/EditorConfigLineEndingResolverTests.cs

### [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Files/EditorConfigLineEndingResolverTests.cs:310
On Windows, the whole path that leads to the `editorconfig-unreadable` warning is never tested. Every unreadable-file test skips there (`Assert.SkipWhen(OperatingSystem.IsWindows(), …)`), and the broken-symlink test skips too. CI also runs on Windows. On Windows an exclusive open lock makes the file unreadable without Unix file modes (`IOException` with a sharing violation). That would cover the warning path on all three CI platforms. Also add a case for a malformed glob (see the Major finding).

```csharp
// Windows: hold the file open exclusively for the duration of the test.
using var exclusiveLock = OperatingSystem.IsWindows()
    ? new FileStream(editorConfigPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
    : null;
using var unreadableScope = OperatingSystem.IsWindows() ? null : new UnreadableFileScope(editorConfigPath);

[Fact]
public void GetLineEnding_EditorConfigWithOverflowingNumericRange_ReturnsNoLineEndingAndWarning()
{
    _directory.WriteFile("root = true\n\n[{1..99999999999}]\nend_of_line = crlf\n", EditorConfigFileName);

    var result = _sut.GetLineEnding(GetPath("readme.md"));

    result.LineEnding.Should().BeNull();
    result.Warning.Should().NotBeNull();
}
```

## Tool Output Appendix

### dotnet build
- Skipped (tools not requested). The calling workflow reported 0 warnings and 0 errors.

### dotnet test
- Skipped (tools not requested). The calling workflow reported 2652 passed, 0 failed, 1 pre-existing skip.

### dotnet format
- Skipped (tools not requested).

### Reviewer probe (not a tool run)
- `dotnet run` scratch app against `editorconfig` 0.18.0 with malformed section headers: `[{1..99999999999}]` → `System.OverflowException`; `[x{1..2147483647}]` → `System.OutOfMemoryException`. Other malformed headers (`[a\]`, `[{a..b}]`, `[[!]]`, `[(]`, unclosed braces) were tolerated. This was folded into the Major finding.
