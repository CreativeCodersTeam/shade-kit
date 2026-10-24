# CLAUDE.md

## Project

**SHADE kit** (Smart Harness for AI Driven Engineering) is a toolkit that runs AI agents through the software lifecycle in a guided, gated workflow.

Status: greenfield. Items marked *(planned)* are decided but not built yet. When one gets built, remove the mark and add the real commands and paths.

Repository language is always English. The kit is designed to be used by English-speaking developers, and all prompts, instructions, and documentation are in English.

It has two deliverables:

1. **Plugin** (`skills/`): skills and research agent definitions in the Claude Code plugin layout. Hooks and commands come later.
2. **Engine** (`src/`): a .NET 10 CLI, shipped two ways:
   - as a `dotnet` global tool
   - as self-contained single-file binaries (no .NET install needed) for `osx-arm64`, `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`

### v1 scope

- **Gated workflow profiles:** `feature` (new → refine → map → plan → implement → verify → finish), `bugfix` (new → diagnose → plan → implement → verify → finish), and `quick` (new → quick → finish). The agent checks each gate when the next phase starts; the human approves the transition. Each gate's approval and checklist can be switched off in `gates.json`.
- **Quality gates:** the Definition of Ready (gate into `plan`) and Definition of Done (gate into `finish`) are checklists. In v1 the agent runs build, test, lint, and coverage with the commands approved in `plan.md` and records the results; the CLI enforcing and running them comes later.
- **State & traceability:** workflow state and approval history are saved in `.shadekit/`, requirements are mapped to the tests that prove them, and `finish` can write a report (`report` in `config.json`).
- **Install/sync:** the engine installs and updates the kit's skills at user level for each supported agent (`integrations install/update`) and initialises and upgrades a repository's `.shadekit/` (`init`, `upgrade`). Nothing is installed into a repository.

## Target agents

The target agents are Claude Code (primary), GitHub Copilot, and OpenAI Codex CLI.

If a design choice can stay agent-agnostic **without losing functionality or precision**, say so and propose the agnostic option. If it cannot, name what would be lost. The plugin layout itself stays Claude-first.

## Repository layout

| Path | Purpose |
|---|---|
| `skills/` | Shipped plugin content |
| `src/` | Production .NET projects: `CreativeCoders.ShadeKit.Cli` (the `shade-kit` CLI) and `CreativeCoders.ShadeKit.Core` (engine logic). The templates that `init` writes live in `src/CreativeCoders.ShadeKit.Core/Templates/Resources/` (`<name>.template.md`, checklists in `checks/`) and are embedded into the Core assembly; their repository paths are listed in `ShippedTemplates`. |
| `tests/` | One test project per production project (`<Project>.Tests`) |
| `build/` | Cake Frosting build project (`CreativeCoders.CakeBuild`), run through `build.sh` / `build.ps1` / `build.cmd`. Not part of `ShadeKit.slnx`; it has its own `Directory.Build.props` and `Directory.Packages.props`. |
| `.github/` | GitHub Actions workflows and Dependabot configuration |
| `research/` | Design notes and investigations. Not shipped. Shipped code and skills must not depend on it. |
| `docs/specs/`, `docs/plans/` | Only created when the spec-driven workflow is used (see below) |

## .NET conventions

