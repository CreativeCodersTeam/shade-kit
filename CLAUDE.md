# CLAUDE.md

## Project

**SHADE kit** (Smart Harness for AI Driven Engineering) is a toolkit that runs AI agents through the software lifecycle in a guided, gated workflow.

Status: greenfield. Items marked *(planned)* are decided but not built yet. When one gets built, remove the mark and add the real commands and paths.

Repository language is always English. The kit is designed to be used by English-speaking developers, and all prompts, instructions, and documentation are in English.

It has two deliverables:

1. **Plugin** (`skills/`): skills, agents, hooks, and commands in the Claude Code plugin layout.
2. **Engine** (`src/`): a .NET 10 CLI, shipped two ways:
   - as a `dotnet` global tool
   - as self-contained single-file binaries (no .NET install needed) for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`

### v1 scope

- **Gated workflow phases:** spec → plan → implement → verify → review, with an explicit approval gate between phases.
- **Quality gates:** build, test, lint, and coverage checks must pass before a phase can complete.
- **State & traceability:** workflow state is saved, requirements are mapped to the tests that cover them, and reports are generated.
- **Install/sync:** the engine installs and updates the kit's skills in a target repository, for each supported agent.

## Target agents

The target agents are Claude Code (primary), GitHub Copilot, and OpenAI Codex CLI.

If a design choice can stay agent-agnostic **without losing functionality or precision**, say so and propose the agnostic option. If it cannot, name what would be lost. The plugin layout itself stays Claude-first.

## Repository layout

| Path | Purpose |
|---|---|
| `skills/` | Shipped plugin content |
| `src/` | Production .NET projects *(planned)* |
| `tests/` | One test project per production project *(planned)* |
| `research/` | Design notes and investigations. Not shipped. Shipped code and skills must not depend on it. |
| `docs/specs/`, `docs/plans/` | Only created when the spec-driven workflow is used (see below) |

## .NET conventions *(planned)*

- Target `net10.0`.
- Use one `.slnx` solution at the repository root.
- Use Central Package Management: all NuGet versions go in `Directory.Packages.props`. Never put `Version=` on a `PackageReference`.
- Put shared build settings (nullable, implicit usings, analyzers) in `Directory.Build.props`, not in individual `.csproj` files.
- Tests use xUnit, FakeItEasy, and AwesomeAssertions.

```sh
dotnet build                     # from repo root (.slnx)
dotnet test
dotnet publish src/<Cli> -c Release -r <rid> --self-contained -p:PublishSingleFile=true
```

## Workflow

- **Ad hoc by default.** Use the spec → plan → implement flow (`docs/specs/`, `docs/plans/`) only when the user asks for it, for example by invoking a dedicated skill. Don't push it unprompted.
- **Branches:** `feature/<name>`. Changes reach `main` only through a pull request.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `ci:`).
- **CI:** GitHub Actions *(planned)*. Pull requests run build and test. A release workflow builds the tool package and the self-contained binaries for every target platform.
