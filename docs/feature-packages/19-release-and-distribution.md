# 19 · Release and distribution

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (reduced to the remaining scope; CI and release workflow are implemented; release notes and branch protection checked) and 2026-11-13 (package 07 implementation: the content is not packed into the release; the release tag names the tree that `integrations install` installs; a release engine whose tag does not exist installs from `main`) and 2026-11-14 (package 07 review rework: the guard test `SkillKitVersionTests` ties the skills' kit version to the build version, so a tag build with stale skills fails its tests) and 2026-11-22 (the version lockstep of skills and engine replaced by the CLI contract: the guard `SkillContractTests`, FP19-AC3 reworded, the proposal "a format change is at least a minor release" dropped; [06-1](06-1-engine-version-check.md), [report](../../reports/versioning-cli-and-skills.md)) |
| Source | Concept §3.4; `CLAUDE.md` (Engine deliverables, CI), [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 07, 08 (the content comes from the tagged tree of this repository); technically every package that is part of a release |
| Enables | Teams outside the kit repository can install and use SHADE kit |

Labels follow the concept: **Decided**, **Proposed**, **Open**, **Unverified**, **Implemented**.

## Benefit

A developer gets SHADE kit on any supported machine in one step, with or without a .NET installation, and every release delivers engine and agent content with the same version number, so the version handshake works out of the box. Since 2026-11-22 both come from the same tag, and the handshake checks the CLI contract, which the tag build guarantees to fit (06-1, 06-2).

## Implemented

The build and release pipeline exists. `CLAUDE.md` (sections ".NET conventions" and "Workflow") is the description of record; this package does not repeat it.

- One version for every project, the tool package, and the binaries, derived by GitVersion from the git history. **Implemented** (`GitVersion.yml`).
- The `dotnet` global tool package (command `shade-kit`) and self-contained single-file binaries for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`. **Implemented** (Cake build in `build/`).
- Pull requests to `main` run pack (build, test, coverage) and the smoke test (`shade-kit version --json` on the native binary) on Ubuntu, Windows, and macOS. **Implemented** (`.github/workflows/pull-request.yml`).
- A tag `v<SemVer>` on `main` builds, tests, and smoke-tests on all three platforms, pushes the tool package to nuget.org, and creates a GitHub release with the five binary archives and the tool package. **Implemented** (`.github/workflows/release.yml`).

## Scope

### In scope

- Keeping engine and integration content on one version. Until 2026-11-13 this meant packing the content (skills, research agent definitions) into the engine release; since package 07 the content is not packed: the release tag `v<version>` names the tree that `integrations install` installs. **Decided** on 2026-11-13 (package 07 implementation). What remains is the skills' hand-raised `metadata.version` (06-2), which a guard test checks against the build version since 2026-11-14. Replaced on 2026-11-22 (06-2): the skills carry no version; their handshake names the CLI contract, which `SkillContractTests` checks against `CliContract.Current` without GitVersion, so a release needs no skill change unless the contract changes.
- The version rule for format changes. Dropped on 2026-11-22: the only proposed rule (a format change is at least a minor release, see Version) was dropped with the major.minor handshake (06-1).
- An installer script for the binaries.
- Release notes that list format changes and the need for `upgrade`.
- Making a failing pull request build block the merge into `main`.

### Out of scope

- Everything listed under "Implemented".
- Publishing the plugin to an agent's public marketplace. In v1 the content reaches the agent through `integrations install` (packages 07 and 08). **Proposed.** Since 2026-11-13 this repository is a Claude Code plugin marketplace itself (`creativecoders-shade-kit`, `.claude-plugin/`), which `integrations install` adds for the user (package 07).
- Automatic self-update of the engine.

## Functional description

### Version

- Engine and integration content are released together under one version number. **Decided** (concept §9.6). Replaced on 2026-11-22 (concept §9.6): they are released together from one tag `v<version>`; their compatibility is checked by the CLI contract, not by the version number. The tag `v<version>` that builds the engine is also the ref from which `integrations install` installs the content, so both come from one tree. **Decided** on 2026-11-13 (package 07 implementation), replacing "the content is packed into the engine". A prerelease engine installs from `main`, and so does a release engine whose tag does not exist (a build of an untagged `main` commit, which GitVersion gives no prerelease label), with the warning `integration-release-tag-missing` (07).
- The version follows semantic versioning; a format version change (package 01) is at least a minor release, so the handshake rule (major and minor equal, package 06) catches it. **Proposed.** Dropped on 2026-11-22: the reason existed only for the major.minor handshake, which the CLI contract replaced (06-1); a format change is caught by the format check of every command (package 01) and handled by `upgrade` (18). The version still follows semantic versioning.

### Additional release artifacts

| Artifact | For whom |
|---|---|
| Installer script (shell for macOS and Linux, PowerShell for Windows) | Downloads the binary for the platform, puts it on the `PATH`, and prints the next step (`shade-kit integrations install`). **Proposed** in the concept. |
| Release notes | Every format version change and its `upgrade` need are listed. **Proposed.** Today the `CreateGitHubRelease` task of `CreativeCoders.CakeBuild` 6.10.0 uses `ReleaseBody` from the build context, and `build/BuildContext.cs` sets it to the fixed text `SHADE kit Release` (checked 2026-10-18). The release notes therefore have to be generated first, for example from the Conventional Commits or a changelog, and passed as `ReleaseBody`. |

## Acceptance criteria

- **FP19-AC1** A pull request runs build and test, and a failure blocks the merge. The workflow is **Implemented**. The merge block is not met (checked 2026-10-18 with the GitHub API): `main` has no classic branch protection; the repository ruleset 25112099 requires a pull request (squash only) and forbids deletion and force pushes, and it has a `required_status_checks` rule whose list of checks is empty. Needed step: add the checks of `.github/workflows/pull-request.yml` (`ubuntu-latest`, `windows-latest`, `macos-latest`) as required checks to the ruleset. The ruleset lives in the GitHub settings, not in the repository.
- **FP19-AC2** A version tag produces the tool package and five binaries with that version. **Implemented.**
- **FP19-AC3** `shade-kit version --json` of every artifact reports the same engine version and the same CLI contract (`data.contract`), and `integrations install` from it installs the content of the tag of that version, whose skills name a contract in the window of that engine (FP07-AC2). Reworded on 2026-11-22; the earlier text: "… reports the same engine version, and `integrations install` from it installs content with that version" (replaced, because the content no longer carries a version).
- **FP19-AC4** The installer script installs the binary on macOS, Linux, and Windows and prints the next step.
- **FP19-AC5** On a machine without .NET, the binary runs `init` and `integrations install`.
- **FP19-AC6** The release notes of a release that raises a format version name the file type, the old and new version, and that `shade-kit upgrade` is needed. Not met today: the release body is the fixed text `SHADE kit Release`.

## Open points

- Replaced on 2026-11-22 (06-2, CLI contract): the skills carry no version, so a tag needs no skill change; `SkillContractTests` fails the tag build only when a handshake names another contract than `CliContract.Current` of that tree, and nothing checks this before the tag is set. A release tag also needs `.claude-plugin/` in its tree (07). The earlier text: every skill's `metadata.version` must carry the major and minor of the tag before it is set (06-2, FP07-AC2). Since 2026-11-14 the guard test `SkillKitVersionTests` (Core tests) fails when the skills' major.minor differs from the engine version of the build, and the release workflow runs the tests on the tag (`-t pack` on all three platforms before `nuget-push` and the GitHub release; **FACT**, `.github/workflows/release.yml`, with pack running the tests as `CLAUDE.md` describes). So a tag build with stale skills fails its tests and publishes nothing; the builds of `main` and of pull requests fail the same way before the tag. Remaining gap: nothing checks it before the tag is set (the `verify-tag` job does not), so a bad tag exists already when its build fails and has to be removed by hand; a release check in `verify-tag` or a Cake task was proposed by the review of 07 (2026-11-13, Major, layer 2) and not selected. The value is still raised by hand, and the commit that raises the minor of the skills needs the minor of the build version raised too (06-2). A release tag also needs `.claude-plugin/` in its tree; `v0.7.0` and `v0.8.0` have none (07).
