# Smart Harness for AI Driven Engineering Kit

SHADE kit runs AI coding agents through a gated feature workflow: from a raw requirement to a clarified spec, a plan, the implementation, and a verified result with a report. It is built for small teams of 2–5 developers who work on features in parallel, each on its own branch, with Claude Code, GitHub Copilot, or OpenAI Codex CLI.

> **Status:** in development. Most commands described here are planned; today the CLI provides `shade-kit version`, `shade-kit init`, and `shade-kit feature list`.

## How it works

SHADE kit has two parts. The skills are the prompts the agent follows; the `shade-kit` CLI is the memory and the referee.

- **Skills** (`skills/`): one skill per phase plus shared skills for interviews, gate checks, research, and reviews. They are installed once per machine for each agent (`shade-kit integrations install`), never into a repository.
- **Engine** (`shade-kit`): a .NET CLI that keeps the feature state in `.shadekit/` inside the repository, committed and merged with the code. Agents call it with `--json`.
- **Phases:** `new` → `refine` → `map` → `plan` → `implement` → `verify` → `finish` in the `feature` profile. The `bugfix` profile replaces `refine` and `map` with `diagnose`; the `quick` profile runs `new` → `quick` → `finish` for small changes.
- **Gates:** the human approves every phase transition. The Definition of Ready guards `plan`, the Definition of Done guards `finish`; each gate can be switched off per repository.
- **Traceability:** requirements get stable IDs and are mapped to the tests that prove them; `finish` writes a report with requirements, tests, check results, and approvals.

SHADE kit never commits, pushes, or opens pull requests; that stays with the developer.

Design notes: [concept](research/shade-kit-concept.md), [skills](research/shade-kit-skills.md), and the [v1 feature packages](docs/feature-packages/README.md).

## Build

The build uses [Cake](https://cakebuild.net) with `CreativeCoders.CakeBuild` (project in `build/`). It needs the .NET 10 SDK and the full git history, because [GitVersion](https://gitversion.net) derives the version from it.

```sh
./build.sh -t pack                              # build, test, coverage report and the dotnet tool package
./build.sh -t smoketest -t createdistpackages   # self-contained single-file binaries for the host OS, smoke test, archives
```

On Windows use `build.cmd` or `build.ps1`. Results land in `.artifacts/` and `.tests/`.

A release is a tag `v<SemVer>` on `main`; the `release` workflow publishes the tool package to nuget.org and creates a GitHub release with binaries for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64` and `win-arm64`.
