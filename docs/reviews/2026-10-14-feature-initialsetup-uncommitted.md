# .NET Code Review — feature/initialsetup (uncommitted)

**Date:** 2026-10-14
**Mode:** uncommitted (provided)
**Detected SDK:** not pinned (`global.json` has no `sdk.version`); installed SDK used for ad-hoc verification: 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props — read manually; `detect-dotnet-version.sh` exited 5 ("malformed global.json") because `global.json` contains only `test.runner` and no `sdk.version`. The file is valid JSON; the script's check is a false positive. No user was reachable (sub-agent), so the review continued version-aware with the TFM from `Directory.Build.props`.
**Checklist:** review-checklist-net10.md, plus the general security, performance, architecture, and code-quality checklists
**Tools run:** build=N · format=N · test=N (all three `provided`)
**Report language:** English (provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; diff exceeds 2000 LOC and 50 files)
**Diff size:** 71 files, 6900 changed LOC

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 3 |
| Suggestion | 3 |
| Nitpick | 2 |

**Scope:** Feature package 01 (store foundation and format versioning, `docs/feature-packages/01-store-foundation-and-format-versioning.md`, ADR 0001). The diff covers the `Formats` subsystem in Core (`FormatDefinition`, `FormatRegistry`, `VersionedDocument`, `VersionedJsonSerializer`, and the format exceptions), `KitConfig` and its policy enum, `EngineVersionProvider`, the reworked `KitStore`/`KitStoreFactory` (git-root-anchored kit root, `ReadJsonAsync`/`WriteJsonAsync`), the CLI agent contract (`CliJsonEnvelope`, `CliJsonWriter`, `CliErrorMapper`, `ShadeKitCliErrorCodes`, the new exit codes), the JSON-mode host wrapper in `Program.RunAsync`, `version --json`, `features list --json`, a `CLAUDE.md` update, and 280 test methods across both test projects. Two obsolete files (`Config/ConfigWithVersion.cs`, `Config/StoreConfig.cs`) are deleted.

**Acceptance criteria:** FP01-AC1 through FP01-AC12 are implemented. AC10 is proven at the `IKitStore` level only, because no CLI command reads a versioned file yet. AC8 and AC12 have a gap: when `--json` comes before the command name, the run is not recognised as JSON mode (see the Program.cs findings). The agreed design decisions listed by the calling workflow were treated as intended and are not reported as findings.

**Top risks:**
1. In plain mode, an unexpected exception crashes the process with exit code 134 (SIGABRT) and a .NET crash trace, not with the host-error code 255 (verified with an unreadable `features` directory).
2. JSON mode depends on how the option parser reads `--json`: `shade-kit --json version`, `version --json=true`, and `version --json --json` all print plain host-error text to stdout and exit 255 with no envelope (verified against the built binary).
3. `Program.RunAsync` writes a host-error envelope without checking whether the command already wrote one. A future command that writes an envelope and then fails unexpectedly would put two JSON documents on stdout.

**Overall:** A carefully built, well-documented, and thoroughly tested foundation. Versioning, migration, unknown-field preservation, and the pre-write check behave as the requirement specifies. The remaining issues sit at the edges of the agent contract (how JSON mode is detected and the exactly-one-envelope guarantee) and in plain-mode crash handling. None of them blocks shipping.

## Findings — src/CreativeCoders.ShadeKit.Cli/Program.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Program.cs:43
In plain mode, unexpected exceptions are not caught, so the process crashes with exit code 134 and a .NET "Unhandled exception" trace instead of returning a stable host-error exit code.

FACT: running `shade-kit features list` in a repository whose `.shadekit/features` directory has mode `000` printed `Unhandled exception. System.UnauthorizedAccessException ...` and exited with 134. The same run with `--json` correctly produced a `host-error` envelope and exit 255. Package 01 states that every failure has a stable exit code and that 255 is reserved for host errors. `RunAsync_UnexpectedExceptionPlain_PropagatesExceptionWithoutEnvelope` pins the current behaviour, so it looks deliberate, but it gives users an unstable exit code (134 on Unix, a different value on Windows), possible OS crash reports, and a stack trace in place of a message. Catch unexpected exceptions in plain mode as well, write a short message to stderr, and return `HostError`. Then update the pinning test.