- Target `net10.0`. The SDK is pinned in `global.json` (`sdk.version`, `rollForward: patch`); CI installs exactly that version with `actions/setup-dotnet`.
- Use one `.slnx` solution at the repository root (`ShadeKit.slnx`).
- Use Central Package Management: all NuGet versions go in `Directory.Packages.props`. Never put `Version=` on a `PackageReference`.
- Put shared build settings (nullable, implicit usings, analyzers) in `Directory.Build.props`, not in individual `.csproj` files. Analyzers run with `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, and `TreatWarningsAsErrors`, so every warning breaks the build. SonarAnalyzer.CSharp runs in every solution project as a `GlobalPackageReference` in `Directory.Packages.props`; a Sonar finding that is not fixed is suppressed with `[SuppressMessage(..., Justification = "...")]` on the class or method, not in `.editorconfig`. `tests/.editorconfig` turns off CA1707 because test method names use underscores (`Method_Scenario_ExpectedResult`).
- Files are UTF-8 without BOM with LF line endings (`.editorconfig`).
- Tests use xUnit v3, FakeItEasy, and AwesomeAssertions. They run on Microsoft.Testing.Platform (enabled in `global.json`) with `Microsoft.Testing.Extensions.TrxReport` and `Microsoft.Testing.Extensions.CodeCoverage` (`dotnet test --coverage`), which is what the Cake test task drives.
- Versioning: GitVersion (`GitVersion.yml`, `GitVersion.MsBuild` as `GlobalPackageReference`) derives one version from the git history for every project, the tool package, and the binaries. Never put a version in a `.csproj` or `.props` file. Builds need the full history (`fetch-depth: 0`). A release is a tag `v<SemVer>` on `main`.
- Only `CreativeCoders.ShadeKit.Cli` is packable (`IsPackable=false` in `Directory.Build.props`). A publish with a runtime identifier produces a self-contained single-file binary (set in the CLI `.csproj`).
- The CLI is built on `CreativeCoders.Cli.Hosting`. A command is a class with `[CliCommand([...])]` that implements `ICliCommand` or `ICliCommand<TOptions>`, placed under `Commands/<Group>/<Command>/`. A group is declared with `[assembly: CliCommandGroup([...], "...")]` in `Commands/<Group>/<Group>CommandGroup.cs`. Expected failures throw `CliCommandAbortException` with an exit code from `ShadeKitCliExitCodes`.
- Agent contract: every agent-facing command takes options derived from `JsonCommandOptions` (`--json`). In JSON mode it writes one `CliJsonEnvelope` (`ok`, `data`, `warnings`, `error`) through `ICliJsonWriter` to stdout and nothing else; on an expected failure it writes a failure envelope and throws `CliCommandAbortException` with `PrintMessage = false`. Commands get `ShadeKitCommandContext` (`Output/`) injected: `RunAsync` runs the core call (optionally with the kit store) with the run's cancellation token and turns mapped failures into the failure envelope / `CliCommandAbortException`, and `WriteSuccess` writes the single success envelope or the plain warnings and lines; commands do not use `ICliJsonWriter`, `CliErrorMapper`, or `CliCommandAbort` directly (only `version`, which cannot fail, writes its envelope through `ICliJsonWriter`). Core exceptions are mapped to exit code, error code, and message only in `CliErrorMapper`. Exit codes are grouped by area (10–19 store, 20–29 format, 30–39 features, 40–49 phases and gates, 255 host); 2 (invalid arguments, usage) and 130 (canceled) are outside the ranges; every exit code has a kebab-case error code in `ShadeKitCliErrorCodes`; codes that only appear as warnings are in `ShadeKitCliWarningCodes`.
- Versioned JSON files in `.shadekit/` are read and written only through `IKitStore.ReadJsonAsync`/`WriteJsonAsync`. Each file type has a format id in `FormatIds` and a definition in `FormatRegistry.CreateDefault()`; raising a format version means a new table entry plus an `IFormatMigration` step and a test that migrates a fixture from the previous version. Document models derive from `VersionedDocument`; nested classes need their own `[JsonExtensionData] Dictionary<string, JsonElement>` to keep unknown fields.
- Text file content is held as `TextFileContent` (lines, UTF-8 BOM flag, final-line-ending flag), never as `byte[]`, and is written only through `ITextFileWriter` (`IKitStore.WriteFileAsync` uses it for `.shadekit/`). The writer is the only place that encodes to bytes; it resolves the line ending with `ILineEndingResolver`: git (attributes as git's `convert_attrs`: resolved `text`, legacy `crlf` only while `text` is unspecified; `-text` → no decision, `eol` attribute, `text=input`/`crlf=input` → LF, `core.autocrlf`, `core.eol` for text files; config values cached per repository root for the resolver's lifetime, i.e. one command run) → `.editorconfig` `end_of_line` → operating system. Files are read as strict UTF-8; an unreadable `.editorconfig` falls back with an `EditorConfigUnreadableWarning`, one the parser cannot evaluate (e.g. an overflowing numeric-range glob) with an `EditorConfigInvalidWarning`.
- Core services are registered with `services.AddShadeKitCore()`.

```sh
dotnet build                     # from repo root (.slnx)
dotnet test
dotnet test --coverage           # with coverage (writes TestResults/)
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- init          # initialises the git repository of the current directory
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- feature list
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- feature new --source "Login with SSO" [--title <t>] [--profile <p>]   # source: text, URL, or file
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- feature current --json
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- feature set <id>
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- feature show <id>
dotnet run --project src/CreativeCoders.ShadeKit.Cli -- feature drop <id> [--yes]
dotnet pack src/CreativeCoders.ShadeKit.Cli          # dotnet global tool package (command: shade-kit)
dotnet publish src/CreativeCoders.ShadeKit.Cli -c Release -r <rid>   # self-contained single-file binary
```

Cake build (`./build.sh` on macOS/Linux, `./build.cmd` everywhere, `build.ps1` on Windows). Output goes to `.artifacts/` (`nuget/`, `published/<rid>/`, `dist/`) and `.tests/` (`coverage/`, `coverage-report/`):

```sh
./build.sh -t pack                          # clean, restore, build, test, coverage report, tool package
./build.sh -t smoketest                     # publish binaries for the host OS, run `shade-kit version --json` on the native one
./build.sh -t smoketest -t createdistpackages   # plus shade-kit-<rid>.tar.gz / .zip archives
./build.sh -t publish --runtime=linux-x64 --runtime=win-x64   # publish other runtimes explicitly
```

Without `--runtime`, the host OS decides the runtimes: Linux → `linux-x64`, `linux-arm64`; Windows → `win-x64`, `win-arm64`; macOS → `osx-arm64`.

## Workflow

- **Ad hoc by default.** Use the spec → plan → implement flow (`docs/specs/`, `docs/plans/`) only when the user asks for it, for example by invoking a dedicated skill. Don't push it unprompted.
- **Branches:** `feature/<name>`. Changes reach `main` only through a pull request.
- **Commits:** Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`, `ci:`).
- **CI:** GitHub Actions (`.github/workflows/`), each build job runs the Cake build on Ubuntu, Windows, and macOS:
  - `pull-request`: pack (build, test, coverage) and smoke test on every pull request to `main`.
  - `integration` (`feature/**`) and `main`: additionally the dist archives; Ubuntu pushes the tool package to GitHub Packages.
  - `release` (tag `v<SemVer>`): a `verify-tag` job rejects tags that are not `v<SemVer>` or not on `main`. After all platforms succeed, a `nuget-push` job pushes the tool package built on Ubuntu to nuget.org (secret `NUGET_ORG_TOKEN`, only in that job), then a final job creates the GitHub release with the five binary archives and the tool package.
  - `dependabot-auto-merge` / `dependabot-manual` with `.github/dependabot.yml` (NuGet for `/` and `/build`, GitHub Actions).
