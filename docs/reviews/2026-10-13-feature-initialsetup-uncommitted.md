# .NET Code Review — feature/initialsetup (uncommitted)

**Date:** 2026-10-13
**Mode:** uncommitted (working tree vs. HEAD, incl. staged/unstaged/untracked) — origin: provided
**Detected SDK:** 10.0.401 (installed `dotnet --version`; `global.json` pins no SDK)
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props — `detect-dotnet-version.sh` exited 5 ("malformed global.json") because `global.json` contains only `test.runner` and no `sdk` section. The file is valid JSON and the missing SDK pin is a confirmed design decision, so the target framework was resolved manually from `<TargetFramework>net10.0</TargetFramework>` in `Directory.Build.props` (non-interactive run, no user to ask).
**Checklist:** review-checklist-net10.md (+ security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (all provided; build and 56/56 tests reported green in Phase 4)
**Report language:** English — origin: provided
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** full (diff below the large-diff thresholds; gate not triggered)
**Diff size:** 27 files, 1362 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 1 |
| Suggestion | 4 |
| Nitpick | 1 |

**Top risks:**
1. Four "kit root not found" tests only pass if no `.shadekit` directory exists anywhere above the system temp directory, so they depend on the machine they run on.
2. `FeaturesListCommand` treats any `DirectoryNotFoundException` from `IKitStore` as "features directory missing". That ties the CLI to an undocumented detail of `KitStore` instead of a domain exception, which is how the root-not-found case already works.
3. `shade-kit version` prints the full 40-character commit SHA (**FACT**: observed `shade-kit 1.0.0+4355d63c184d1586407d6b3c714ff47e07b3e1f1`). For builds from a dirty working tree, that SHA is HEAD's, not the code that was actually built.

**Overall:** A clean, well-tested CLI skeleton. The confirmed design decisions are implemented as described: `CliCommandAbortException` with `Markup.Escape`d messages, `ToProcessExitCode` mapping negative codes to 255 (**FACT**: an unknown command exits 255, a missing kit root exits 1), the `KitStoreFactory.Create(string)` fix, CPM, and MTP/coverlet.MTP. There are no Critical or Major findings. The remaining items are test robustness and small design refinements.

Verified facts used in this review:
- **FACT:** `ICliCommand`, `ICliCommand`1`, `CliCommandGroupAttribute`, `CommandResult` exist in `CreativeCoders.Cli.Core.dll`; `CliCommandAbortException`, `CliExitCodes`, `PrintFooterText` exist in `CreativeCoders.Cli.Hosting.dll` 6.10.0, so the conventions documented in `CLAUDE.md` match the package.
- **FACT:** `dotnet test --coverlet` is the documented coverlet.MTP 10.1.0 switch (`Coverlet.MTP.Integration.md`).
- **FACT:** `Spectre.Console.Testing` 0.57.2 matches the transitive `Spectre.Console` 0.57.2 that `CreativeCoders.Cli.Hosting` 6.10.0 pulls in, so there is no version skew.
- **FACT:** `TestResults/`, `bin/` and `obj/` are git-ignored (`.gitignore` lines 23, 36).

## Findings

### [Minor][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Store/KitStoreFactoryTests.cs:63
The "not found" tests depend on the environment: they pass only if no `.shadekit` directory exists above the system temp directory.

`KitStoreFactory` walks up to 20 parent directories. These tests create their tree under `Directory.CreateTempSubdirectory(...)` and expect `KitRootPathNotFoundException`:
- `Create_NoKitRootInPathOrParents_ThrowsKitRootPathNotFoundException` (line 63)
- `Create_KitRootTwentyLevelsAbovePath_ThrowsKitRootPathNotFoundException` (line 98)
- `Create_ShadeKitIsAFile_ThrowsKitRootPathNotFoundException` (line 153)
- `KitStoreFactoryCurrentDirectoryTests.Create_NoKitRootAboveCurrentDirectory_ThrowsKitRootPathNotFoundException` (`KitStoreFactoryCurrentDirectoryTests.cs:38`)

If any ancestor of the temp directory contains `.shadekit`, these tests fail. For example, `TMPDIR` might point into a CI workspace or into a repository that dogfoods SHADE kit, or a user might have `/tmp/.shadekit` or `/.shadekit`. Once the kit installs `.shadekit` into repositories, possibly this one, the risk becomes real. Guard the precondition explicitly so that a polluted environment gives a clear failure, or skips the test, instead of an obscure assertion error.

```csharp
// TempKitDirectory.cs: expose a precondition check
public bool HasKitRootAbove()
{
    for (var dir = _directory.Parent; dir is not null; dir = dir.Parent)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, KitStorePaths.RootPath)))
        {
            return true;
        }
    }

    return false;
}

// In each "not found" test (xUnit v3):
Assert.SkipWhen(tempDirectory.HasKitRootAbove(),
    "A '.shadekit' directory exists above the temp directory; the not-found scenario cannot be tested here.");