```csharp
if (!IsJsonMode(args))
{
    try
    {
        return ShadeKitCliExitCodes.ToProcessExitCode(
            await CreateHost(new CliJsonWriter(output), output).RunMainAsync(args).ConfigureAwait(false));
    }
    catch (Exception e)
    {
        await error.WriteLineAsync($"shade-kit failed unexpectedly: {e.Message}").ConfigureAwait(false);
        await error.WriteLineAsync(e.ToString()).ConfigureAwait(false);

        return ShadeKitCliExitCodes.HostError;
    }
}
```

### [Minor][Architecture] src/CreativeCoders.ShadeKit.Cli/Program.cs:77
JSON-mode detection uses the option parser's reading of `--json`. Common spellings an agent may use are not recognised, so the agent gets plain text on stdout and no envelope.

FACT (built binary, `bin/Debug/net10.0/shade-kit`): `--json version`, `version --json=true`, `version --json maybe`, and `version --json --json` each print `No command found for given arguments: ...` to stdout and exit 255, with no JSON. `RunAsync_JsonSwitchBeforeCommand_TakesCommandAsSwitchValueAndUsesPlainMode` documents the first case. The requirement says every `--json` output, success or failure, carries the envelope (FP01-AC12) and that every error in JSON mode produces a JSON document (FP01-AC8). An agent that puts global flags first, which is a common CLI habit, breaks this contract. Decide JSON mode from the raw tokens: treat `--json` (and `--json=true`) anywhere in `args` as JSON mode unless it is explicitly followed by `false`. Keep the host's own parsing unchanged.

```csharp
private static bool IsJsonMode(string[] args)
{
    const string Switch = "--" + JsonCommandOptions.JsonOptionName;

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(Switch + "=true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (args[i] == Switch)
        {
            return i + 1 >= args.Length || !args[i + 1].Equals("false", StringComparison.OrdinalIgnoreCase);
        }
    }

    return false;
}
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Program.cs:58
The `catch` branch (line 58) and the negative-exit-code branch (line 64) always write a `host-error` envelope, even when the command already wrote one. In that case stdout holds two JSON documents, which breaks the agreed rule of exactly one envelope on stdout.

No current command triggers this. `FeaturesListCommand` writes its envelope as its last action, and failure envelopes are followed by a `CliCommandAbortException` that the host turns into a positive exit code. But the guarantee depends on every future command never failing after `Write`. `TrackingCliJsonWriter.HasWritten` is already there; use it so that no second document is written, and only adjust the exit code.

```csharp
catch (Exception e)
{
    await error.WriteLineAsync(e.ToString()).ConfigureAwait(false);

    return jsonWriter.HasWritten
        ? ShadeKitCliExitCodes.HostError
        : WriteHostError(output,
            $"The command failed unexpectedly: {e.Message} Details were written to standard error.");
}

if (exitCode < 0)
{
    return jsonWriter.HasWritten
        ? ShadeKitCliExitCodes.HostError
        : WriteHostError(output,
            $"The command could not be run (host exit code {exitCode}). Details were written to standard error.");
}
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs

### [Suggestion][Tests] src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs:65
`MapWarning` has no production caller. No code path turns `VersionedReadResult.Warnings` into envelope `warnings`, so FP01-AC10 ("a read command ... returns its result with an `upgrade-needed` warning") is verified only at the store level.

That is reasonable while no command reads a versioned file. But the CLI side of AC10 (warning code, message, exit code 0, `ok: true`) has no end-to-end test, and every later read command will need the same plumbing. Consider adding a small shared helper that builds a success envelope from a `VersionedReadResult`, and covering it once with a test, so package 02+ commands cannot forget the warnings.

