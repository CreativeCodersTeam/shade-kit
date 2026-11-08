# 06-1 · Engine version check (`version --expect`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06), updated 2026-11-05 and 2026-11-06 (implementation clarification and implementation: strict SemVer 2.0.0 for `--expect`, the match on major and minor, `expectedVersion` in the envelope, the messages, `--expect` without a value and the `=` form, exit code 50, warning code `engine-prerelease-version-mismatch`). Implemented. |
| Source | [06 Skill foundation](06-skill-foundation.md) (version handshake), [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Kind | .NET (engine and CLI) |
| Depends on | 01 |
| Enables | 06-2, 06-5, 06-6, 06-9 |

## Benefit

A skill learns with one CLI call whether it may run against the installed engine. The engine applies the match rule, so every agent gets the same answer, and the skill text does not have to compare version numbers itself.

## Scope

### In scope

- The option `--expect <version>` of `shade-kit version`.
- The match rule, the result in JSON and plain output, and the new exit, error, and warning codes.
- The `CLAUDE.md` changes that follow from it.

### Out of scope

- Where a skill takes its version from and how it calls the check (06-2, 06-5, 06-6, 06-9).
- Keeping the skill version in line with GitVersion (open point in 06-2).
- Installed integrations in `version --json` (package 07).

## Functional description

**Implemented** in the Core namespace `CreativeCoders.ShadeKit.Core.Versions` (`Core/Versions/`): `SemanticVersion`, `IEngineVersionChecker` / `EngineVersionChecker` (a singleton registered by `AddShadeKitCore()`), `EngineVersionCheckResult`, `EngineVersionMismatchDirection`, the exceptions in `Versions/Exceptions/`, and the warning in `Versions/Warnings/`. The CLI part is `VersionCommand`, `VersionCommandOptions.Expect`, `VersionCommandData.ExpectedVersion`, `ExpectedVersionOptionConverter`, and `CliErrorMapper`.

### Option `--expect`

- `shade-kit version [--json] --expect <version>` compares the engine version with the passed kit version of the calling skill. **Decided** on 2026-11-04. **Implemented** (`VersionCommandOptions.Expect`, `VersionCommand`).
- Without `--expect`, `version` behaves as before (package 01): it cannot fail, and its output is unchanged. **Implemented** (`VersionCommandTests`, `ProgramTests`).
- With `--expect`, `version` runs the check through `ShadeKitCommandContext` like every other command. **Decided** on 2026-11-05. **Implemented** (`VersionCommand`).
- `<version>` is a strict SemVer 2.0.0 version (grammar of semver.org): `major.minor.patch` with numbers without leading zeros (`0` is allowed), an optional prerelease label, and optional build metadata. Build metadata (`+…`) is accepted and ignored. Rejected are, for example, a `v` prefix (`v1.2.3`), whitespace around or inside the value (it is not trimmed), the empty string, two or four parts (`1.2`, `1.2.3.4`), leading zeros (`01.2.3`, `1.2.3-01`), and a number above `int.MaxValue`. Any value that is not a SemVer version ends with exit code 2 `invalid-arguments`. **Decided** on 2026-11-05. **Implemented** (`SemanticVersion`, `InvalidSemanticVersionException`, `CliErrorMapper`; `SemanticVersionTests`).
- `--expect` without a value (the last argument, or followed by an argument that starts with `-`) also ends with exit code 2 `invalid-arguments`. The option parser alone would leave the option unset, so `version` would succeed without a check. **Decided** on 2026-11-06 (implementation). **Implemented**: `ExpectedVersionOptionConverter` throws `ExpectedVersionMissingException` while the arguments are read, and `CliErrorMapper.MapArgumentError` maps it (`ExpectedVersionOptionConverterTests`, `VersionCommandOptionsTests`, `CliErrorMapperTests`, `ProgramTests`).
- The value follows the option after a space. The option parser has no `--expect=<version>` form: `--expect=1.2.3` is an unknown option and is ignored like every unknown option of every command, so `version` succeeds without a check (exit code 0). Skills call exactly `--expect <version>`. **Decided** on 2026-11-06 (implementation: accepted and documented).

### Match rule

**Decided** on 2026-11-04. **Implemented** (`EngineVersionChecker`; `EngineVersionCheckerTests`).

| Engine version | Major and minor equal to `--expect` | Result |
|---|---|---|
| Release (no prerelease label) | yes (patch may differ) | Success |
| Release | no | Failure: exit code 50 `version-mismatch` |
| Prerelease (for example `0.8.0-feature-06.3`) | yes | Success |
| Prerelease | no | Success with the warning `engine-prerelease-version-mismatch` |

- The prerelease label of `--expect` is not compared, and neither is the patch. **Decided** on 2026-11-05.
- Major and minor are compared as a pair: the engine is lower than the skill if its (major, minor) is lower, for example `1.9` against `1.10`, and higher otherwise. **Decided** on 2026-11-05.
- An engine version that is not a SemVer version is a defect of the engine build, not of the arguments: the check fails with exit code 255 `host-error`. **Decided** on 2026-11-05. **Implemented** (`EngineVersionChecker` throws an `InvalidOperationException`, which `CliErrorMapper` does not map).

### Result

- Success: the envelope of `version` (package 01) with `expectedVersion`, the value of `--expect` as passed, in addition. Without `--expect`, the field is left out, so the envelope is unchanged. **Decided** on 2026-11-05. **Implemented** (`VersionCommandData.ExpectedVersion`).
- Failure: a failure envelope with `version-mismatch`. The message names both versions and what to update. **Decided** on 2026-11-05. **Implemented** (`EngineVersionMismatchException`, `CliErrorMapper`). `{engine}` is the engine version, `{expected}` the value of `--expect` as passed, and `x.y` the major and minor version:
  - Engine lower than the skill: `The SHADE kit engine {engine} does not fit the kit skills {expected}: the major and minor versions must be equal. Update the engine to x.y, then run the skill again.` (`x.y` of `{expected}`)
  - Engine higher than the skill: `The SHADE kit engine {engine} does not fit the kit skills {expected}: the major and minor versions must be equal. Update the SHADE kit skills to x.y by hand, then run the skill again.` (`x.y` of `{engine}`). Until package 07 builds `integrations update`, the message names updating the skills by hand.
- Warning of a prerelease engine: `The prerelease engine {engine} does not fit the kit skills {expected} (the major and minor versions differ); it is used anyway because it is a prerelease.` **Decided** on 2026-11-05. **Implemented** (`EnginePrereleaseVersionMismatchWarning`, `CliErrorMapper.MapWarning`).
- Invalid `--expect`: `Invalid arguments: '{value}' is not a SemVer version such as 1.2.3 or 1.2.3-beta.1. Run 'shade-kit --help' for usage.` The text names neither `--expect` nor `version`, because the Core `InvalidSemanticVersionException` it maps is generic and a later caller of the SemVer check must not get an `--expect` message (**Decided** on 2026-11-06, code review). For `--expect` without a value: `Invalid arguments: --expect needs a SemVer version such as 1.2.3 or 1.2.3-beta.1, but got none. Run 'shade-kit version --help' for usage.` **Decided** on 2026-11-05 and 2026-11-06. **Implemented** (`CliErrorMapper.Map`, `CliErrorMapper.MapArgumentError`).
- Plain output: on success, the line `shade-kit <engine version>` as before; a warning comes first as a line `Warning: <message>`. On a mismatch, the same message as in JSON mode, exit code 50. **Implemented** (`ShadeKitCommandContext.WriteSuccess`, `VersionCommand`).
- Where a plain failure is written follows the CLI host, like for every other command. **Decided** on 2026-11-06 (implementation: both paths kept and documented):
  - A mismatch and an invalid `--expect` value fail inside the command: the message goes to stdout.
  - `--expect` without a value fails while the arguments are read, like every argument error: the message goes to stderr.
  - In JSON mode, every case writes one envelope to stdout and nothing else.

### Exit and warning codes

- New range **50–59, versions and integrations**, for this package and package 07. **Decided** on 2026-11-04. **Implemented** (`ShadeKitCliExitCodes`).
- `50` `version-mismatch` (`ShadeKitCliExitCodes.VersionMismatch`, `ShadeKitCliErrorCodes.VersionMismatch`, `CliErrorMapper`). **Implemented**.
- Warning `engine-prerelease-version-mismatch` (`ShadeKitCliWarningCodes.EnginePrereleaseVersionMismatch`). **Decided** on 2026-11-05 (name). **Implemented**.

### `CLAUDE.md`

- The sentence that `version` cannot fail and therefore writes its envelope through `ICliJsonWriter` is replaced: no command uses `ICliJsonWriter` directly, `version` included. **Decided** on 2026-11-05. **Implemented**.
- The range 50–59 is added to the list of exit code ranges, `Core/Versions/` to the repository layout, and `version --json --expect <version>` to the command examples. **Implemented**.

## Acceptance criteria

Every criterion is **Implemented**. The tests that prove it carry `[Trait("Requirement", "FP06-1-ACn")]`.

- **FP06-1-AC1** `shade-kit version --json --expect <v>` against a release engine whose major or minor differs from `<v>` ends with exit code 50 and a failure envelope with the error code `version-mismatch`; the message names both versions. The plain output gives the same message and exit code. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC2** The mismatch message names updating the engine when the engine version is lower than `<v>`, and updating the skills when it is higher. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC3** With equal major and minor and a different patch, the command succeeds without a warning. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `ProgramTests`.
- **FP06-1-AC4** A prerelease engine whose major or minor differs from `<v>` succeeds with the warning `engine-prerelease-version-mismatch`. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC5** An `--expect` value that is not a SemVer version ends with exit code 2 `invalid-arguments`. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `VersionCommandOptionsTests`, `ExpectedVersionOptionConverterTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC6** Without `--expect`, the output and exit code of `version` are unchanged. Proven by `VersionCommandTests`.

## Open points

None. Both earlier points are answered: the success envelope carries `expectedVersion` when `--expect` is passed, and the warning code is `engine-prerelease-version-mismatch` (both **Decided** on 2026-11-05).
