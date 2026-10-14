# 19 · Release and distribution

| | |
|---|---|
| Status | Draft, 2026-10-14 |
| Source | Concept §3.4; `CLAUDE.md` (Engine deliverables, CI); [ADR 0001](../adr/0001-shadekit-version-drift.md) decision 6 |
| Depends on | 07, 08 (content packed into the release); technically every package that is part of a release |
| Enables | Teams outside the kit repository can install and use SHADE kit |

## Benefit

A developer gets SHADE kit on any supported machine in one step, with or without a .NET installation, and every release delivers engine and agent content with the same version number, so the version handshake works out of the box.

## Scope

### In scope

- One version number per release for engine and integration content.
- The `dotnet` global tool package (command `shade-kit`).
- Self-contained single-file binaries for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`.
- An installer script for the binaries.
- CI: build and test on every pull request, and a release workflow that produces all artifacts.

### Out of scope

- Publishing the plugin to an agent's public marketplace. In v1 the content reaches the agent through `integrations install` (packages 07 and 08). **Proposed.**
- Automatic self-update of the engine.

## Functional description

### Version

- Engine and integration content are released together under one version number. **Decided** (ADR 0001). Because the content is packed into the engine (package 07), one build produces both.
- The version follows semantic versioning; a format version change (package 01) is at least a minor release, so the handshake rule (major and minor equal, package 06) catches it. **Proposed.**

### Artifacts per release

| Artifact | For whom |
|---|---|
| `dotnet` tool package | Developers with the .NET SDK: `dotnet tool install -g`. **Decided.** |
| Single-file binaries, five platforms | Developers without .NET. **Decided.** |
| Installer script (shell for macOS and Linux, PowerShell for Windows) | Downloads the binary for the platform, puts it on the `PATH`, and prints the next step (`shade-kit integrations install`). **Proposed** in the concept. |
| Release notes | Every format version change and its `upgrade` need are listed. **Proposed.** |

### CI

- Every pull request runs build and test. **Decided** (`CLAUDE.md`, planned). This part does not depend on any feature and can be set up at any time, ideally before package 01.
- A release workflow, triggered by a version tag, builds and tests once, then produces the tool package, the five binaries, and the release notes, and attaches them to a GitHub release. **Decided** (`CLAUDE.md`).
- The release workflow runs a smoke test on each binary it can run on a hosted runner (`shade-kit version --json`). **Proposed.**

## Acceptance criteria

- **FP19-AC1** A pull request runs build and test, and a failure blocks the merge.
- **FP19-AC2** A version tag produces the tool package and five binaries with that version.
- **FP19-AC3** `shade-kit version --json` of every artifact reports the same engine version, and `integrations install` from it installs content with that version.
- **FP19-AC4** The installer script installs the binary on macOS, Linux, and Windows and prints the next step.
- **FP19-AC5** On a machine without .NET, the binary runs `init` and `integrations install`.

## Open points

- None.
