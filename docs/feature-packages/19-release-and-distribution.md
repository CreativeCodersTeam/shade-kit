# 19 · Release and distribution

| | |
|---|---|
| Status | Draft, 2026-10-14, updated 2026-10-18 (reduced to the remaining scope; CI and release workflow are implemented; release notes and branch protection checked) |
| Source | Concept §3.4; `CLAUDE.md` (Engine deliverables, CI), [concept §9.6](../../research/shade-kit-concept.md#9-version-drift) |
| Depends on | 07, 08 (content packed into the release); technically every package that is part of a release |
| Enables | Teams outside the kit repository can install and use SHADE kit |

Labels follow the concept: **Decided**, **Proposed**, **Open**, **Unverified**, **Implemented**.

## Benefit

A developer gets SHADE kit on any supported machine in one step, with or without a .NET installation, and every release delivers engine and agent content with the same version number, so the version handshake works out of the box.

## Implemented

The build and release pipeline exists. `CLAUDE.md` (sections ".NET conventions" and "Workflow") is the description of record; this package does not repeat it.

- One version for every project, the tool package, and the binaries, derived by GitVersion from the git history. **Implemented** (`GitVersion.yml`).
- The `dotnet` global tool package (command `shade-kit`) and self-contained single-file binaries for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`. **Implemented** (Cake build in `build/`).
- Pull requests to `main` run pack (build, test, coverage) and the smoke test (`shade-kit version --json` on the native binary) on Ubuntu, Windows, and macOS. **Implemented** (`.github/workflows/pull-request.yml`).
- A tag `v<SemVer>` on `main` builds, tests, and smoke-tests on all three platforms, pushes the tool package to nuget.org, and creates a GitHub release with the five binary archives and the tool package. **Implemented** (`.github/workflows/release.yml`).

## Scope

### In scope

- Packing the integration content (skills, research agent definitions) into the engine release, so engine and content share one version.
- The version rule for format changes.
- An installer script for the binaries.
- Release notes that list format changes and the need for `upgrade`.
- Making a failing pull request build block the merge into `main`.

### Out of scope

- Everything listed under "Implemented".
- Publishing the plugin to an agent's public marketplace. In v1 the content reaches the agent through `integrations install` (packages 07 and 08). **Proposed.**
- Automatic self-update of the engine.

## Functional description

### Version

- Engine and integration content are released together under one version number. **Decided** (concept §9.6). Because the content is packed into the engine (package 07), one build produces both.
- The version follows semantic versioning; a format version change (package 01) is at least a minor release, so the handshake rule (major and minor equal, package 06) catches it. **Proposed.**

### Additional release artifacts

| Artifact | For whom |
|---|---|
| Installer script (shell for macOS and Linux, PowerShell for Windows) | Downloads the binary for the platform, puts it on the `PATH`, and prints the next step (`shade-kit integrations install`). **Proposed** in the concept. |
| Release notes | Every format version change and its `upgrade` need are listed. **Proposed.** Today the `CreateGitHubRelease` task of `CreativeCoders.CakeBuild` 6.10.0 uses `ReleaseBody` from the build context, and `build/BuildContext.cs` sets it to the fixed text `SHADE kit Release` (checked 2026-10-18). The release notes therefore have to be generated first, for example from the Conventional Commits or a changelog, and passed as `ReleaseBody`. |

## Acceptance criteria

- **FP19-AC1** A pull request runs build and test, and a failure blocks the merge. The workflow is **Implemented**. The merge block is not met (checked 2026-10-18 with the GitHub API): `main` has no classic branch protection; the repository ruleset 25112099 requires a pull request (squash only) and forbids deletion and force pushes, and it has a `required_status_checks` rule whose list of checks is empty. Needed step: add the checks of `.github/workflows/pull-request.yml` (`ubuntu-latest`, `windows-latest`, `macos-latest`) as required checks to the ruleset. The ruleset lives in the GitHub settings, not in the repository.
- **FP19-AC2** A version tag produces the tool package and five binaries with that version. **Implemented.**
- **FP19-AC3** `shade-kit version --json` of every artifact reports the same engine version, and `integrations install` from it installs content with that version.
- **FP19-AC4** The installer script installs the binary on macOS, Linux, and Windows and prints the next step.
- **FP19-AC5** On a machine without .NET, the binary runs `init` and `integrations install`.
- **FP19-AC6** The release notes of a release that raises a format version name the file type, the old and new version, and that `shade-kit upgrade` is needed. Not met today: the release body is the fixed text `SHADE kit Release`.

## Open points

- None of its own.
