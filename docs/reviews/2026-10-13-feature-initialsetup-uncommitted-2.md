# .NET Code Review — feature/initialsetup (uncommitted)

**Date:** 2026-10-13
**Round:** 2 (re-review after rework; round 1: `docs/reviews/2026-10-13-feature-initialsetup-uncommitted.md`)
**Mode:** uncommitted (working tree vs. HEAD, incl. staged/unstaged/untracked) — origin: provided
**Detected SDK:** 10.0.401 (installed; `global.json` pins no SDK)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props — `detect-dotnet-version.sh` exited 5 ("malformed global.json") because `global.json` deliberately contains only `test.runner` and no `sdk` section. The file is valid JSON, so, as in round 1, the target framework was taken from `<TargetFramework>net10.0</TargetFramework>` in `Directory.Build.props` (non-interactive run, no user to ask).
**Checklist:** review-checklist-net10.md (+ security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (all provided; build with analyzers + `TreatWarningsAsErrors` and 67/67 tests reported green in Phase 4)
**Report language:** English — origin: provided
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**; additionally `docs/reviews/2026-10-13-feature-initialsetup-uncommitted.md` (round-1 report, excluded by instruction)
**Review strategy:** full (diff below the large-diff thresholds; gate not triggered)
**Diff size:** 32 files, ~1583 changed LOC (the collector reported 33 files / 1788 LOC including the excluded 205-line round-1 report)

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 0 |
| Suggestion | 2 |
| Nitpick | 2 |

**Top risks:**
1. `ExecuteAsync_Called_WritesToolNameAndVersionWithoutBuildMetadata` derives its expected value from `AssemblyVersion.ToString(3)`. It fails as soon as the project gets a prerelease or four-part `<Version>`, for example from the planned release workflow.
2. The "features directory missing" abort message drops the `FeaturesPath` that the new domain exception now carries. With nested kit roots, the user cannot see which `.shadekit` was picked.
3. Small style leftovers: an uncommented `!` in `VersionCommand.GetVersion`, and trailing whitespace in a touched file that the new `.editorconfig` would now trim.

**Overall:** All six round-1 findings are fixed correctly, and the new code comes with targeted tests: the domain exception and its propagation rules, the faulted task, the build-metadata edge cases, and the skip guard. No Critical, Major, or Minor findings remain. The open items are optional refinements.

### Round-1 findings — verification

| # | Round-1 finding | Status | Evidence |
|---|---|---|---|
| 1 | [Minor] machine-dependent "root not found" tests | **Fixed** | **FACT:** `TempKitDirectory.SkipWhenKitRootExistsAbove()` (`TempKitDirectory.cs:32`) calls `Assert.SkipWhen` and walks every ancestor of the temp directory. It is called in all 4 not-found tests (`KitStoreFactoryTests.cs:67,103,159`, `KitStoreFactoryCurrentDirectoryTests.cs:42`). **FACT:** `Assert.SkipWhen` exists in `xunit.v3.assert` 4.0.2 (package XML docs). The guard is a superset of the factory's 20-level search, so it never under-skips. In the twenty-levels test it is even redundant, because the search never gets above the temp root. That is harmless. |
| 2 | [Suggestion] BCL `DirectoryNotFoundException` read as "features missing" | **Fixed** | **FACT:** `FeaturesDirectoryNotFoundException` exposes `FeaturesPath` and puts it in the message. `KitStore.GetFeatureNames` checks `Directory.Exists` first and then throws it (`KitStore.cs:19-22`). `IKitStore` documents it with `<exception>`. `FeaturesListCommand` catches only the domain exception (`FeaturesListCommand.cs:68`). Tests cover eager and lazy throws, and confirm that BCL `DirectoryNotFoundException`, `IOException`, and `UnauthorizedAccessException` propagate unwrapped. |
| 3 | [Suggestion] synchronous throw from `ExecuteAsync` | **Fixed** (as decided: `async` without `await`) | **FACT:** `FeaturesListCommand.ExecuteAsync` is `async` (`FeaturesListCommand.cs:41`). `ExecuteAsync_KitRootNotFound_ReturnsFaultedTaskInsteadOfThrowingSynchronously` checks that the call does not throw and that the task is faulted with `CliCommandAbortException`. That no CS1998 is emitted comes from the Phase-4 build report; it was not re-verified here (build=no). |
| 4 | [Suggestion] full commit SHA in version output | **Fixed** | **FACT:** `VersionCommand.GetVersion` cuts at the first `+` and falls back to the assembly version when the result is empty (`VersionCommand.cs:48-56`). The theory covers `+sha`, prerelease+metadata, no metadata, trailing `+`, double `+`, empty, and leading `+`. See the new Suggestion on the end-to-end test below. |
| 5 | [Suggestion] no analyzers | **Fixed** | **FACT:** `Directory.Build.props:7-9` sets `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, and `TreatWarningsAsErrors=true`. **FACT:** `tests/.editorconfig` disables only CA1707, and only for test sources. **FACT:** both collection classes are named `CurrentDirectoryCollectionDefinition`, and both `[Collection]` usages reference `.Name`. **FACT:** the SDK applies `analysislevelstyle_recommended.globalconfig` via `AnalysisLevelStyle` (which defaults to `AnalysisLevel`), so `EnforceCodeStyleInBuild` does enforce IDE rules and is not a no-op. |
| 6 | [Nitpick] BOM / encoding | **Fixed** | **FACT:** the root `.editorconfig` sets utf-8, lf, final newline, space indentation, and trim of trailing whitespace. **FACT:** none of the 32 changed or new files has a BOM, CRLF line endings, or a missing final newline. |

## Findings

### [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Cli.Tests/Commands/Version/VersionCommandTests.cs:17
The end-to-end version test builds its expected value from `AssemblyVersion.ToString(3)`. That holds only while `<Version>` is a plain three-part number.

**FACT:** The SDK derives `AssemblyVersion` from `$(Version)` without its prerelease label (`GetAssemblyVersion` task, `Microsoft.NET.GenerateAssemblyInfo.targets`), while `InformationalVersion` is `$(Version)` plus `+<SourceRevisionId>`. So for `-p:Version=1.2.0-beta.1` the command correctly prints `shade-kit 1.2.0-beta.1` but the test expects `shade-kit 1.2.0`. For a four-part `Version=1.2.3.4` it expects `1.2.3` but gets `1.2.3.4`. The release workflow in `CLAUDE.md` is still *(planned)*, and it will most likely set such versions. When it does, this test will fail even though the code is correct. The edge cases are already covered by the `GetVersion_InformationalVersion_…` theory, so this test only needs to prove that the command prints the tool name and the informational version without build metadata.

```csharp
[Fact]
public async Task ExecuteAsync_Called_WritesToolNameAndVersionWithoutBuildMetadata()
{
    // Arrange
    var informationalVersion = typeof(VersionCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
        .InformationalVersion;
    var expectedVersion = informationalVersion.Split('+', 2)[0];

    var console = new TestConsole();
    var command = new VersionCommand(console);

    // Act
    await command.ExecuteAsync();

    // Assert
    console.Output.TrimEnd()
        .Should().Be($"shade-kit {expectedVersion}")
        .And.NotContain("+");
}
```

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Commands/Features/List/FeaturesListCommand.cs:72
The abort message for a missing `features` directory does not name the directory, even though `FeaturesDirectoryNotFoundException.FeaturesPath` now provides it.

The kit-root-not-found message names the directory it searched from (`FeaturesListCommand.cs:87-88`). The features message only says "the SHADE kit root directory". `KitStoreFactory` picks the *closest* `.shadekit` (covered by `Create_NestedKitRoots_ReturnsStoreOfClosestKitRoot`), so with nested kits the user cannot tell which one is missing `features`. Using the path the exception already carries makes the error actionable and keeps the two messages consistent. `Markup.Escape` is already applied to the whole string, so a path containing `[` or `]` stays safe.

```csharp
catch (FeaturesDirectoryNotFoundException e)
{
    throw new CliCommandAbortException(
        Markup.Escape(
            $"The SHADE kit root directory contains no '{KitStorePaths.FeaturesDirName}' directory " +
            $"(expected at '{e.FeaturesPath}')."),
        ShadeKitCliExitCodes.FeaturesDirectoryNotFound, e);
}
```

In `FeaturesListCommandTests.ExecuteAsync_FeaturesDirectoryNotFound_…`, also assert `exception.Message.Should().Contain("/kit/.shadekit/features")`.

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Commands/Version/VersionCommand.cs:52
`informationalVersion!` uses the null-forgiving operator without a comment. The code is correct: `metadataSeparatorIndex >= 0` implies a non-null string. But the compiler cannot prove that, and the code-quality checklist asks for either a comment or a shape that does not need `!`. A pattern-based form needs neither and is shorter.

```csharp
internal static string GetVersion(Assembly assembly)
{
    var informationalVersion = assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion;

    var version = informationalVersion?.Split('+', 2)[0];

    return string.IsNullOrEmpty(version)
        ? assembly.GetName().Version?.ToString() ?? string.Empty
        : version;
}
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Store/KitStoreFactory.cs:25
**FACT:** Lines 25, 27, and 45 of this file, which this change set modifies, contain whitespace-only lines with trailing spaces, as do lines 6, 8, and 10 of the unchanged `KitStorePaths.cs`. **FACT:** The unchanged `IKitStoreFactory.cs`, `KitRootPathNotFoundException.cs`, and `KitStorePaths.cs` have no final newline. The new root `.editorconfig` (`trim_trailing_whitespace`, `insert_final_newline`) will now rewrite these lines the next time each file is saved, so later, unrelated diffs pick up noise. Normalizing once, in this change set or in a separate `chore:` commit, keeps future diffs clean. This is formatting only and does not touch the pre-existing `KitStoreFactory` behaviors that are out of scope.

```csharp
// KitStoreFactory.cs — whitespace-only lines become empty lines
const int maxRecursionDepth = 20;

var recursionDepth = 0;

while (recursionDepth++ < maxRecursionDepth)
```

(`dotnet format whitespace ShadeKit.slnx` applies the `.editorconfig` rules to all files at once.)

## Tool Output Appendix

### dotnet build
- Skipped (build=no; reported clean with `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, and `TreatWarningsAsErrors` in Phase 4).

### dotnet test
- Skipped (test=no; reported 67/67 passed in Phase 4).

### dotnet format
- Skipped (format=no).

### Other checks
- `scripts/detect-dotnet-version.sh`: exit 5, "malformed global.json". This is a known artefact of the detector expecting an `sdk` section. The target framework was resolved from `Directory.Build.props` (see metadata).
- Read-only verifications: BOM/CRLF/final-newline scan of all changed files; `Assert.SkipWhen` lookup in the `xunit.v3.assert` 4.0.2 package; SDK `codestyle` targets and `analysislevelstyle_recommended.globalconfig` (code-style enforcement); SDK `GetAssemblyVersion` target (version derivation).