```

### [Suggestion][Architecture] src/CreativeCoders.ShadeKit.Cli/Commands/Features/List/FeaturesListCommand.cs:68
The command reads a BCL `DirectoryNotFoundException` from `IKitStore` as "the kit root has no `features` directory".

`IKitStore.GetFeatureNames()` declares no such contract. The mapping relies on how `KitStore` happens to be implemented today (`Directory.EnumerateDirectories`). The same exception can also mean that the `.shadekit` directory itself disappeared after the lookup, and the message would then be wrong. Core already uses a domain exception for the parallel case (`KitRootPathNotFoundException`). Changing `KitStore`'s behavior is outside this change set, so consider this a follow-up: give Core a `FeaturesDirectoryNotFoundException` and let the CLI catch that instead.

```csharp
// Core (follow-up)
public class FeaturesDirectoryNotFoundException(string featuresPath, Exception? innerException = null)
    : Exception($"Features directory '{featuresPath}' not found.", innerException)
{
    public string FeaturesPath { get; } = featuresPath;
}

// KitStore.GetFeatureNames
var featuresPath = GetFeaturesPath();
if (!Directory.Exists(featuresPath))
{
    throw new FeaturesDirectoryNotFoundException(featuresPath);
}

// FeaturesListCommand.GetFeatureNames
catch (FeaturesDirectoryNotFoundException e)
{
    throw new CliCommandAbortException(
        Markup.Escape($"The SHADE kit root directory contains no '{KitStorePaths.FeaturesDirName}' directory."),
        ShadeKitCliExitCodes.FeaturesDirectoryNotFound, e);
}
```

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Commands/Features/List/FeaturesListCommand.cs:41
`ExecuteAsync` returns a `Task` but throws `CliCommandAbortException` and propagates other exceptions synchronously, rather than returning a faulted task.

This works today: the host awaits the call inside its try/catch, and AwesomeAssertions' `ThrowAsync` handles synchronous throws. But Task-returning methods are expected to report non-usage exceptions through the returned task. A caller that stores the task first, or that composes it (for example `Task.WhenAll` or a pre/post-processor), would see the exception at a different point. Separating the synchronous body from the Task wrapper costs almost nothing and makes the behavior uniform. A bare `async` without any `await` would trigger CS1998.

```csharp
public Task<CommandResult> ExecuteAsync()
{
    try
    {
        return Task.FromResult(Execute());
    }
    catch (Exception e)
    {
        return Task.FromException<CommandResult>(e);
    }
}

private CommandResult Execute()
{
    var featureNames = GetFeatureNames();

    if (featureNames.Length == 0)
    {
        _ansiConsole.WriteLine("No features found.");

        return CommandResult.Success;
    }

    foreach (var featureName in featureNames)
    {
        _ansiConsole.WriteLine(featureName);
    }

    return CommandResult.Success;
}
```

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Commands/Version/VersionCommand.cs:43
`shade-kit version` prints the full SourceLink revision, for example `shade-kit 1.0.0+4355d63c184d1586407d6b3c714ff47e07b3e1f1` (**FACT**: observed with the current Debug build).

The .NET SDK adds `+<commit SHA>` to `AssemblyInformationalVersion` by default. For a user-facing version string, this is long. For local builds of uncommitted changes, it is also misleading, because it names HEAD rather than the code that was built. Decide this on purpose: either keep it (useful for bug reports), shorten the SHA, or turn it off in the CLI project with `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>`. The existing test (`VersionCommandTests.cs:13`) reads the attribute at runtime, so it keeps passing with any of these choices.

```csharp
internal static string GetVersion(Assembly assembly)
{
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    if (informationalVersion is not null)
    {
        // Shorten "1.0.0+<40-char sha>" to "1.0.0+<7-char sha>"
        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 && informationalVersion.Length > plusIndex + 8
            ? informationalVersion[..(plusIndex + 8)]
            : informationalVersion;
    }

    return assembly.GetName().Version?.ToString() ?? string.Empty;
}
```

### [Suggestion][Code-Quality] Directory.Build.props:3
The shared build settings enable `Nullable` and `ImplicitUsings`, but nothing for analyzers or warnings. `CLAUDE.md` names `Directory.Build.props` as the home for "nullable, implicit usings, analyzers", and the net10 checklist recommends `TreatWarningsAsErrors`.

Turning this on while the codebase is still tiny is cheap, and it stops warnings from piling up before the quality gates (build/lint) of v1 exist.

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

### [Nitpick][Code-Quality] tests/CreativeCoders.ShadeKit.Cli.Tests/CreativeCoders.ShadeKit.Cli.Tests.csproj:1
File encodings are inconsistent. **FACT:** both new test `.csproj` files (`tests/CreativeCoders.ShadeKit.Cli.Tests/...csproj`, `tests/CreativeCoders.ShadeKit.Core.Tests/...csproj`) start with a UTF-8 BOM, while this change removes the BOM from both `src/**/*.csproj` files and `Program.cs`. The repository has no `.editorconfig` to settle the question, so IDEs will keep flipping it.

Pick one convention and record it in an `.editorconfig` at the repository root.

```ini
# .editorconfig
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
```

## Tool Output Appendix

### dotnet build
- Skipped (build=no; reported green in Phase 4).

### dotnet test
- Skipped (test=no; reported 56/56 passed in Phase 4).

### dotnet format
- Skipped (format=no).

### Other checks
- `scripts/detect-dotnet-version.sh`: exit 5, "malformed global.json". This is an artefact of the detector expecting an `sdk` section; the target framework was resolved from `Directory.Build.props` (see metadata).
- The existing Debug build was run manually to observe runtime behavior: `version` → exit 0; `features list` outside a kit → red abort message, exit 1; unknown command → exit 255.
