# .NET Code Review — feature/initialsetup (uncommitted)

**Date:** 2026-10-14
**Mode:** uncommitted (provided)
**Detected SDK:** not pinned (`global.json` has no `sdk.version`); installed SDK: 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:Directory.Build.props. `detect-dotnet-version.sh` exited 5 ("malformed global.json") because `global.json` contains only `test.runner` and no `sdk` section. The file is valid JSON, so this is a false positive of the script. No user was reachable (sub-agent), so the review continued version-aware with the TFM from `Directory.Build.props`.
**Checklist:** review-checklist-net10.md, plus the general security, performance, architecture, and code-quality checklists
**Tools run:** build=N · format=N · test=N (all three `provided`)
**Report language:** English (provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; diff exceeds 2000 LOC and 50 files)
**Diff size:** 74 files, 7327 changed LOC
**Review type:** re-review after a rework loop; previous report `docs/reviews/2026-10-14-feature-initialsetup-uncommitted.md`

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 1 |
| Suggestion | 3 |
| Nitpick | 2 |

**Scope:** The same working tree as the previous review (feature package 01, `docs/feature-packages/01-store-foundation-and-format-versioning.md`, ADR 0001), plus the rework. Since the previous report, these files changed: `Program.cs`, `Output/TrackingCliJsonWriter.cs`, `Store/KitStore.cs`, `Formats/VersionedJsonSerializer.cs`, `ProgramTests.cs`, `TrackingCliJsonWriterTests.cs`, and `KitStoreJsonTests.cs`. The previous report itself is now also part of the uncommitted diff. The changed files were reviewed in detail. The other files were checked again against the checklists.

**Verification of the fixed findings:**

| ID | Previous finding | Result |
|---|---|---|
| M1 | Plain-mode unexpected exception crashed with exit 134 | **Fixed.** FACT: `Program.cs:54-61` catches every exception, writes `e.ToString()` to stderr, and returns 255 in plain mode. Built binary, unreadable `.shadekit/features`: `features list` exits 255 with the `UnauthorizedAccessException` trace on stderr and nothing on stdout. Covered by `RunAsync_UnexpectedExceptionPlain_ReturnsHostErrorWithDetailsOnStandardErrorAndNoEnvelope`. |
| M2 | JSON mode depended on the option parser | **Fixed, one edge case left** (see the Minor finding). FACT (built binary): `--json version`, `version --json=true`, `version --json 0`, and `version --help --json` each write exactly one `host-error` envelope to stdout and exit 255. `version --json --json` and `version --json True` succeed. `version --json=false` and `version --json False` stay in plain mode. Covered by four new or changed `ProgramTests`. |
| M3 | Host-error envelope written even after the command's own envelope | **Fixed.** FACT: every host-error path goes through `WriteHostError` → `TrackingCliJsonWriter.WriteIfNothingWritten` (`Program.cs:124-130`), so a second document is never written. Covered at unit level by `TrackingCliJsonWriterTests` (including the case where the inner writer throws). The `Program`-level branches have no test (see Suggestion). |
| S3 | Conditional logic in a test theory | **Fixed.** FACT: `KitStoreJsonTests.cs:93` (`ReadJsonAsync_OlderVersionWithoutConfigFile_...`, `[Fact]`) and `:114` (theory over `"migrate-and-warn"` / `null`, always writes `config.json`). No `if` is left in either body. |
| N1 | `!` without a comment | **Fixed.** FACT: comments added at `KitStore.cs:103` and `VersionedJsonSerializer.cs:82`. |
| N2 | `IsConfigFile` used ordinal comparison | **Fixed.** FACT: `KitStore.cs:151-159` uses `OrdinalIgnoreCase` on Windows and macOS, `Ordinal` elsewhere. Covered by `ReadJsonAsync_OutdatedConfigItselfByOtherCaseOnCaseInsensitivePlatform_ReturnsOneWarning`, which skips itself on case-sensitive file systems. |

