# .NET Code Review — feature/cli-init-repo (uncommitted)

**Date:** 2026-10-18
**Mode:** uncommitted (provided) — working tree vs. HEAD, including staged `git mv` renames and untracked files
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (all five project files target net10.0; SDK pinned in global.json)
**Checklist:** review-checklist-net10.md (+ security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (all provided as `no`; the caller reports build and full test suite green: 0 warnings, 757/757 tests)
**Report language:** English (provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; large-diff gate triggered: 98 files > 50, 7650 LOC > 2000)
**Diff size:** 98 files, 7650 changed LOC (incl. Markdown docs, skills, and templates; the C# part is about 45 files)
**Review context:** requirement `docs/feature-packages/02-repository-initialisation.md`; the user's binding decisions from the FP02 implementation brief. Decided behaviour (no rollback on IO failures, removal of exit 11, missing `features/` counts as empty, template sources in `Core/Templates/Resources`, replace-between-markers, trimmed-line rules) is not reported as a defect; risks within those decisions are labelled as such.

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 1 |
| Suggestion | 5 |
| Nitpick | 5 |

**Top risks:**
1. `TextFileContent` detects only a UTF-8 BOM. `init` appends UTF-8 bytes to a UTF-16 `.gitignore`, `CLAUDE.md`, or `AGENTS.md` and reports success, which leaves a file with mixed encodings. Verified by running the built CLI against a UTF-16LE `.gitignore`. A UTF-16LE `.gitignore` is what Windows PowerShell 5.1 `>` produces.
2. A `CLAUDE.md`, `AGENTS.md`, or `.gitignore` that exists as a directory passes the preconditions, so `init` fails only after `.shadekit/` is written. Re-running `init` then stops with exit 13. Partial writes after a failure are a decided behaviour, but this failure is predictable and could be caught before the first write.
3. A cancellation after the preconditions leaves an empty `.shadekit/` behind, which also blocks the next `init` with exit 13. A test codifies this behaviour and depends on the order of internal calls.

**Overall:** A carefully built change. Byte-level file handling keeps every byte outside the changed part, CRLF and the BOM are preserved, and the result is idempotent. The order "validate, then write" holds for every precondition the requirement names. The agent contract (JSON envelope, exit codes 12, 13, and 23, `CliErrorMapper`-only mapping) is followed and covered by unit and end-to-end tests, and every FP02 acceptance criterion has a test with a matching trait. The findings are edge cases and cleanups; nothing blocks the merge.

**Verified facts behind the findings:**
- **FACT:** With a UTF-16LE `.gitignore` (`FF FE 62 00 69 00 6E 00 2F 00 0D 00 0A 00`), `shade-kit init --json` returns `ok: true` and `.gitignore: appended`. The file then continues with plain UTF-8 bytes `0A 0A 23 20 53 48 41 44 45 …` (`# SHADE kit: …`). This was reproduced with `src/CreativeCoders.ShadeKit.Cli/bin/Debug/net10.0/shade-kit.dll` in a scratch repository.
- **FACT:** With `CLAUDE.md` = `# Proj\n\n`, the result starts with `# Proj\n\n\n<!-- shade-kit:begin`, so there are two blank lines before the section (same run).
- **FACT:** With `AGENTS.md` as a symlink to `CLAUDE.md`, `init` appends the section to `CLAUDE.md` once and leaves `AGENTS.md` unlisted. The symlink is kept and the file contains exactly one begin marker. The behaviour is correct but untested.
- **FACT:** `RepositoryInitializerTests.InitializeAsync_TokenCanceledAfterPreconditions_…` enumerates `.shadekit/` after the cancellation. The enumeration succeeds only if the directory exists, so the test asserts that an empty `.shadekit/` stays behind.

**Known open points (from FP02 "Open points", not counted above):** plain output wraps at 80 columns when piped; the shared FP01 enum converter accepts comma-separated values; the skeleton templates name their owner package; and a second `.gitignore` comment line is added when the comment exists but an entry is missing.

**AC coverage (checked):** FP02-AC1 to FP02-AC8 each have at least one test with `[Trait("Requirement", "FP02-ACn")]` in `RepositoryInitializerTests`, `InitCommandTests`, `InitCommandEndToEndTests`, `GitIgnoreWriterTests`, `InstructionSectionWriterTests`, and `CliErrorMapperTests`. FP02-AC8 is also covered by `ShippedTemplatesTests.All_Fp02Ac8Examples_MapToRepositoryPathFromTemplateTable`. The same test class checks that the catalog and the embedded resources match one to one, which also guards the Windows `%(RecursiveDir)` resource-name normalisation when CI runs on Windows.

## Findings — src/CreativeCoders.ShadeKit.Core/Init/TextFileContent.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Init/TextFileContent.cs:39
Only a UTF-8 BOM is recognised. A UTF-16 or UTF-32 file is still split and appended to as if it were ASCII-compatible, and the inserted text is UTF-8.

For a UTF-16LE file, `IndexOf("\r\n"u8)` finds no match because the CRLF is stored as `0D 00 0A 00`. No line matches an entry or a marker, and `AppendBlock` writes UTF-8 bytes after the UTF-16 content. `init` reports `appended` with exit 0, and the file now mixes two encodings (verified, see the summary). The class remark is literally correct ("any encoding that is compatible with ASCII line terminators"), but nothing rejects encodings that are not. The test `ReplaceLines_InvalidUtf8OutsideReplacedLines_KeepsBytes` even uses `FF FE` as its "invalid UTF-8" prefix, so this path is exercised without being flagged. Detect UTF-16 and UTF-32 BOMs and refuse the file. Do the check in the `RepositoryInitializer` preconditions for all three target files, so that nothing is written. This is not a rollback; it only adds a check before the first write.

```csharp
// TextFileContent
private static ReadOnlySpan<byte> Utf16LeBom => [0xFF, 0xFE];
private static ReadOnlySpan<byte> Utf16BeBom => [0xFE, 0xFF];

public bool HasUnsupportedEncoding =>
    _bytes.AsSpan().StartsWith(Utf16LeBom) || _bytes.AsSpan().StartsWith(Utf16BeBom); // also covers UTF-32 LE (FF FE 00 00)

// RepositoryInitializer.InitializeAsync, inside the precondition block, for .gitignore, CLAUDE.md, AGENTS.md:
if (await TextFileContent.ReadAsync(targetPath, cancellationToken).ConfigureAwait(false)
        is { HasUnsupportedEncoding: true })
{
    throw new UnsupportedTextFileEncodingException(targetPath); // mapped in CliErrorMapper (new exit code in 20–29)
}
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Init/TextFileContent.cs:113
`AppendBlock` always adds a blank line, even when the file already ends with one, so the result has two blank lines before the appended block (verified: `# Proj\n\n` becomes `# Proj\n\n\n<!-- shade-kit:begin …`). The rule "one blank line before it" is then not met literally. Skip the separator when the last line is already empty.

```csharp
if (_bytes.Length > _textStart)
{
    if (_bytes[^1] != (byte)'\n')
    {
        text.Append(NewLine);
    }
    else if (Lines.Count > 0 && Lines[^1].Text.Length == 0)
    {
        // The file already ends with a blank line.
        return [.. _bytes, .. Utf8WithoutBom.GetBytes(ToFileLineEndings(block) + NewLine)];
    }

    text.Append(NewLine);
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Init/RepositoryInitializer.cs

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Init/RepositoryInitializer.cs:69
The preconditions miss a write failure that can be predicted. When `CLAUDE.md`, `AGENTS.md`, or `.gitignore` exists as a directory, `File.Exists` is `false`, so `ValidateAsync` treats it as a missing (valid) file. The write fails only after `.shadekit/` is complete, and the next `init` stops with exit 13 `already-initialized`. `InitializeAsync_LastWriteFails_KeepsFilesWrittenBefore` documents this. No rollback is the user's decision; checking these paths before the first write keeps that decision and makes this failure behave like the other precondition errors (nothing written).

```csharp
foreach (var fileName in (string[])[GitIgnoreFileName, .. InstructionFileNames])
{
    var targetPath = Path.Combine(repositoryRootPath, fileName);

    if (Directory.Exists(targetPath))
    {
        throw new InitTargetNotAFileException(targetPath); // mapped in CliErrorMapper
    }
}
```

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Init/RepositoryInitializer.cs:88
`Directory.CreateDirectory(kitRootPath)` runs before the first cancellable write. A token cancelled between the preconditions and `WriteJsonAsync` therefore leaves an empty `.shadekit/`, and the next `init` reports "already initialised". The explicit `CreateDirectory` is redundant: `KitStore.WriteJsonAsync` creates the parent directory (`KitStore.cs:120`), and `IKitStoreFactory.CreateForKitRoot` is documented not to require an existing directory. The test `RepositoryInitializerTests.cs:647` codifies the empty directory, and its arrangement depends on `Version` being the first call after the preconditions. That is an internal ordering detail. Check the token before the first write, drop the redundant call, and assert `Path.Exists(.shadekit)` is `false` in the test.

```csharp
private async Task WriteKitRootAsync(string repositoryRootPath, string kitRootPath, List<InitializedFile> files,
    CancellationToken cancellationToken)
{
    var kitVersion = _engineVersionProvider.Version;

    cancellationToken.ThrowIfCancellationRequested();

    var kitStore = _kitStoreFactory.CreateForKitRoot(kitRootPath); // WriteJsonAsync creates .shadekit/
    // ...
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Init/GitIgnoreWriter.cs

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Init/GitIgnoreWriter.cs:59
This is a risk inside a decided rule. FP02 says a `.gitignore` entry is present when the *trimmed* line equals it, but git treats leading whitespace as part of the pattern and ignores only trailing unescaped spaces. A line `  .shadekit/local.json` therefore counts as present, `init` appends nothing, and git still does not ignore `local.json`. If the user agrees, trim only the end and update the FP02 rule text to match.

```csharp
private static bool IsEntryLine(string lineText, string entry)
{
    // git keeps leading whitespace as part of the pattern; only trailing spaces are ignored.
    var trimmedText = lineText.TrimEnd();

    return trimmedText == entry || trimmedText == "/" + entry;
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Init/IGitIgnoreWriter.cs

### [Suggestion][Architecture] src/CreativeCoders.ShadeKit.Core/Init/IGitIgnoreWriter.cs:9
The interface contracts are defined by static members of one implementation. `IGitIgnoreWriter` refers to `GitIgnoreWriter.Entries`, and `IInstructionSectionWriter` (`IInstructionSectionWriter.cs:9`, `:30`) refers to `InstructionSectionWriter.Section`, `BeginMarkerPrefix`, and `EndMarker`. `CliErrorMapper` also builds its message from `InstructionSectionWriter` constants. Package 18 reuses these writers, and a second implementation, or a decorator for upgrade, would have to reference the first implementation's statics. Move the shared contract values into small static classes next to the interfaces, and keep the implementations as consumers.

```csharp
namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>Defines the managed SHADE kit section in agent instruction files.</summary>
public static class ManagedInstructionSection
{
    public const string BeginMarkerPrefix = "<!-- shade-kit:begin";
    public const string EndMarker = "<!-- shade-kit:end -->";
    public const string Text = BeginMarkerPrefix + " - managed by shade-kit, …" + EndMarker;
}

/// <summary>Defines the .gitignore entries for the local-only files of SHADE kit.</summary>
public static class KitGitIgnoreEntries
{
    public static IReadOnlyList<string> All { get; } = [".shadekit/local.json", ".shadekit/features/current-feature.json"];
}
```

## Findings — tests/CreativeCoders.ShadeKit.Core.Tests/Init/RepositoryInitializerTests.cs

### [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Init/RepositoryInitializerTests.cs:333
A common setup, `AGENTS.md` as a symlink to `CLAUDE.md`, has no test. It works today: one section, the second file not listed, the symlink kept (verified manually). But this depends on `ApplyAsync` re-reading the file after the first write. A future change, such as reading both files once during validation and reusing that content, would silently write the section twice. Add a regression test and skip it where symlinks cannot be created, for example on Windows without the privilege.

```csharp
[Fact]
[Trait("Requirement", "FP02-AC5")]
public async Task InitializeAsync_AgentsMdIsSymlinkToClaudeMd_WritesExactlyOneSectionAndKeepsSymlink()
{
    // Arrange
    using var tempDirectory = new TempKitDirectory();
    tempDirectory.CreateGitRoot();
    tempDirectory.WriteFile("# Project\n", ClaudeMd);
    var agentsPath = Path.Combine(tempDirectory.RootPath, AgentsMd);
    File.CreateSymbolicLink(agentsPath, ClaudeMd);

    // Act
    var result = await CreateInitializer().InitializeAsync(tempDirectory.RootPath, TestContext.Current.CancellationToken);

    // Assert
    Regex.Count(tempDirectory.ReadFileText(ClaudeMd), "<!-- shade-kit:begin")
        .Should().Be(1);

    new FileInfo(agentsPath).LinkTarget
        .Should().Be(ClaudeMd);

    result.Files.Select(x => x.Path)
        .Should().Contain(ClaudeMd).And.NotContain(AgentsMd);
}
```

### [Nitpick][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Init/RepositoryInitializerTests.cs:164
The FP02-AC2 test asserts inside a `foreach` loop. The checklist flags loops in test bodies, and a failure report names only the first failing file. Parse the files first and use `AllSatisfy`.

```csharp
var roots = await Task.WhenAll(jsonFiles.Select(async x =>
    (File: x, Root: JsonNode.Parse(await File.ReadAllTextAsync(x, TestContext.Current.CancellationToken))!)));

roots.Should().AllSatisfy(x =>
{
    x.Root["schemaVersion"]!.GetValue<int>().Should().Be(1, x.File);
    x.Root["minEngineVersion"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace(x.File);
});
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Commands/Feature/List/FeatureListCommand.cs

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Commands/Feature/List/FeatureListCommand.cs:47
After the edit, one XML doc line is 166 characters long. The surrounding docs wrap at about 120. Rewrap the paragraph.

```csharp
/// envelope whose <c>data</c> is a <see cref="FeatureListCommandData" />; a store without features, also one
/// without a <c>features</c> directory, is a result, not an error. In JSON mode, an expected failure is written as
/// an error envelope and the abort message is not printed.
```

## Findings — src/CreativeCoders.ShadeKit.Core/Store/IGitRepositoryLocator.cs

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Store/IGitRepositoryLocator.cs:17
`<returns>` says the path has no trailing directory separator. `Path.TrimEndingDirectorySeparator` keeps the separator of a root directory, so a repository at `/` or `C:\` is returned with one. Make the doc precise.

```csharp
/// <returns>
/// The full path of the repository root, without a trailing directory separator unless the root is a file system
/// root such as <c>/</c> or <c>C:\</c>.
/// </returns>
```

## Findings — src/CreativeCoders.ShadeKit.Core/Templates/IShippedTemplateReader.cs

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Templates/IShippedTemplateReader.cs:14
The `<exception cref="InvalidOperationException">` text says "The template source is not part of the shipped templates". `ShippedTemplateReader` does not check catalog membership; it throws only when no embedded resource `templates/<ResourcePath>` exists. Describe the real condition.

```csharp
/// <exception cref="InvalidOperationException">
/// The engine contains no embedded resource for <see cref="ShippedTemplate.ResourcePath" />.
/// </exception>
```

## Tool Output Appendix

### dotnet build
- Skipped (tools: build=no, provided). The caller reports 0 warnings and 0 errors.

### dotnet test
- Skipped (tools: test=no, provided). The caller reports 757/757 passed.

### dotnet format
- Skipped (tools: format=no, provided).

### Manual verification
- Ran the existing Debug build `src/CreativeCoders.ShadeKit.Cli/bin/Debug/net10.0/shade-kit.dll init` in two scratch repositories outside the project: a UTF-16LE `.gitignore` with `CLAUDE.md` ending in a blank line, and `AGENTS.md` as a symlink to `CLAUDE.md`. The results are cited in the findings above. The scratch repositories were deleted afterwards.