```csharp
internal static class CliJsonEnvelopeFactory
{
    public static CliJsonEnvelope Success<TData>(TData data, IEnumerable<FormatWarning> formatWarnings)
        where TData : notnull
    {
        return CliJsonEnvelope.Success(data, formatWarnings.Select(CliErrorMapper.MapWarning));
    }
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs

### [Suggestion][Security] src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs:167
The containment check in `GetFullPath` compares path strings only. Symbolic links inside `.shadekit/` can still send reads and writes outside the kit root (OWASP A01/A04).

`Path.GetFullPath` does not resolve links. If `.shadekit/features/x` is a directory symlink to some other location, `WriteJsonAsync("features/x/feature.json", ...)` passes the check and creates the file at the link target. FACT (verified on .NET 10.0.402, macOS): `File.Exists` returns `true` for a dangling file symlink, and `File.WriteAllTextAsync` follows the link and creates the target outside the root. In `KitStore`, the pre-write check reads first, so a dangling file symlink currently ends in an unmapped `FileNotFoundException` (host error 255) rather than a write. Directory symlinks are not caught. The impact is low: file names are fixed by the engine and content is engine-generated. The XML docs promise "points outside RootPath", though, and `.shadekit/` comes from cloned repositories. Either reject link segments below `RootPath`, or document that symlinks are followed.

```csharp
private void EnsureNoLinkBelowRoot(string fullPath)
{
    for (var current = new FileInfo(fullPath) as FileSystemInfo;
         current is not null && current.FullName.Length > RootPath.Length;
         current = Directory.GetParent(current.FullName))
    {
        if (current.Exists && current.LinkTarget is not null)
        {
            throw new ArgumentException($"The path '{fullPath}' contains a symbolic link.", nameof(fullPath));
        }
    }
}
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs:103
The null-forgiving operator is used without a comment, here and in `src/CreativeCoders.ShadeKit.Core/Formats/VersionedJsonSerializer.cs:82`.

Both are safe. `fullPath` always lies below `RootPath`, so it has a parent, and serializing a non-null `VersionedDocument` never yields a null node. The code-quality checklist asks for a short reason with each `!`.

```csharp
// fullPath is always below RootPath (checked in GetFullPath), so it has a parent directory.
Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Store/KitStore.cs:152
`IsConfigFile` compares full paths with ordinal `==`. On case-insensitive file systems (default macOS, Windows), `ReadJsonAsync("Config.json", FormatIds.Config, FormatAccess.Read)` on an outdated config reads the same physical file but is not recognised as the config itself, so the `upgrade-needed` warning is reported twice.

All callers use `KitStorePaths.ConfigFileName`, so this is unlikely in practice. A comparison that matches the platform avoids the duplicate.

```csharp
private bool IsConfigFile(string relativePath)
{
    var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    return string.Equals(GetFullPath(relativePath), GetFullPath(KitStorePaths.ConfigFileName), comparison);
}
```

## Findings — tests/CreativeCoders.ShadeKit.Core.Tests/Store/KitStoreJsonTests.cs

### [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Store/KitStoreJsonTests.cs:105
`ReadJsonAsync_OlderVersionWithMigrateAndWarnOrNoSetting_ReturnsMigratedDocumentWithWarning` contains an `if (writeConfig)` branch in its Arrange block. The reviewer checklist flags conditional logic in test bodies.

The theory mixes two scenarios: no `config.json`, and a `config.json` with or without the setting. Split them into two tests, or pass the config content (or `null`) as theory data and write it through a helper that accepts `null`.

```csharp
[Fact]
[Trait("Requirement", "FP01-AC10")]
public async Task ReadJsonAsync_OlderVersionWithoutConfigFile_ReturnsMigratedDocumentWithWarning() { /* no config.json */ }

[Theory]
[Trait("Requirement", "FP01-AC10")]
[InlineData("migrate-and-warn")]
[InlineData(null)]
public async Task ReadJsonAsync_OlderVersionWithMigrateAndWarnOrNoSetting_ReturnsMigratedDocumentWithWarning(
    string? outdatedFormatOnRead) { /* always writes config.json */ }
```

## Tool Output Appendix

### dotnet build
- Skipped (build=no, provided). The calling workflow reported a green build in Phase 4. For ad-hoc verification of the Program.cs findings only, `dotnet build src/CreativeCoders.ShadeKit.Cli` was run once: 0 errors.

### dotnet test
- Skipped (test=no, provided). The calling workflow reported 390/390 tests passing in Phase 4; not re-run here.

### dotnet format
- Skipped (format=no, provided).
