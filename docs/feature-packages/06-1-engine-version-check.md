# 06-1 · Engine version check (`version --expect`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06) |
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

### Option `--expect`

- `shade-kit version [--json] --expect <version>` compares the engine version with the passed kit version of the calling skill. **Decided** on 2026-11-04.
- Without `--expect`, `version` behaves as before (package 01): it cannot fail.
- `<version>` is a SemVer version (`major.minor.patch`, optional prerelease label). Any other value ends with exit code 2 `invalid-arguments`. **Proposed.**

### Match rule

**Decided** on 2026-11-04.

| Engine version | Major and minor equal to `--expect` | Result |
|---|---|---|
| Release (no prerelease label) | yes (patch may differ) | Success |
| Release | no | Failure: exit code 50 `version-mismatch` |
| Prerelease (for example `0.8.0-feature-06.3`) | yes | Success |
| Prerelease | no | Success with the warning `engine-prerelease-version-mismatch` |

The prerelease label of `--expect` is not compared. **Proposed.**

### Result

- Success: the envelope of `version` (package 01) with the passed `expectedVersion` in addition. **Proposed.**
- Failure: a failure envelope with `version-mismatch`. The message names both versions and what to update **(Proposed)**:
  - Engine lower than the skill: update the engine.
  - Engine higher than the skill: update the kit skills. Until package 07 builds `integrations update`, the message names updating the skills by hand.
- Plain output: the same message, exit code 50.

### Exit and warning codes

- New range **50–59, versions and integrations**, for this package and package 07. **Decided** on 2026-11-04.
- `50` `version-mismatch` (`ShadeKitCliExitCodes`, `ShadeKitCliErrorCodes`, `CliErrorMapper`).
- Warning `engine-prerelease-version-mismatch` (`ShadeKitCliWarningCodes`). **Proposed** (name).

### `CLAUDE.md`

- The sentence that `version` cannot fail and therefore writes its envelope through `ICliJsonWriter` changes: `version --expect` can fail and goes through `ShadeKitCommandContext` like every other command. **Proposed** (how the command is changed).
- The range 50–59 is added to the list of exit code ranges.

## Acceptance criteria

- **FP06-1-AC1** `shade-kit version --json --expect <v>` against a release engine whose major or minor differs from `<v>` ends with exit code 50 and a failure envelope with the error code `version-mismatch`; the message names both versions. The plain output gives the same message and exit code.
- **FP06-1-AC2** The mismatch message names updating the engine when the engine version is lower than `<v>`, and updating the skills when it is higher.
- **FP06-1-AC3** With equal major and minor and a different patch, the command succeeds without a warning.
- **FP06-1-AC4** A prerelease engine whose major or minor differs from `<v>` succeeds with the warning `engine-prerelease-version-mismatch`.
- **FP06-1-AC5** An `--expect` value that is not a SemVer version ends with exit code 2 `invalid-arguments`.
- **FP06-1-AC6** Without `--expect`, the output and exit code of `version` are unchanged.

## Open points

- Whether the success envelope carries `expectedVersion` (see above).
- Name of the warning code.
