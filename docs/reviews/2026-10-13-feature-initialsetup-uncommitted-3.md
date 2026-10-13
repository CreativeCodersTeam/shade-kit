# .NET Code Review — feature/initialsetup (uncommitted)

**Date:** 2026-10-13
**Round:** 3 (re-review after rework; round 1: `docs/reviews/2026-10-13-feature-initialsetup-uncommitted.md`, round 2: `docs/reviews/2026-10-13-feature-initialsetup-uncommitted-2.md`)
**Mode:** uncommitted (working tree vs. HEAD, incl. staged/unstaged/untracked) — origin: provided
**Detected SDK:** 10.0.401 (installed `dotnet --version`; `global.json` pins no SDK)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props — `detect-dotnet-version.sh` exited 5 ("malformed global.json") because `global.json` deliberately contains only `test.runner` and no `sdk` section. The file is valid JSON, so, as in rounds 1 and 2, the target framework was taken from `<TargetFramework>net10.0</TargetFramework>` in `Directory.Build.props:4` (non-interactive run, no user to ask).
**Checklist:** review-checklist-net10.md (+ security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (all provided; build with analyzers + `TreatWarningsAsErrors` and 67/67 tests reported green in Phase 4)
**Report language:** English — origin: provided
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**; additionally `docs/reviews/2026-10-13-feature-initialsetup-uncommitted.md` and `docs/reviews/2026-10-13-feature-initialsetup-uncommitted-2.md` (round-1 and round-2 reports, excluded by instruction)
**Review strategy:** full (35 in-scope files / 1601 LOC, below the large-diff thresholds; gate not triggered)
**Diff size:** 35 files, 1601 changed LOC (the collector reported 37 files / 1943 LOC including the two excluded reports)

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 0 |
| Suggestion | 0 |
| Nitpick | 3 |

**Top risks:**
1. None of the open items affects behavior. The `<value>` doc of `FeaturesDirectoryNotFoundException.FeaturesPath` promises a full path, but `KitStore` passes a relative path through unchanged.
2. `IKitStore` / `IKitStoreFactory`, the Core contracts the CLI depends on, have no type-level XML docs, while the rest of the new public surface is documented.
3. One verbose array initializer remains in a test (`new[] { "features" }`), where a list pattern is shorter.

**Overall:** All four round-2 findings are fixed correctly, and all round-1 fixes still hold. The change set has no Critical, Major, Minor, or Suggestion findings. The three Nitpicks are optional. The change set is ready to commit from a review standpoint.

### Round-2 findings — verification

| # | Round-2 finding | Status | Evidence |
|---|---|---|---|
| A | [Suggestion] `VersionCommandTests` expected `AssemblyVersion.ToString(3)` | **Fixed** | **FACT:** `ExecuteAsync_Called_WritesToolNameAndVersionWithoutBuildMetadata` (`VersionCommandTests.cs:13-31`) takes its expected value from `VersionCommand.GetVersion(typeof(VersionCommand).Assembly)`, checks the console output against `shade-kit {expectedVersion}`, and asserts that the value is not empty and contains no `+`. Nothing ties the test to `AssemblyVersion` any more, so prerelease or four-part `<Version>` values no longer break it. The stripping logic is covered with fixed literals by the theory `GetVersion_InformationalVersion_ReturnsVersionWithoutBuildMetadata` (`VersionCommandTests.cs:33-59`: `+sha`, prerelease + metadata, none, trailing `+`, double `+`, empty, leading `+`), and the missing-attribute fallback by `GetVersion_AssemblyWithoutInformationalVersion_ReturnsAssemblyVersion`. One limit remains: because the end-to-end expectation comes from `GetVersion` itself, that test alone would not notice if `GetVersion` returned the assembly version. The theory covers that case, so this is acceptable. |
| B | [Suggestion] features-missing abort message lacked the path | **Fixed** | **FACT:** `FeaturesListCommand.cs:70-74` builds the message as `"... contains no 'features' directory ('{e.FeaturesPath}')."` and wraps the whole string in `Markup.Escape`. **FACT:** `ExecuteAsync_FeaturesDirectoryNotFound_…` (`FeaturesListCommandTests.cs:110-144`) uses `/kit/[nested]/.shadekit/features` and asserts `'/kit/[[nested]]/.shadekit/features'`, which proves that both the path and its escaping reach the message. Exit code, `IsError`, inner exception, and empty console output are still asserted. |
| C | [Nitpick] uncommented `informationalVersion!` | **Fixed** | **FACT:** `VersionCommand.cs:44-51` has no null-forgiving operator. The `?.` chain (`GetCustomAttribute<…>()?.InformationalVersion.Split('+', 2)[0]`) yields `null` when the attribute is missing, and `string.IsNullOrEmpty(version)` falls back to `assembly.GetName().Version?.ToString() ?? string.Empty`. The `<remarks>` on `ExecuteAsync` (`VersionCommand.cs:29-33`) describe this behavior correctly. |
| D | [Nitpick] trailing whitespace / missing final newline in Core files | **Fixed** | **FACT:** the `git diff HEAD` of `KitStoreFactory.cs`, `KitStorePaths.cs`, `IKitStoreFactory.cs`, `KitRootPathNotFoundException.cs` (and `KitStore.cs`, `IKitStore.cs`) contains only whitespace-only-line → empty-line and final-newline changes in the formerly affected spots. No logic changed, so the out-of-scope `KitStoreFactory` behaviors are untouched. **FACT:** a byte-level scan of all 35 in-scope files finds no trailing whitespace, no missing final newline, no BOM, and no CR characters. |

Round-1 fixes re-checked in passing, all still in place: the `SkipWhenKitRootExistsAbove` guard in all four not-found tests, catching the domain exception only, `async` `ExecuteAsync` with the faulted-task test, the analyzer settings in `Directory.Build.props:7-9` with CA1707 off only in `tests/.editorconfig`, and the encoding rules in `.editorconfig`.

## Findings

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Store/FeaturesDirectoryNotFoundException.cs:13
The `<value>` doc says `FeaturesPath` is "the full path of the missing `features` directory". The code does not guarantee that.

**FACT:** `KitStore` stores its constructor argument unchanged after a whitespace check (`KitStore.cs:9-12`). It builds the path with `Path.Combine(_kitStorePath, "features")` (`KitStore.cs:33-36`) and passes that to the exception (`KitStore.cs:21`). `KitStore` is public, so `new KitStore("relative/.shadekit")` produces a relative `FeaturesPath`. The CLI path through `KitStoreFactory.Create()` starts from `Directory.GetCurrentDirectory()` and is therefore absolute in practice, so this is a documentation inaccuracy only. Since round 2, this value appears in the user-facing abort message. Fix the doc wording, which leaves the out-of-scope factory behaviors alone, rather than the code.

```csharp
/// <summary>
/// Gets the path of the <c>features</c> directory that was not found.
/// </summary>
/// <value>
/// The path of the missing <c>features</c> directory, composed from the store path passed to <see cref="KitStore" />.
/// </value>
public string FeaturesPath { get; } = featuresPath;
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Store/IKitStore.cs:3
`IKitStore` gained member docs in this change set but still has no type-level `<summary>`, and `IKitStoreFactory` (`IKitStoreFactory.cs:3-8`) has none at all.

Every other public type this change set adds or extends is documented: `ShadeKitCliExitCodes`, both commands, `FeaturesCommandGroup`, `ShadeKitCoreServiceCollectionExtensions`, and `FeaturesDirectoryNotFoundException`. The two Core interfaces are the contracts the CLI is written against, and `FeaturesListCommand` documents its behavior in terms of them. The missing pieces are not obvious from the names. `Create()` searches upward from the current directory. `Create(string)` searches upward from `path`, and both throw `KitRootPathNotFoundException`. Today that knowledge exists only in `FeaturesListCommand`'s remarks and in the tests. This is optional. The implementing classes (`KitStore`, `KitStoreFactory`, `KitStorePaths`, `KitRootPathNotFoundException`) can follow in a later change.

```csharp
/// <summary>
/// Provides read access to a SHADE kit store, the <c>.shadekit</c> directory of a repository.
/// </summary>
public interface IKitStore
{
    // existing member docs unchanged
}

/// <summary>
/// Locates the SHADE kit root directory and creates an <see cref="IKitStore" /> for its <c>.shadekit</c> directory.
/// </summary>
public interface IKitStoreFactory
{
    /// <summary>
    /// Creates the store of the closest SHADE kit root at or above the current directory.
    /// </summary>
    /// <returns>The store of the closest SHADE kit root.</returns>
    /// <exception cref="KitRootPathNotFoundException">No SHADE kit root directory is found.</exception>
    IKitStore Create();

    /// <summary>
    /// Creates the store of the closest SHADE kit root at or above <paramref name="path" />.
    /// </summary>
    /// <param name="path">The directory to start the search from.</param>
    /// <returns>The store of the closest SHADE kit root.</returns>
    /// <exception cref="KitRootPathNotFoundException">No SHADE kit root directory is found.</exception>
    IKitStore Create(string path);
}
```

### [Nitpick][.NET-Idioms] tests/CreativeCoders.ShadeKit.Cli.Tests/Commands/Features/List/FeaturesListCommandTests.cs:351
`x.Commands.SequenceEqual(new[] { "features" })` uses the verbose array initializer that the net10 checklist flags ("Collection expressions" / "Pattern matching — list patterns").

**FACT:** `CliCommandGroupAttribute.Commands` is a `string[]` in `CreativeCoders.Cli.Core` 6.10.0. Both `x.Commands is ["features"]` and `x.Commands.SequenceEqual(["features"])` compile against it and return `true` for this attribute. This was checked with a throw-away file-based app in the scratchpad, not in the repo. The list pattern is the shortest form and needs no LINQ. No build analyzer flags this today, because the build is reported warning-free.

```csharp
groupAttributes
    .Should().ContainSingle(x => x.Commands is ["features"]);
```

## Tool Output Appendix

### dotnet build
- Skipped (build=no; reported clean with `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, and `TreatWarningsAsErrors` in Phase 4).

### dotnet test
- Skipped (test=no; reported 67/67 passed in Phase 4).

### dotnet format
- Skipped (format=no). Whitespace hygiene was checked separately (see "Other checks").

### Other checks
- `scripts/detect-dotnet-version.sh`: exit 5, "malformed global.json". This is a known artifact: the detector expects an `sdk` section. The target framework was resolved from `Directory.Build.props` (see metadata).
- `scripts/collect-diff.sh --mode uncommitted`: exit 0, 37 files / 1943 LOC raw. 35 files / 1601 LOC remain after excluding the two prior review reports.
- Read-only verifications: byte-level scan of all 35 in-scope files for trailing whitespace, final newline, BOM, and CR; `git diff HEAD` of the Core store files (whitespace-only changes for finding D); search for `NoWarn` / `#pragma` / `SuppressMessage` (none found); search for checklist anti-patterns (`.Result`, `.Wait()`, `async void`, `catch (Exception)`, `DateTime.Now`, `new Random`, object `lock`, verbose array/list initializers; one hit, reported above); type probe of `CliCommandGroupAttribute.Commands` with a scratch file-based app (`dotnet run probe.cs`, outside the repo).
