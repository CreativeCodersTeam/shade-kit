# 06-1 · Engine version check (`version --expect-contract`)

| | |
|---|---|
| Status | Draft, 2026-11-04 (split from package 06), updated 2026-11-05 and 2026-11-06 (implementation clarification and implementation: strict SemVer 2.0.0 for `--expect`, the match on major and minor, `expectedVersion` in the envelope, the messages, `--expect` without a value and the `=` form, exit code 50, warning code `engine-prerelease-version-mismatch`), 2026-11-13 (package 07 implementation: the mismatch message of an engine higher than the skill names `shade-kit integrations install`; exit codes 51–53 in the range 50–59), and 2026-11-22 (the CLI contract replaces the SemVer check: `CliContract` with `Current` and `MinSupported`, `version --expect-contract <n>`, `data.contract` and `data.expectedContract`, exit codes 54 `engine-outdated` and 55 `skills-outdated`; `--expect`, exit code 50 `version-mismatch`, and the warning `engine-prerelease-version-mismatch` removed; analysis in [Background](#background-why-a-cli-contract); scenario runs S18 and S19). Implemented. |
| Source | [06 Skill foundation](06-skill-foundation.md) (version handshake), [concept §9.6](../../research/shade-kit-concept.md#9-version-drift), [Background: why a CLI contract](#background-why-a-cli-contract) (analysis of 2026-11-21) |
| Kind | .NET (engine and CLI) |
| Depends on | 01 |
| Enables | 06-2, 06-5, 06-6, 06-9 |

## Benefit

A skill learns with one CLI call whether the installed engine supports the CLI contract the skill is written for. The engine applies the window rule, so every agent gets the same answer, and the skill text does not have to compare numbers itself. The contract is independent of the product version, so a development build is checked like a release, and a release that changes nothing a skill can see needs no skill change.

## Scope

### In scope

- The CLI contract of the engine (`CliContract`) and its window.
- The option `--expect-contract <n>` of `shade-kit version`.
- The window rule, the result in JSON and plain output, and the new exit and error codes.
- Removing `--expect`, its match rule, exit code 50, and the warning `engine-prerelease-version-mismatch` (2026-11-22).
- The `CLAUDE.md` changes that follow from it.

### Out of scope

- Where a skill states its contract and how it calls the check (06-2, 06-5, 06-6, 06-9).
- Installed integrations in `version --json` (package 07).
- A snapshot test of the contract surface (open point in the [README](README.md#open-questions)).

## Functional description

**Implemented** on 2026-11-22 in the Core namespace `CreativeCoders.ShadeKit.Core.Versions` (`Core/Versions/`): `CliContract`, `CliContractWindow`, `ICliContractChecker` / `CliContractChecker` (a singleton that `AddShadeKitCore()` registers with `CliContract.Window`), and the exceptions `EngineContractOutdatedException` and `SkillsContractOutdatedException` in `Versions/Exceptions/`. `SemanticVersion` stays; the integrations use it (package 07). The CLI part is `VersionCommand`, `VersionCommandOptions.ExpectContract`, `VersionCommandData.Contract` and `VersionCommandData.ExpectedContract`, `VersionCommandContractData`, `ExpectedContractOptionConverter`, `ExpectedContractInvalidException`, and `CliErrorMapper`. Removed on 2026-11-22: `IEngineVersionChecker`, `EngineVersionChecker`, `EngineVersionCheckResult`, `EngineVersionMismatchDirection`, `EngineVersionMismatchException`, `InvalidSemanticVersionException`, `EnginePrereleaseVersionMismatchWarning`, `ExpectedVersionOptionConverter`, and `ExpectedVersionMissingException`.

### CLI contract

- The CLI contract is the numbered agreement between the engine and the kit skills. It covers everything the kit skills rely on: the agent-facing commands and their options, the fields of the `CliJsonEnvelope`, the fields of its `data` object, and the error and warning codes. **Decided** on 2026-11-22 (option (d) of [Background](#background-why-a-cli-contract)). **Implemented** (`CliContract`).
- The engine supports a window of contracts, from `CliContract.MinSupported` to `CliContract.Current`. `Current` rises with every change of the contract that the skills can see, additive ones included (a new option, a new `data` field, a new code); `MinSupported` rises only when a part of the contract is removed or reinterpreted, so that skills written for an older contract would no longer work. **Decided** on 2026-11-22.
- The contract numbers are independent of the product version that GitVersion derives; a release does not change them unless the contract changes. Every build of the same code has the same contract, so there is no leniency for a prerelease engine. **Decided** on 2026-11-22.
- Both start at `1` (`CliContract.Current = 1`, `CliContract.MinSupported = 1`). **Implemented** (`CliContractTests.Window_Constants_AreCurrentAndMinSupported`).
- `CliContractWindow` requires `MinSupported` ≥ 1 and `Current` ≥ `MinSupported`, so a requested contract `0` lies below every window. **Implemented** (`CliContractWindowTests`).
- Whether `Current` rises with every skill-visible change is a manual review duty; a snapshot test that would guard it is an open point ([README](README.md#open-questions), D-12 of the implementation). **Decided** on 2026-11-22.

### Option `--expect-contract`

- `shade-kit version [--json] --expect-contract <n>` checks the CLI contract `<n>` that the calling skill is written for against the window of the engine. **Decided** on 2026-11-22. **Implemented** (`VersionCommandOptions.ExpectContract`, `VersionCommand`, `CliContractChecker`).
- `<n>` is a contract number: a non-negative integer that fits an `int`, written in ASCII digits without a sign, leading zeros, or whitespace (`0`, `1`, `42`; not `01`, `+1`, ` 1`, `1.0`, `2147483648`). Any other value ends with exit code 2 `invalid-arguments` while the arguments are read; so does `--expect-contract` without a value (the last argument, or followed by an argument that starts with `-`, so `--expect-contract -1` counts as no value). **Decided** on 2026-11-22. **Implemented** (`ExpectedContractOptionConverter` throws `ExpectedContractInvalidException`, which `CliErrorMapper.MapArgumentError` maps; `ExpectedContractOptionConverterTests`, `VersionCommandOptionsTests`, `CliErrorMapperTests`, `ProgramTests`).
- With `--expect-contract`, `version` runs the check through `ShadeKitCommandContext` and does not read the installed integrations: no agent CLI is started, and `data.integrations` is left out (plain mode: no integration lines). This keeps the handshake fast (07 measured 0.73 s with `claude` on the `PATH` against 0.08 s without, for the earlier `--expect` call). **Decided** on 2026-11-22. **Implemented** (`VersionCommandTests.ExecuteAsync_ExpectContract_DoesNotReadIntegrationsAndOmitsField`, `VersionCommandTests.ExecuteAsync_ContractMismatch_DoesNotReadIntegrations`, `VersionCommandTests.ExecuteAsync_PlainModeExpectContract_WritesNoIntegrationLines`).
- Without `--expect-contract`, `version` reads and lists the integrations as before (package 07) and cannot fail because of them. **Implemented** (`VersionCommandTests.ExecuteAsync_WithoutExpectContract_DoesNotCheckContract`, `VersionCommandTests.ExecuteAsync_JsonModeWithoutExpectContract_WritesIntegrations`).
- The value follows the option after a space. The option parser ignores every unknown option of every command, and it has no `--expect-contract=<n>` form: `--expect-contract=999` is an unknown option, so `version` succeeds without a check (exit code 0). Skills call exactly `--expect-contract <n>`. **FACT** (run on 2026-11-22 against the Debug build of this branch: `version --json --expect-contract=999` returned exit code 0 and `ok: true` without `expectedContract`; `version --json --expect-contract 999` returned exit code 54; unknown options in general: `ProgramTests.RunAsync_JsonWithUnknownOption_IgnoresOptionAndWritesSuccessEnvelope`). The same holds for the removed option: `version --json --expect 0.9.0` is ignored and exits 0 without a check (`ProgramTests.RunAsync_VersionJsonExpectOldOption_IsNotChecked`). **Decided** on 2026-11-22 (D-11: `--expect` removed completely, not kept as an alias).

### Window rule

**Decided** on 2026-11-22. **Implemented** (`CliContractChecker`; `CliContractCheckerTests`).

| Requested contract `R` | Result |
|---|---|
| `MinSupported` ≤ `R` ≤ `Current` | Success |
| `R` > `Current` (the skills are newer than the engine) | Failure: exit code 54 `engine-outdated` |
| `R` < `MinSupported` (the skills are older than the engine supports) | Failure: exit code 55 `skills-outdated` |

### Result

- `version --json` always has `data.contract` (`{"current": 1, "minSupported": 1}`), after `formats`. **Implemented** (`VersionCommandData.Contract`, `VersionCommandContractData`; `VersionCommandTests.ExecuteAsync_JsonMode_WritesContractWithCurrentAndMinSupported`, `ProgramTests.RunAsync_VersionJson_WritesContract`).
- Success with `--expect-contract`: the envelope of `version` with `expectedContract`, the checked number, in addition and without `integrations`. Without `--expect-contract`, `expectedContract` is left out. **Implemented** (`VersionCommandData.ExpectedContract`; `VersionCommandTests.ExecuteAsync_JsonModeExpectContract_WritesExpectedContract`, `VersionCommandTests.ExecuteAsync_JsonModeWithoutExpectContract_OmitsExpectedContract`, `ProgramTests.RunAsync_VersionJsonExpectContract1_WritesSuccessWithExpectedContract`).
- Failure: a failure envelope with `engine-outdated` or `skills-outdated`. The message names the engine version, the window, the requested contract, and the remedy. **Implemented** (`CliErrorMapper.Map`; `CliErrorMapperTests.Map_EngineContractOutdated_Returns54WithUpdateEngineMessage`, `CliErrorMapperTests.Map_SkillsContractOutdated_Returns55WithIntegrationsInstallMessage`). `{engine}` is the engine version, `{min}` and `{current}` the window, `{n}` the requested contract:
  - `engine-outdated`: `The SHADE kit engine {engine} supports the CLI contracts {min} to {current}, but the kit skills need the CLI contract {n}. Update the engine, then run the skill again.`
  - `skills-outdated`: `The kit skills are written for the CLI contract {n}, but the SHADE kit engine {engine} supports only the CLI contracts {min} to {current}. Run 'shade-kit integrations install' to install the matching kit skills, then run the skill again.` For a release engine whose tag exists, the hint cannot loop the way the 50 hint could (07): the guard `SkillContractTests` makes every tagged tree carry skills with `R == Current` of the engine built from it (06-2, [07 FP07-AC2](07-claude-code-integration.md#acceptance-criteria)). An engine that installs from `main` (a prerelease engine, or a release engine whose tag does not exist, 07) gets the skills of `main`, whose `R` can lie below its `MinSupported`, so the hint can repeat once `MinSupported` > 1; this is latent while `MinSupported` = 1.
- Invalid `--expect-contract`: `Invalid arguments: --expect-contract needs a CLI contract number such as 1, but got none. Run 'shade-kit version --help' for usage.`, and `… but got '{value}'. …` for a value that is not a contract number. **Implemented** (`CliErrorMapper.MapArgumentError`; `CliErrorMapperTests.MapArgumentError_ExpectedContractInvalidWithoutValue_ReturnsInvalidArgumentsWithGotNone`, `CliErrorMapperTests.MapArgumentError_ExpectedContractInvalidWithValue_ReturnsInvalidArgumentsNamingValue`).
- Plain output: line 1 `shade-kit <engine version>`, line 2 `CLI contract <current> (supported: <min> to <current>)`, then, without `--expect-contract`, one line per installed integration. A failed check writes only the message, with exit code 54 or 55. **Implemented** (`VersionCommandTests.ExecuteAsync_PlainMode_WritesContractLineAfterVersionLine`, `ProgramTests.RunAsync_VersionPlain_WritesContractLineAfterVersionLine`, `ProgramTests.RunAsync_VersionPlainExpectContract999_WritesOnlyMessageAndReturns54`).
- Where a plain failure is written follows the CLI host, as before: a contract outside the window fails inside the command, so the message goes to stdout; an invalid or missing value fails while the arguments are read, so the message goes to stderr; in JSON mode every case writes one envelope to stdout and nothing else. **Implemented** (`ProgramTests.RunAsync_VersionPlainExpectContract999_WritesOnlyMessageAndReturns54`, `ProgramTests.RunAsync_VersionPlainExpectContractWithoutValue_WritesMessageToStandardErrorAndReturns2`).

### Exit and warning codes

- Range **50–59, versions and integrations**, for this package and package 07. **Decided** on 2026-11-04. **Implemented** (`ShadeKitCliExitCodes`).
- `54` `engine-outdated` (`ShadeKitCliExitCodes.EngineOutdated`, `ShadeKitCliErrorCodes.EngineOutdated`) and `55` `skills-outdated` (`ShadeKitCliExitCodes.SkillsOutdated`, `ShadeKitCliErrorCodes.SkillsOutdated`). **Decided** on 2026-11-22. **Implemented** (`ShadeKitCliExitCodesTests`, `ShadeKitCliErrorCodesTests`).
- `51` `agent-not-found`, `52` `agent-command-failed`, and `53` `integration-conflict` of package 07 are in the same range since 2026-11-13 ([07](07-claude-code-integration.md#exit-error-and-warning-codes)). **Implemented**.
- `50` `version-mismatch` and the warning `engine-prerelease-version-mismatch` are removed; `50` is unused. **Decided** on 2026-11-22. **Implemented**.

### `CLAUDE.md`

- `Core/Versions/` in the repository layout names the contract types, the command example is `version --json --expect-contract <n>`, the `ICliValueConverter` example is `ExpectedContractOptionConverter`, and the skill conventions describe the contract handshake and the guard `SkillContractTests`. **Implemented** on 2026-11-22.

## Acceptance criteria

Status as of 2026-11-22. Every criterion is **Implemented**. The tests that carried `[Trait("Requirement", "FP06-1-AC1")]` to `FP06-1-AC5` were deleted with the `--expect` check; the new tests carry no FP06-1 trait and are named here by class and method. Two tests of the unchanged path still carry `FP06-1-AC6`: `VersionCommandTests.ExecuteAsync_PlainMode_WritesToolNameAndEngineVersion` and `VersionCommandTests.ExecuteAsync_JsonModeWithoutExpectContract_WritesEnvelopeWithContractAndIntegrations`.

- **FP06-1-AC1** `shade-kit version --json --expect-contract <n>` with `<n>` above `CliContract.Current` ends with exit code 54 and a failure envelope with the error code `engine-outdated`, whose message tells to update the engine; the plain output gives the same message and exit code. Proven by `CliContractCheckerTests.Check_RequestedAboveCurrent_ThrowsEngineContractOutdatedExceptionWithContracts`, `VersionCommandTests.ExecuteAsync_JsonModeEngineOutdated_WritesEngineOutdatedEnvelopeAndAbortsWith54`, `VersionCommandTests.ExecuteAsync_PlainModeEngineOutdated_AbortsWith54AndMessageWithoutOutput`, `CliErrorMapperTests.Map_EngineContractOutdated_Returns54WithUpdateEngineMessage`, `ProgramTests.RunAsync_VersionJsonExpectContract999_WritesEngineOutdatedAndReturns54`, `ProgramTests.RunAsync_VersionPlainExpectContract999_WritesOnlyMessageAndReturns54`.
- **FP06-1-AC2** With `<n>` below `CliContract.MinSupported` the command ends with exit code 55 and the error code `skills-outdated`, whose message names `shade-kit integrations install`. Proven by `CliContractCheckerTests.Check_RequestedBelowMinSupported_ThrowsSkillsContractOutdatedExceptionWithContracts`, `CliContractCheckerTests.Check_RequestedZeroWithDefaultWindow_ThrowsSkillsContractOutdatedException`, `VersionCommandTests.ExecuteAsync_JsonModeSkillsOutdated_WritesSkillsOutdatedEnvelopeAndAbortsWith55`, `VersionCommandTests.ExecuteAsync_PlainModeSkillsOutdated_AbortsWith55AndMessageWithoutOutput`, `CliErrorMapperTests.Map_SkillsContractOutdated_Returns55WithIntegrationsInstallMessage`, `ProgramTests.RunAsync_VersionJsonExpectContract0_WritesSkillsOutdatedAndReturns55`.
- **FP06-1-AC3** With `<n>` inside the window the command succeeds without a warning and echoes `<n>` as `expectedContract`; this holds for a prerelease engine too, since the check does not look at the product version. Proven by `CliContractCheckerTests.Check_RequestedWithinWindow_Succeeds`, `VersionCommandTests.ExecuteAsync_JsonModeExpectContractInWindow_WritesSuccess`, `VersionCommandTests.ExecuteAsync_JsonModeExpectContractWithRealChecker_SucceedsForEveryContractInWindow`, `VersionCommandTests.ExecuteAsync_JsonModeExpectContract_WritesExpectedContract`, `ProgramTests.RunAsync_VersionJsonExpectContract1_WritesSuccessWithExpectedContract`.
- **FP06-1-AC4** With `--expect-contract`, `version` does not read the integrations: no agent CLI is started, `data.integrations` is left out, and the plain output has no integration lines, also when the check fails. Proven by `VersionCommandTests.ExecuteAsync_ExpectContract_DoesNotReadIntegrationsAndOmitsField`, `VersionCommandTests.ExecuteAsync_ContractMismatch_DoesNotReadIntegrations`, `VersionCommandTests.ExecuteAsync_PlainModeExpectContract_WritesNoIntegrationLines`.
- **FP06-1-AC5** `--expect-contract` without a value, or with a value that is not a contract number, ends with exit code 2 `invalid-arguments`; the removed `--expect` is not checked. Proven by `ExpectedContractOptionConverterTests` (`Convert_Null_ThrowsExpectedContractInvalidException`, `Convert_InvalidText_ThrowsExpectedContractInvalidExceptionWithValue`, `Convert_ValidText_ReturnsInt`), `VersionCommandOptionsTests` (`Options_ExpectOption_DoesNotExist`, `Parse_ExpectContractWithoutValue_ThrowsExpectedContractInvalidExceptionWithoutValue`, `Parse_ExpectContractNotANumber_ThrowsExpectedContractInvalidExceptionWithValue`), `CliErrorMapperTests.MapArgumentError_ExpectedContractInvalidWithoutValue_ReturnsInvalidArgumentsWithGotNone`, `CliErrorMapperTests.MapArgumentError_ExpectedContractInvalidWithValue_ReturnsInvalidArgumentsNamingValue`, `ProgramTests.RunAsync_VersionJsonExpectContractNotANumber_WritesInvalidArgumentsAndReturns2`, `ProgramTests.RunAsync_VersionExpectContractWithoutValue_WritesInvalidArgumentsAndReturns2`, `ProgramTests.RunAsync_VersionJsonExpectOldOption_IsNotChecked`.
- **FP06-1-AC6** Without `--expect-contract`, `version` reads and lists the integrations as before and cannot fail; the output carries the contract window (`data.contract`, plain line 2). Proven by `VersionCommandTests.ExecuteAsync_PlainMode_WritesToolNameAndEngineVersion` and `VersionCommandTests.ExecuteAsync_JsonModeWithoutExpectContract_WritesEnvelopeWithContractAndIntegrations` (both `FP06-1-AC6`), `VersionCommandTests.ExecuteAsync_WithoutExpectContract_DoesNotCheckContract`, `ProgramTests.RunAsync_VersionJson_WritesContract`.

## Scenario runs

**2026-11-22**, the refused handshake with the CLI contract (procedure of `tests/skill-scenarios/README.md`, step 5). The handshake runs of the skill packages before this date used `--expect` ([06-5](06-5-phase-entry-skill.md#acceptance-criteria), [06-6](06-6-next-phase-skill.md#acceptance-criteria), [06-9](06-9-skill-skeleton-alignment.md#acceptance-criteria)); these two scenarios cover the new refusals. One fresh runner subagent per scenario, as the README sets out; the refusal fixtures come from the real engine through the stand-in calls `--expect-contract 999` (`env-version-engine-outdated.json`) and `--expect-contract 0` (`env-version-skills-outdated.json`).

| # | Scenario | Agent played | Checks |
|---|---|---|---|
| S18 | `shade-kit-new`, the engine is older than the skills (`engine-outdated`) | Claude Code | pass |
| S19 | `shade-kit-next-phase`, the skills are older than the engine (`skills-outdated`) | OpenAI Codex CLI, default mode | pass |
| | Total (`check_runs.py --only S18,S19`, final checker) | | 22 of 22 |

- Both skills stopped at the handshake with exactly one CLI call (`shade-kit version --json --expect-contract 1`), asked the human nothing, and ended through `shade-kit-next-steps` with the refusal and the right remedy: S18 to update the engine, then call `/shade-kit-new` again; S19 to run `shade-kit integrations install`, start a new agent session, then call `$shade-kit:shade-kit-next-phase` again.
- Unclear points the runners reported: (1) the `error.message` of the refusal fixtures names the contract 999 or 0, while the skill passes 1; this is inherent, because the real engine cannot produce these errors for 1, and it is documented in the scenario README; (2) step 5.3 of `shade-kit-next-steps` does not say what to do when `phase next` never ran; both runners left that part out (open, not changed in this round, [README](README.md#open-questions)); (3) the `skills-outdated` row of the refusal table adds "start a new agent session" to the advice of the envelope; intended, because an agent loads the new skills only in a new session.
- Known checker gap (D-23, **Closed** in review round 1 on 2026-11-22, open before): the S19 check that the options do not tell to update the engine (`ENGINE_UPDATE` in `checks/check_runs.py`) missed an engine update named only through the tool ("get a newer shade-kit") and flagged "install the kit plugin that matches the engine" as an engine update; the S18 check that the options say to update the engine uses the same pattern. The task was marked blocked by the user after two verification rounds and recorded as an open point in the [README](README.md#open-questions). Closed in review round 1 (finding M4), refined in review round 2 (finding M6): `ENGINE_UPDATE` now also counts an engine update named through the tool ("get a newer shade-kit", "install the latest shade-kit", "upgrade the shade-kit CLI", "Update SHADE kit", `dotnet tool update`), also with the verbs download and replace and the -s/-ing forms and as "the engine is/was too old" or "outdated", and no longer counts the skills remedy when a word between the verb and "engine" or the tool, or a skills word in the clause before "newer engine" or in the 40 characters after it, names the skills, a plugin, the integration or integrations, the marketplace, or the kit on its own ("install the kit plugin that matches the engine", "reinstall the SHADE kit integration for the engine", "Install the skills that match the newer engine"); "the kit engine" counts as the engine, and the call form `$shade-kit:` before a skill name does not count as the tool (finding M7); negations ("you do not need to update the engine") and paraphrases without a listed verb ("bring the engine up to date") remain a known limit of the heuristic (review round 3, 2026-11-22). S18 and S19 checked again on the same runner outputs: 22 of 22.

## Background: why a CLI contract

The analysis of 2026-11-21 that led to the CLI contract, with its findings and sources. It was written as a separate report, which was removed on 2026-11-24; its content is kept here. **FACT** marks what was checked in this repository on 2026-11-21; the rest names its source or is marked **Unverified**.

### The lockstep before 2026-11-22

- The repository had one computed version: GitVersion derived it from the git history and `next-version: v0.9.0`, embedded it as the informational version, and `EngineVersionProvider` read it at run time. **FACT** (`GitVersion.yml`, `src/CreativeCoders.ShadeKit.Core/Engine/EngineVersionProvider.cs`).
- The skills kept a second copy by hand: all 19 `SKILL.md` carried `version: 0.9.0` in their frontmatter, five skills ran the handshake with the version in its text, and two command lists repeated it, so a raise touched 26 lines in 19 files. **FACT** (`git grep -c 0.9.0 -- skills`). The copy in the text was needed because the model does not see the frontmatter: Claude Code hides it (2.1.318, 06-2), Copilot CLI removes it since 1.0.48 ([Copilot CLI changelog](https://github.com/github/copilot-cli/blob/main/changelog.md)), and Codex shows the raw file but evaluates only `name`, `description`, and `metadata.short-description` ([openai/codex `parser.rs`](https://github.com/openai/codex/blob/main/codex-rs/skills/src/parser.rs)).
- `SkillKitVersionTests` tied both copies to the GitVersion version of the build that ran the test, so its result depended on the branch, the local tags, and `next-version`, not only on the checked-in files. **FACT**. At run time the rule was the opposite: a major.minor mismatch ended `version --expect` with exit code 50, but a prerelease engine only warned. On the branch engine `0.9.0-rework-versioning-branch-mgmt.4`, `--expect 0.8.0` and `--expect 1.0.0` both returned `ok: true`. **FACT** (run on 2026-11-21). So on every feature branch the handshake checked nothing, while the guard insisted on an exact major.minor, and the refusal path was never exercised locally.
- A branch engine without `SHADE_KIT_MARKETPLACE_SOURCE` installs the skills of `main`, neither the skills of the branch nor those the engine was tested with (`IntegrationService`, package 07).
- Two of the three raises (0.7 → 0.8 → 0.9) were forced: the first because the new guard failed after the tag `v0.8.0`, the second because `shade-kit-new` needed `feature new --move-source`, which 0.8 engines do not know (06-2, package 09). The handshake could not say "needs engine ≥ 0.9.1", because it ignored the patch, so every skill dependency on the engine forced a kit-wide minor across more than 20 files and `GitVersion.yml`, even when 18 of 19 skills were unchanged; and an engine-only minor forced every skill to be rewritten. The root cause: one number had to say which product build this is (changes with every commit, owned by GitVersion) and which CLI contract the skills rely on (should rarely change, owned by the authors).

### Constraints

- One skill source for all three agents, without agent-specific manifests (`SingleSkillSourceTests`, package 08), and no transformation at install time: the plugin is installed directly from the git tree at the tag or at `main`.
- The model does not see the frontmatter in Claude Code and Copilot CLI.
- The option parser ignores unknown options silently: `--expect=1.2.3` ended with exit code 0 without a check. **FACT**. This constraint decides the strict guard below.

### Options compared

| Option | Changes per ordinary release | Changes for a new skill-visible CLI feature | Guard tied to GitVersion | Check works on development builds | Agent-neutral | Rework of the integrations |
|---|---|---|---|---|---|---|
| (a) Lockstep, stamped automatically | 0–1 (merge a release PR) | kit-wide minor | yes | no | yes | none |
| (b) Version read from a shipped file | 1 file | kit-wide minor | yes | no | **no** (path per agent) | none |
| (c) Independent product SemVer with mutual minimum versions | 0 | raise the minimum (easily forgotten) | no | **no** (SemVer prerelease order) | yes | **large** |
| (d) Contract version with a window | **0** | 1 constant and the handshake lines, guarded by a test | **no** | **yes** | yes | none |
| (e) Capability query only | 0 | check logic in every skill | no | yes | yes | none |

- **(a)** saves typing but keeps the coupling. The best fit was release-please in manifest mode, the only tool examined that writes versions into arbitrary text files through a release PR before the tag ([release-please manifest releaser](https://github.com/googleapis/release-please/blob/main/docs/manifest-releaser.md)); its generic updater needs a marker on each version line ([`generic.ts`](https://github.com/googleapis/release-please/blob/main/src/updaters/generic.ts)), and a marker such as `version: 0.9.0 # x-release-please-version` broke the regex of the guard, which read the comment as part of the value (**FACT**). Tags created with the default `GITHUB_TOKEN` probably start no further workflows (**Unverified**). GitVersion, MinVer, and Nerdbank.GitVersioning only compute versions and never write Markdown ([GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration), [MinVer README](https://github.com/adamralph/minver/blob/main/README.md), [NBGV `version.json`](https://dotnet.github.io/Nerdbank.GitVersioning/docs/versionJson.html)). The prerelease gap, the branch-installs-`main` effect, and the missing patch dependency stay.
- **(b)** fails agent neutrality. Claude Code replaces `${CLAUDE_SKILL_DIR}` and `${CLAUDE_PLUGIN_ROOT}` in the skill text and can run commands beforehand with `` !`…` `` ([Claude Code skills](https://code.claude.com/docs/en/skills)), but a failed `` !`…` `` aborts the whole skill, the explanation of a refusal included. Copilot documents no substitution in `SKILL.md`, so a placeholder would arrive literally ([Copilot CLI plugin reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-plugin-reference)); whether Copilot tells the model the absolute skill path is **Unverified**; Codex does ([`catalog_prompt.rs`](https://github.com/openai/codex/blob/main/codex-rs/ext/skills/src/catalog_prompt.rs)). A `version` in `plugin.json` is ruled out too: no agent passes it to the model, and in Claude Code it freezes users on the cached plugin until the string changes, which would stop the SHA-based updates of the `main` fallback ([Claude Code plugin loading](https://code.claude.com/docs/en/plugins/loading.md)). Letting the engine read the frontmatter through a `--skill-dir` option only lowers the number of places.
- **(c)** is the right idea on the wrong axis. The marketplace ref `v<engine>` rests on the lockstep; separate streams need separate tags (for example `skills-v*`), a mapping from engine version to skills tag, a tag list instead of one `git ls-remote` per tag, and changes to `MarketplaceSource`, all three `*Integration.cs`, and `release.yml`, the most invasive variant. A minimum on product SemVer fails exactly for local testing: a prerelease has a lower precedence than its release ([SemVer 2.0.0](https://semver.org/)), so a branch build `0.10.0-feature.4` does not satisfy `>= 0.10.0`; Terraform avoids this by not matching prereleases in ranges at all ([Terraform version constraints](https://developer.hashicorp.com/terraform/language/expressions/version-constraints)). Product versions change with every release, so a minimum tied to them is noisy and easily forgotten. And a skills SemVer has no consumer: Claude Code versions the plugin by commit SHA, Codex by the fixed value `local` ([openai/codex `store.rs`](https://github.com/openai/codex/blob/main/codex-rs/core-plugins/src/store.rs)), and only the own guard read `metadata.version`.
- **(d)** is the pattern of mature host-plugin systems: the Terraform plugin protocol v5/v6 is independent of the CLI version ([Terraform plugin protocol](https://developer.hashicorp.com/terraform/plugin/terraform-plugin-protocol)); the Docker Engine API has a configurable minimum and refuses older clients with "client version 1.43 is too old. Minimum supported API version is 1.44" ([Docker Engine v29](https://www.docker.com/blog/docker-engine-version-29/)); MCP separates `protocolVersion` from `serverInfo.version` ([MCP lifecycle](https://modelcontextprotocol.io/specification/2025-06-18/basic/lifecycle)). The repository already had the model: integer format versions per file that change "only when the structure changes, not with every engine release", with `minEngineVersion` and `formats` in `version --json` (`FormatRegistry`, package 01).
- **(e)**, a capability query in the style of LSP ([LSP 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)), would drop version numbers altogether but moves the check logic into Markdown that the model runs, where it is least reliable.

### Decision details

- Chosen: (d), with the Docker-style window of this package. The contract had no version before: the envelope carried only `ok`, `data`, `warnings`, and `error` (**FACT**, `CliJsonEnvelope`).
- The guard is strict: every handshake names `R == Current`, not only `MinSupported ≤ R ≤ Current`. Reason: the parser ignores unknown options. Without the coupling, a skill could use a new option without raising `R`; an older engine inside the window would ignore the option and answer exit code 0, a wrong result without any sign. Because the release engine and the skills come from the same tag, the strict guard costs nothing. Forgetting to raise the minimum is named in the literature as the main risk of minimum-version schemes, without a cited post-mortem.
- The proposed contract snapshot test (reflection over `[CliCommand]`, the option attributes of the `JsonCommandOptions` commands, the `*CommandData` types, `ShadeKitCliErrorCodes`, and `ShadeKitCliWarningCodes`, compared with a checked-in file that names the contract number) was not built (D-12, open point below).
- Distribution is unchanged: `IntegrationService` installs the tag `v<engine>` and falls back to `main`. Because the guard enforces `R == Current` in the tag commit, a release engine always installs skills inside its window, so the loop that the old guard prevented (exit code 50, then `integrations install` fetches the same stale skills, package 07) cannot occur. The check now also protects the override and the `main` paths.
- The product version stays with GitVersion. `next-version` loses its skill role; after the first tag at or above 0.9.0 it could be replaced by `+semver: minor` in commit messages (`commit-message-incrementing` is on by default, [GitVersion 7.0 configuration](https://gitversion.net/7.0/docs/reference/configuration)). Whether GitVersion ignores `next-version` once such a tag exists is **Unverified**. The skills carry no `metadata.version`; the release number of the kit is the tag. Releasing skills on their own cadence later would need no change of the check: the installer would pick the newest `skills-v*` tag whose `R` lies in the window of the engine. Not needed today.
- Cost per event: an ordinary release (engine fix, skill text change, format change, product minor) needs no version change, only the tag; a new skill-visible CLI feature needs `CliContract.Current`, the five handshake sentences, and the two command lists (06-2); a breaking change also needs `MinSupported`. Before, a product minor alone cost 26 lines and `GitVersion.yml`.

### Timing of the switch

- Switched in one step, without a transition with two handshake options, before the first installable release tag: `v0.7.0` and `v0.8.0` have no `.claude-plugin/` (**FACT**, `git ls-tree`), and `integrations` came after `v0.8.0` (#34). So no released engine that can install skills ignores `--expect-contract` as an unknown option. Remaining risk: tool packages pushed to GitHub Packages from `main` or a feature branch between the tags (accepted, D-10, 06-2).

### Local testing

- Every build of the same code has the same contract, and the guard does not depend on local tags, so `dotnet run --project src/CreativeCoders.ShadeKit.Cli` and a locally packed tool are checked like a release. Skills from the working tree:

  ```sh
  SHADE_KIT_MARKETPLACE_SOURCE=$PWD dotnet run --project src/CreativeCoders.ShadeKit.Cli -- integrations install
  dotnet run --project src/CreativeCoders.ShadeKit.Cli -- version --json --expect-contract 1
  ```

- Claude Code loads a plugin with a relative source from a marketplace added by local path in place; changes take effect in the next session or after `/reload-plugins`, without a version raise ([Claude Code plugin loading](https://code.claude.com/docs/en/plugins/loading.md)); `claude --plugin-dir .` works without the engine. Copilot CLI loads plugins from a directory marketplace live since 1.0.81 ([Copilot CLI changelog](https://github.com/github/copilot-cli/blob/main/changelog.md)). Codex copies the plugin into the cache folder `local` ([Codex build plugins](https://developers.openai.com/codex/plugins/build)); whether it picks up changes without a new `integrations update` is **Unverified**.
- The refusal paths are tested without a release build or a tag: end-to-end tests and the scenario fixtures use `--expect-contract 999` and `--expect-contract 0`.
- To be confirmed by a scenario run per agent: that all three agents run the handshake sentence from the text unchanged (true for the old wording), and how Copilot and Codex name the skill path, should the handshake number later move into a file of the plugin. Until both are shown for all three agents, the number in the text is the only lossless, agent-neutral form.
- Side finding, not part of this change: `init` writes the prerelease version of a branch build as `kitVersion` into `config.json` and `templates.lock.json` of the target repository (`RepositoryInitializer`); recorded in package 18.

## Replaced on 2026-11-22: the SemVer check of `--expect` (history)

Replaced on 2026-11-22 by the CLI contract above ([Background](#background-why-a-cli-contract)). Reason: one product version, kept by hand in every skill, carried two meanings; every skill-visible change forced a kit-wide product minor (and `next-version` in `GitVersion.yml`), the guard depended on the branch and the tags of the build, and a prerelease engine was not checked at all. The text below is the state before 2026-11-22; its **Implemented** labels name code that has been removed.

- Replaced on 2026-11-22: the strict SemVer 2.0.0 value of `--expect` (now a contract number), the major.minor match rule (now the window rule), the prerelease leniency (none: every build of the same code has the same contract), and the note on `--expect=<version>` (still true of the parser, now for `--expect-contract=<n>`, see above).

### Functional description until 2026-11-22

**Implemented** in the Core namespace `CreativeCoders.ShadeKit.Core.Versions` (`Core/Versions/`): `SemanticVersion`, `IEngineVersionChecker` / `EngineVersionChecker` (a singleton registered by `AddShadeKitCore()`), `EngineVersionCheckResult`, `EngineVersionMismatchDirection`, the exceptions in `Versions/Exceptions/`, and the warning in `Versions/Warnings/`. The CLI part is `VersionCommand`, `VersionCommandOptions.Expect`, `VersionCommandData.ExpectedVersion`, `ExpectedVersionOptionConverter`, and `CliErrorMapper`.

#### Option `--expect`

- `shade-kit version [--json] --expect <version>` compares the engine version with the passed kit version of the calling skill. **Decided** on 2026-11-04. **Implemented** (`VersionCommandOptions.Expect`, `VersionCommand`).
- Without `--expect`, `version` behaves as before (package 01): it cannot fail, and its output is unchanged. **Implemented** (`VersionCommandTests`, `ProgramTests`).
- With `--expect`, `version` runs the check through `ShadeKitCommandContext` like every other command. **Decided** on 2026-11-05. **Implemented** (`VersionCommand`).
- `<version>` is a strict SemVer 2.0.0 version (grammar of semver.org): `major.minor.patch` with numbers without leading zeros (`0` is allowed), an optional prerelease label, and optional build metadata. Build metadata (`+…`) is accepted and ignored. Rejected are, for example, a `v` prefix (`v1.2.3`), whitespace around or inside the value (it is not trimmed), the empty string, two or four parts (`1.2`, `1.2.3.4`), leading zeros (`01.2.3`, `1.2.3-01`), and a number above `int.MaxValue`. Any value that is not a SemVer version ends with exit code 2 `invalid-arguments`. **Decided** on 2026-11-05. **Implemented** (`SemanticVersion`, `InvalidSemanticVersionException`, `CliErrorMapper`; `SemanticVersionTests`).
- `--expect` without a value (the last argument, or followed by an argument that starts with `-`) also ends with exit code 2 `invalid-arguments`. The option parser alone would leave the option unset, so `version` would succeed without a check. **Decided** on 2026-11-06 (implementation). **Implemented**: `ExpectedVersionOptionConverter` throws `ExpectedVersionMissingException` while the arguments are read, and `CliErrorMapper.MapArgumentError` maps it (`ExpectedVersionOptionConverterTests`, `VersionCommandOptionsTests`, `CliErrorMapperTests`, `ProgramTests`).
- The value follows the option after a space. The option parser has no `--expect=<version>` form: `--expect=1.2.3` is an unknown option and is ignored like every unknown option of every command, so `version` succeeds without a check (exit code 0). Skills call exactly `--expect <version>`. **Decided** on 2026-11-06 (implementation: accepted and documented).

#### Match rule

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

#### Result

- Success: the envelope of `version` (package 01) with `expectedVersion`, the value of `--expect` as passed, in addition. Without `--expect`, the field is left out, so the envelope is unchanged. **Decided** on 2026-11-05. **Implemented** (`VersionCommandData.ExpectedVersion`).
- Failure: a failure envelope with `version-mismatch`. The message names both versions and what to update. **Decided** on 2026-11-05. **Implemented** (`EngineVersionMismatchException`, `CliErrorMapper`). `{engine}` is the engine version, `{expected}` the value of `--expect` as passed, and `x.y` the major and minor version:
  - Engine lower than the skill: `The SHADE kit engine {engine} does not fit the kit skills {expected}: the major and minor versions must be equal. Update the engine to x.y, then run the skill again.` (`x.y` of `{expected}`)
  - Engine higher than the skill: `The SHADE kit engine {engine} does not fit the kit skills {expected}: the major and minor versions must be equal. Run 'shade-kit integrations install' to install the SHADE kit skills x.y, then run the skill again.` (`x.y` of `{engine}`). Until 2026-11-13 the message said `Update the SHADE kit skills to x.y by hand`; package 07 built the `integrations` commands and changed it. It names `install`, not `update`: `install` also works when nothing is installed yet, and for an installed agent it behaves like `update`. **Decided** on 2026-11-13 (package 07 implementation, Gate 4). **Implemented** (`CliErrorMapper.CreateEngineVersionMismatchMessage`, `CliErrorMapperTests`). The hint helps only if the tag of the engine carries skills of its major.minor; otherwise `install` would install the same skills again. Since 2026-11-14 the guard test `SkillKitVersionTests` (FP07-AC2) fails every build whose skills carry another major.minor than the engine, the tag build included ([07 Kit version guard](07-claude-code-integration.md#kit-version-guard)). **Decided** on 2026-11-14 (package 07 review rework, Gate 5).
- Warning of a prerelease engine: `The prerelease engine {engine} does not fit the kit skills {expected} (the major and minor versions differ); it is used anyway because it is a prerelease.` **Decided** on 2026-11-05. **Implemented** (`EnginePrereleaseVersionMismatchWarning`, `CliErrorMapper.MapWarning`).
- Invalid `--expect`: `Invalid arguments: '{value}' is not a SemVer version such as 1.2.3 or 1.2.3-beta.1. Run 'shade-kit --help' for usage.` The text names neither `--expect` nor `version`, because the Core `InvalidSemanticVersionException` it maps is generic and a later caller of the SemVer check must not get an `--expect` message (**Decided** on 2026-11-06, code review). For `--expect` without a value: `Invalid arguments: --expect needs a SemVer version such as 1.2.3 or 1.2.3-beta.1, but got none. Run 'shade-kit version --help' for usage.` **Decided** on 2026-11-05 and 2026-11-06. **Implemented** (`CliErrorMapper.Map`, `CliErrorMapper.MapArgumentError`).
- Plain output: on success, the line `shade-kit <engine version>` as before; a warning comes first as a line `Warning: <message>`. On a mismatch, the same message as in JSON mode, exit code 50. **Implemented** (`ShadeKitCommandContext.WriteSuccess`, `VersionCommand`).
- Where a plain failure is written follows the CLI host, like for every other command. **Decided** on 2026-11-06 (implementation: both paths kept and documented):
  - A mismatch and an invalid `--expect` value fail inside the command: the message goes to stdout.
  - `--expect` without a value fails while the arguments are read, like every argument error: the message goes to stderr.
  - In JSON mode, every case writes one envelope to stdout and nothing else.

#### Exit and warning codes

- New range **50–59, versions and integrations**, for this package and package 07. **Decided** on 2026-11-04. **Implemented** (`ShadeKitCliExitCodes`).
- `50` `version-mismatch` (`ShadeKitCliExitCodes.VersionMismatch`, `ShadeKitCliErrorCodes.VersionMismatch`, `CliErrorMapper`). **Implemented**.
- `51` `agent-not-found`, `52` `agent-command-failed`, and `53` `integration-conflict` of package 07 are in the same range since 2026-11-13, with the warning codes `integration-prerelease-ref`, `integration-release-tag-missing`, and `agent-unavailable` ([07](07-claude-code-integration.md#exit-error-and-warning-codes)). **Implemented**.
- Warning `engine-prerelease-version-mismatch` (`ShadeKitCliWarningCodes.EnginePrereleaseVersionMismatch`). **Decided** on 2026-11-05 (name). **Implemented**.

#### `CLAUDE.md`

- The sentence that `version` cannot fail and therefore writes its envelope through `ICliJsonWriter` is replaced: no command uses `ICliJsonWriter` directly, `version` included. **Decided** on 2026-11-05. **Implemented**.
- The range 50–59 is added to the list of exit code ranges, `Core/Versions/` to the repository layout, and `version --json --expect <version>` to the command examples. **Implemented**.

### Acceptance criteria until 2026-11-22

Replaced on 2026-11-22 by the [acceptance criteria](#acceptance-criteria) above. The tests named here were deleted with the `--expect` check, except the two tests of AC6 that still carry `FP06-1-AC6` (see the current FP06-1-AC6 above).

- **FP06-1-AC1** `shade-kit version --json --expect <v>` against a release engine whose major or minor differs from `<v>` ends with exit code 50 and a failure envelope with the error code `version-mismatch`; the message names both versions. The plain output gives the same message and exit code. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC2** The mismatch message names updating the engine when the engine version is lower than `<v>`, and updating the skills when it is higher. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC3** With equal major and minor and a different patch, the command succeeds without a warning. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `ProgramTests`.
- **FP06-1-AC4** A prerelease engine whose major or minor differs from `<v>` succeeds with the warning `engine-prerelease-version-mismatch`. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC5** An `--expect` value that is not a SemVer version ends with exit code 2 `invalid-arguments`. Proven by `EngineVersionCheckerTests`, `VersionCommandTests`, `VersionCommandOptionsTests`, `ExpectedVersionOptionConverterTests`, `CliErrorMapperTests`, `ProgramTests`.
- **FP06-1-AC6** Without `--expect`, the output and exit code of `version` are unchanged. Proven by `VersionCommandTests`.

## Open points

- A contract snapshot test that guards that `CliContract.Current` rises with every skill-visible change: decided again after v1 is complete; until then raising it is a manual review duty (D-12, [README](README.md#open-questions)).

The two points open before 2026-11-22 were answered on 2026-11-05: the success envelope carried `expectedVersion` when `--expect` was passed, and the warning code was `engine-prerelease-version-mismatch` (both replaced on 2026-11-22).