**Accepted, unchanged (not re-reported):** S1, which is that `CliErrorMapper.MapWarning` has no production caller yet (the first read command comes with package 03), and S2, which is that a symlink inside `.shadekit` can get past the string-based outside-root check.

**Acceptance criteria:** FP01-AC1 through FP01-AC12 remain implemented. The AC8/AC12 gap from the previous review is closed for every realistic spelling of `--json`. The agreed design decisions named by the calling workflow were treated as intended.

**Top risks:**
1. If `--json=false` comes before a bare `--json`, the host runs in plain mode while the command runs in JSON mode. Unexpected failures then return 255 with an empty stdout, and successful runs put an extra blank line after the envelope (verified with the built binary).
2. The two tests that make `.shadekit/features` unreadable fail when the suite runs as root, which is common in CI containers.
3. No `<Version>` is set anywhere, so development builds report engine version `1.0.0`, while the format table says the first engine is `0.1.0`.

**Overall:** The rework fixes all six findings correctly and adds tests for each one. Only one small edge case is left in JSON-mode detection, and it needs an unusual combination of arguments. Nothing blocks shipping.

## Findings — src/CreativeCoders.ShadeKit.Cli/Program.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Program.cs:82
`IsJsonMode` and the option parser can still disagree. If `--json=false` comes before a bare `--json`, `Program` picks plain mode, but the parser sets `Json = true`, so the command writes an envelope.

FACT (built binary): the option parser ignores `--json=<value>` tokens, and `IsJsonMode` returns at the first `--json` token of either form. `features list --json=false --json` in a repository with an unreadable `features` directory exits 255 with **0 bytes on stdout**, so there is no envelope. On success, the same arguments write the envelope followed by the host's empty footer line (`}\n\n`), because console output goes to stdout in plain mode. This is the one input left where the rule "every `--json` run writes exactly one envelope" (FP01-AC8/AC12) does not hold. The input is unlikely, but the fix is local. Let a bare `--json` decide first, as the parser does, and look at the `--json=<value>` form only when no bare switch exists.

```csharp
private static bool IsJsonMode(string[] args)
{
    var jsonSwitch = "--" + JsonCommandOptions.JsonOptionName;

    // The option parser reads only the bare switch (first occurrence wins), so it decides first.
    var bareIndex = Array.IndexOf(args, jsonSwitch);

    if (bareIndex >= 0)
    {
        return bareIndex + 1 >= args.Length || !IsFalse(args[bareIndex + 1]);
    }

    // "--json=<value>" is not parsed by the host; treat it as JSON mode so the run gets a host-error envelope.
    var assignment = args.FirstOrDefault(arg => arg.StartsWith(jsonSwitch + "=", StringComparison.Ordinal));

    return assignment is not null && !IsFalse(assignment[(jsonSwitch.Length + 1)..]);
}
```

Add `["version", "--json=false", "--json"]` to a JSON-mode test, and assert that stdout parses as one JSON document.

### [Suggestion][Tests] src/CreativeCoders.ShadeKit.Cli/Program.cs:69
The M3 fix is tested only on `TrackingCliJsonWriter`. Two `Program` branches have no test: the command already wrote an envelope and then an exception escapes (line 58), and the host returns a negative exit code after an envelope (line 69).

`RunAsync` builds the host itself and scans only the CLI assembly, so a test cannot add a command that writes and then fails. If someone later replaces `WriteHostError` with a direct `jsonWriter.Write`, no test fails. Move the decision after the run into a small internal method and test it directly.

```csharp
internal static int CompleteJsonRun(int hostExitCode, TrackingCliJsonWriter jsonWriter)
{
    if (hostExitCode < 0)
    {
        return WriteHostError(jsonWriter,
            $"The command could not be run (host exit code {hostExitCode}). Details were written to standard error.");
    }

    return jsonWriter.HasWritten
        ? hostExitCode
        : WriteHostError(jsonWriter, "The command wrote no JSON result. ...");
}

// Test: envelope already written + negative exit code -> 255 and still exactly one document.
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Program.cs:56
In plain mode, an unexpected exception produces only the raw `e.ToString()` trace. JSON mode, by contrast, starts with "The command failed unexpectedly: …".

The first trace line includes the exception type and message, so it is readable. A short leading line would still make the two modes match and tell users that this is an internal failure, not a usage error.

```csharp
await error.WriteLineAsync($"shade-kit failed unexpectedly: {e.Message}").ConfigureAwait(false);
await error.WriteLineAsync(e.ToString()).ConfigureAwait(false);
```

## Findings — Directory.Build.props

### [Suggestion][Architecture] Directory.Build.props:3
No `<Version>` / `<VersionPrefix>` is set in the repository. As a result, `EngineVersionProvider` reports the SDK default `1.0.0`, while `FormatRegistry` stamps `minEngineVersion` `"0.1.0"` as the first engine version.

FACT: `shade-kit version --json` (Debug build) returns `"engineVersion": "1.0.0"`, and `grep -i version src/*/*.csproj Directory.Build.props` finds no version property. Today nothing compares the two values. Once skills run the version handshake (package 06), a development build that claims `1.0.0` passes every `>= 0.x` check, including checks for features it does not have. Setting the version prefix in one place makes the engine version and the format table agree. The release workflow can still override it.

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <!-- Engine version; must stay >= every minEngineVersion in FormatRegistry. -->
  <VersionPrefix>0.1.0</VersionPrefix>
  ...
</PropertyGroup>
```

## Findings — tests/CreativeCoders.ShadeKit.Cli.Tests/ProgramTests.cs

### [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Cli.Tests/ProgramTests.cs:181
`RunAsync_UnexpectedExceptionJson_...` (line 178) and `RunAsync_UnexpectedExceptionPlain_...` (line 454) cause the failure with `File.SetUnixFileMode(featuresPath, UnixFileMode.None)`. They are skipped only on Windows.

When the tests run as root, which is common in Docker-based CI images, file modes are ignored. The directory stays readable, `features list` succeeds with exit 0, and both tests fail. Check after the `chmod` that the directory really is unreadable, and skip the test otherwise.

```csharp
File.SetUnixFileMode(featuresPath, UnixFileMode.None);

Assert.SkipWhen(CanEnumerate(featuresPath),
    "File modes are not enforced for this user (for example root), so the directory stays readable.");

static bool CanEnumerate(string path)
{
    try
    {
        _ = Directory.EnumerateDirectories(path).Any();
        return true;
    }
    catch (UnauthorizedAccessException)
    {
        return false;
    }
}
```

### [Nitpick][Tests] tests/CreativeCoders.ShadeKit.Cli.Tests/ProgramTests.cs:159
`RunAsync_JsonSwitchWithTrueValue_UsesJsonMode` does not check the exit code. It and `RunAsync_JsonSwitchTwice_WritesSuccessEnvelope` (line 139) also lack the `[Trait("Requirement", "FP01-AC12")]` that the other JSON-mode tests carry.

Without the exit-code check, a run that writes `ok: true` but ends with 255 would still pass. Without the trait, the requirement-to-test mapping misses the M2 cases.

```csharp
[Fact]
[Trait("Requirement", "FP01-AC12")]
public async Task RunAsync_JsonSwitchWithTrueValue_UsesJsonMode()
{
    // ...
    var exitCode = await Program.RunAsync(["version", "--json", "true"], output, error);

    // Assert
    exitCode
        .Should().Be(0);
    // ...
}
```

## Tool Output Appendix

### dotnet build
- Skipped (build=no, provided). The calling workflow reported a green build. For ad-hoc verification of M1, M2, and the Minor finding, the existing Debug binary `src/CreativeCoders.ShadeKit.Cli/bin/Debug/net10.0/shade-kit` was used. Its `shade-kit.dll` is newer than `Program.cs` and `TrackingCliJsonWriter.cs`. The only later source changes (`KitStore.cs`, `VersionedJsonSerializer.cs`) are N1 comments and the N2 comparison, which do not affect these checks.

### dotnet test
- Skipped (test=no, provided). The calling workflow reported 400/400 tests passing; not re-run here.

### dotnet format
- Skipped (format=no, provided).
