# Review: branch `feature/copilot-codex-integration` against `main`

| | |
|---|---|
| Date | 2026-11-18 |
| Branch / head | `feature/copilot-codex-integration`, commit `2d4c338` ("feat: add support for GitHub Copilot and OpenAI Codex CLI integrations with new exception handling") |
| Base | merge-base `e8f06f7` (#34). `main` is at `332f994` since then (Dependabot #35 only, no overlap). |
| Diff | `git diff main...HEAD`: 114 files, +15 094 / −301 |
| Spec | `docs/feature-packages/08-copilot-and-codex-integration.md` (FP08-AC1 … AC6); context `07-claude-code-integration.md`, `docs/feature-packages/README.md` |
| Standards | `CLAUDE.md` (project), `~/.claude/CLAUDE.md` (user), `docs/feature-packages/06-2-skill-conventions-and-version.md`, `.editorconfig`, `tests/.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, plus the Fowler smell baseline (*Refactoring*, ch. 3) |
| Method | Two independent reviews (Standards axis, Spec axis), then each reported claim was re-checked against the code at `2d4c338`. |
| Test run | `dotnet test` on the branch: 6297 tests, 6289 passed, 8 skipped (Windows-only on macOS), 0 failed (reported by the spec reviewer; matches spec 08, "Acceptance criteria"). |

## How to read this report

- **ID:** `STD-nn` for the Standards axis, `SPEC-nn` for the Spec axis. The two axes stay separate on purpose and are not ranked against each other.
- **Kind:**
  - *Hard violation*: breaks a documented repository rule.
  - *Judgement call*: a baseline code smell, never a hard rule; a documented repository rule overrides it.
  - *Spec gap*: missing, partial, or reinterpreted requirement.
  - *Spec deviation*: implemented differently from the spec text.
  - *Risk*: implemented as specified, but with a real failure mode.
- **Severity:** High (users can lose a working setup, or an acceptance criterion is not met), Medium (wrong or misleading behaviour, maintainability cost that will grow), Low (cosmetic, local, or cheap to fix).
- **Verified:**
  - **FACT**: re-checked in the code or docs at `2d4c338`, with the path and line.
  - **Unverified**: could not be checked here, for example because it needs a real `copilot` or `codex` run.
- Line numbers refer to commit `2d4c338`.

## Summary

| ID | Axis | Kind | Severity | Title | Location |
|---|---|---|---|---|---|
| STD-01 | Standards | Hard violation | Medium | `CLAUDE.md` lists two skill guards, the branch adds a third | `CLAUDE.md:114` |
| STD-02 | Standards | Judgement call | Low | Duplicated JSON reading helpers in three output readers | `*/…JsonOutputReader.cs` |
| STD-03 | Standards | Judgement call | Low | `EnsurePositionalArgument` copied into three plugin CLIs | `*/…PluginCli.cs` |
| STD-04 | Standards | Judgement call | Low | `InstallAsync` / `UpdateAsync` / `IsAvailable` / `EnsureAvailable` identical in three integrations | `*/…Integration.cs` |
| STD-05 | Standards | Judgement call | Low | Codex uses only `Add` from the shared resolver (Refused Bequest) | `Codex/CodexCliIntegration.cs:204-218` |
| STD-06 | Standards | Judgement call | Low | `.cmd` shim opt-in is implied by a factory being set; `RunsCmdShims` is used only by tests | `Processes/ExternalTool.cs:40,49` |
| STD-07 | Standards | Judgement call | Medium | Agent-id switches spread over four CLI places (Shotgun Surgery) | `IntegrationsTextFormat.cs`, `AgentOptionConverter.cs`, `CliErrorMapper.cs` |
| STD-08 | Standards | Judgement call | Medium | The same agent-specific text is repeated in many skills and has already drifted | `skills/*/SKILL.md`, `ManagedInstructionSection.cs:39` |
| STD-09 | Standards | Judgement call | Low | `…CommandFailedException` derives from `ExternalToolUnavailableException` | `Integrations/Exceptions/*CommandFailedException.cs` |
| SPEC-01 | Spec | Spec gap | High | FP08-AC2 / AC3: the session part (load `shade-kit-next-phase`, run the version handshake) is not proven | spec 08 lines 181-182 |
| SPEC-02 | Spec | Spec gap | Medium | FP08-AC4 is met by a reinterpretation; the AC text still says "produced at install time" | spec 08 lines 49, 183 |
| SPEC-03 | Spec | Spec gap | Medium | FP08-AC5 rests on one scenario run that left no artifact | spec 08 line 184 |
| SPEC-04 | Spec | Spec deviation | Low | `shade-kit-interview` changes more than G3 asks for | `skills/shade-kit-interview/SKILL.md:78,84` |
| SPEC-05 | Spec | Risk | High | Codex `update` / `install` removes the integration on a network or login failure | `Codex/CodexCliIntegration.cs:272` |
| SPEC-06 | Spec | Spec deviation | Low | The rule that drops the version arrow also changes Claude Code lines | `IntegrationsTextFormat.cs:127-131` |
| SPEC-07 | Spec | Risk | Low | Copilot's cancellation filter keys on `change == Switch`, Codex uses an explicit flag | `Copilot/CopilotCliIntegration.cs:255` |
| SPEC-08 | Spec | Spec deviation | Low | Garbled sentence in package 02 | `docs/feature-packages/02-repository-initialisation.md:212` |

---

## Standards axis

### STD-01 · `CLAUDE.md` lists two skill guards, the branch adds a third

- **Kind / Severity:** Hard violation / Medium
- **Rule:** `CLAUDE.md` describes the current code, and every built item loses its *(planned)* mark and gets its real paths (CLAUDE.md, "Project"). Agents and humans rely on this file as the map of the repository.
- **Location:** `CLAUDE.md:114`
- **Verified:** **FACT**. `tests/CreativeCoders.ShadeKit.Core.Tests/Skills/` contains `ShippedSkillsTests.cs`, `SkillKitVersionTests.cs`, `SingleSkillSourceTests.cs` and `SingleSkillSourceGuard.cs`. The last two were added by this branch (FP08-AC4).

**Excerpt (`CLAUDE.md:114`):**

```markdown
- Skill texts have no automated tests (only the guards `SkillKitVersionTests` and `ShippedSkillsTests`, which check
  the version and the references into `docs/`/`research/` in every text file under `skills/`). …
```

**Impact:** an agent that follows `CLAUDE.md` does not know about the single-skill-source guard. It may add an agent-specific skill copy or manifest, for example `.codex-plugin/plugin.json` or `.github/skills/…`, and only learn from a red build that this is forbidden. The rule "one skill source for all agents" (spec 08, line 49) is not documented in the place where agents look for conventions.

**Proposed solution (recommended):** extend the sentence, and also name the rule in "Skill conventions":

```markdown
- Skill texts have no automated tests (only the guards `SkillKitVersionTests`, `ShippedSkillsTests`, and
  `SingleSkillSourceTests` (FP08-AC4), which check the version, the references into `docs/`/`research/` in every
  text file under `skills/`, and that every skill has one source for all agents: no duplicate skill name, no
  `SKILL.md` in an agent skill location, no agent-specific manifest beside `.claude-plugin/`).
```

**Done when:** `CLAUDE.md` names all three guards, and `grep -n "SingleSkillSourceTests" CLAUDE.md` finds a match.

---

### STD-02 · Duplicated JSON reading helpers in three output readers

- **Kind / Severity:** Judgement call (Duplicated Code) / Low
- **Location:**
  - `Integrations/Claude/ClaudeJsonOutputReader.cs:143,162,184`
  - `Integrations/Copilot/CopilotJsonOutputReader.cs:115,134,186,197,211,218`
  - `Integrations/Codex/CodexJsonOutputReader.cs:132,154,179,220,233,240`
- **Verified:** **FACT**. The bodies are the same; only the tool name in the messages and the exception type differ. The Codex version also lets `subject` be `null` (`Describe(subject)`).

**Excerpt (Copilot, `CopilotJsonOutputReader.cs:134-144`):**

```csharp
public string? ReadOptionalString(JsonElement element, string propertyName, string subject)
{
    if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : throw Fail($"The field '{propertyName}' of the {subject} of the copilot output is not a string.");
}
```

**Excerpt (Codex, `CodexJsonOutputReader.cs:154-164`):**

```csharp
public string? ReadOptionalString(JsonElement element, string propertyName, string? subject)
{
    if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
    {
        return null;
    }

    return value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : throw Fail($"The field '{propertyName}' of the {Describe(subject)} is not a string.");
}
```

The same holds for `ReadRequiredString`, `ReadRequiredBoolean`, `FindLastNonEmptyLine` and the exit-code reason in `CreateExitCodeException` (Copilot `:197-207`, Codex `:220-230`).

**Impact:** a fix to how fields are validated, or to how the last error line is chosen, must be made three times. The three copies have already started to drift: the `subject` is nullable in Codex only, and Claude's `CreateExitCodeException` also reads a JSON result object.

**Proposed solutions:**
1. *(recommended)* Extract an internal `AgentJsonFieldReader` (or `AgentJsonOutputReaderBase<TException>`) with:
   - the tool name;
   - a failure factory `Func<string, Exception?, TException>`;
   - a subject formatter.

   It holds `ReadRequiredString`, `ReadOptionalString`, `ReadRequiredBoolean` and `FindLastNonEmptyLine`. Each agent reader keeps only its own output shapes: Claude's result object, Copilot's `ReadOptionalObjectOrString`, Codex's `ReadOptionalObject`. Do this as a refactoring, without changing behaviour. The existing reader tests (`CopilotPluginCliTests`, `CodexPluginCliTests`, Claude tests) are the safety net; the message texts must stay byte-identical, because the tests assert them.
2. Extract only the static `FindLastNonEmptyLine` and the exit-code reason into one internal static helper. Smaller change, about a third of the duplication.
3. Accept the duplication and record it as a known decision (one reader per agent, because the output shapes may diverge). Cheapest option, no code change.

**Done when:** each helper exists once, and all reader tests pass unchanged.

---

### STD-03 · `EnsurePositionalArgument` copied into three plugin CLIs

- **Kind / Severity:** Judgement call (Duplicated Code) / Low
- **Location:** `Integrations/Claude/ClaudePluginCli.cs:199-211`, `Integrations/Copilot/CopilotPluginCli.cs:204-216`, `Integrations/Codex/CodexPluginCli.cs:224-236`
- **Verified:** **FACT**. The three methods are identical apart from the tool name. Each carries the same S3236 `[SuppressMessage]`.

**Excerpt (`CodexPluginCli.cs:224-236`):**

```csharp
[SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly",
    Justification = "The caller's parameter name is forwarded on purpose, so an invalid value names the " +
                    "caller's parameter instead of this helper's.")]
private static void EnsurePositionalArgument(string value, string paramName)
{
    Ensure.IsNotNullOrWhitespace(value, paramName);

    if (value.StartsWith('-'))
    {
        throw new ArgumentException(
            $"The value '{value}' starts with '-', so codex would read it as an option.", paramName);
    }
}
```

**Impact:** this is a security-relevant guard: it stops a value that starts with `-` from being read as an option. If it is tightened in one copy, for example to also refuse a value with a line break, the other two copies stay weaker. The Sonar suppression is also repeated three times.

**Proposed solutions:**
1. *(recommended)* Move it to `Processes/` as `internal static class PositionalArgument { public static void Ensure(string value, string toolName, string paramName) }`, with the suppression in one place, and call it from all three CLIs. Keep the message text, because the tests assert it.
2. Accept it as local per-tool code. No change.

**Done when:** the guard exists once and the `…PluginCliTests` pass.

---

### STD-04 · `InstallAsync` / `UpdateAsync` / `IsAvailable` / `EnsureAvailable` identical in three integrations

- **Kind / Severity:** Judgement call (Duplicated Code) / Low
- **Location:**
  - `Claude/ClaudeCodeIntegration.cs:118-135,158-186,229`
  - `Copilot/CopilotCliIntegration.cs:127-144,167-195,251`
  - `Codex/CodexCliIntegration.cs:137-153,170-198,250`
- **Verified:** **FACT**. The bodies are the same; only the field names and the "find plugin" call differ.

**Excerpt (Codex `:180-196`; Copilot `:177-193` and Claude `:168-184` are identical apart from the field and method names):**

```csharp
public async Task<IntegrationResult?> UpdateAsync(Func<CancellationToken, Task<MarketplaceSource>> getSource,
    CancellationToken cancellationToken = default)
{
    Ensure.NotNull(getSource);

    var pluginBefore = await FindPluginAsync(cancellationToken).ConfigureAwait(false);

    if (pluginBefore is null)
    {
        return null;
    }

    var source = Ensure.NotNull(await getSource(cancellationToken).ConfigureAwait(false));

    return await ApplyAsync(source, pluginBefore, cancellationToken).ConfigureAwait(false);
}
```

Each `ApplyAsync` also ends with the same pair of catch filters:

```csharp
catch (ExternalToolUnavailableException exception) when (isRemoved)
{
    throw new IntegrationIncompleteException(AgentId, exception);
}
catch (OperationCanceledException exception) when (/* switching */ && cancellationToken.IsCancellationRequested)
{
    throw new IntegrationSwitchCanceledException(AgentId, exception);
}
```

**Impact:** the contract of `IAgentIntegration.UpdateAsync` has three rules: an agent without the plugin does not resolve a source, `getSource` runs lazily, and a `null` source is refused. These rules live in three copies. A fourth agent would copy them a fourth time.

**Proposed solutions:**
1. Add an abstract base `AgentIntegrationBase<TPlugin>` with `InstallAsync`, `UpdateAsync`, `IsAvailable` and `EnsureAvailable`, and abstract members `FindPluginAsync`, `ApplyAsync`, `FindExecutable` and `CreateNotFoundException`. The risk is coupling through inheritance. Keep the base thin, because the `ApplyAsync` bodies really differ (see STD-05).
2. *(recommended for now)* Extract only the error translation into one shared helper, for example `IntegrationStep.RunAsync(agentId, isRemoved, isSwitching, body)` or a static `TranslateFailure`. It holds the incomplete/canceled mapping, which is the part with real semantics. Leave the five-line `InstallAsync`/`UpdateAsync` bodies as they are.
3. Accept it. No change.

**Done when:** for option 2, the catch logic exists once and `ClaudeCodeIntegrationTests`, `CopilotCliIntegrationTests` and `CodexCliIntegrationTests` pass unchanged.

---

### STD-05 · Codex uses only `Add` from the shared resolver (Refused Bequest)

- **Kind / Severity:** Judgement call (Refused Bequest) / Low
- **Location:** `Codex/CodexCliIntegration.cs:204-245`; resolver in `Integrations/MarketplaceChangeResolver.cs`
- **Verified:** **FACT**.
  - Codex reports no ref (`CodexCliIntegration.cs:166`: `new InstalledIntegration(AgentId, ShadeKitPlugin.Id, null, null, …)`).
  - For a GitHub marketplace with the same repository, `Resolve` therefore compares `marketplace.Ref == null` with `source.Ref`. For every source with a ref it returns `Switch`.
  - Codex reads only `change == MarketplaceChange.Add`, and otherwise branches on `SourceKind` (line 218).
  - `Conflict` (thrown by `Resolve`) and the directory `Keep` still work as intended.

**Excerpt (`CodexCliIntegration.cs:204-221`):**

```csharp
var change = _marketplaceChangeResolver.Resolve(marketplace, source);
…
if (change == MarketplaceChange.Add)
{
    await _codexPluginCli.AddMarketplaceAsync(source, cancellationToken).ConfigureAwait(false);
}
else if (marketplace!.SourceKind == AgentMarketplaceSourceKind.GitHub)
{
    // codex does not report the ref, so an add with the source tells whether the marketplace has it.
    if (await TryKeepGitMarketplaceAsync(source, cancellationToken).ConfigureAwait(false) is { } isKept)
```

**Impact:**
- A reader of `ApplyAsync` sees `change` with the value `Switch` and has to know that it is meaningless for Codex.
- If the resolver's `Switch` rule changes, for example to also compare the kit version, Codex is silently unaffected, but its tests would not show that.
- The null-forgiving `marketplace!` relies on the resolver returning `Add` for `null`, which is an implicit contract.

**Proposed solutions:**
1. *(recommended)* Make the "ref unknown" case explicit:
   - Option a: `AgentMarketplace` gets a `RefKnown` flag, or Codex passes a marker, and the resolver returns a new `MarketplaceChange.Verify` ("same repository, ref not reported") for it.
   - Option b: add a resolver method `ResolveWithoutRef(...)` that returns `Add`, `KeepDirectory`, `VerifyGitHub` or throws `Conflict`.

   Codex then switches on that value instead of on `SourceKind`. Update `MarketplaceChangeResolverTests` with the new case.
2. Keep the behaviour, but add a comment at `:204` that for Codex only `Add` and `Conflict` matter. Replace `marketplace!` with a guard: `marketplace ?? throw new InvalidOperationException(...)`.

**Done when:** `ApplyAsync` in Codex has no value of `change` that it ignores, or the ignored values are documented and guarded.

---

### STD-06 · `.cmd` shim opt-in is implied by a factory being set; `RunsCmdShims` is used only by tests

- **Kind / Severity:** Judgement call (Mysterious Name / implicit flag) / Low
- **Repository note:** `CLAUDE.md` documents the current form ("opted in by the last `ExternalTool` parameter"). Changing it means updating that sentence too.
- **Location:** `Processes/ExternalTool.cs:40,49`; `Processes/ExternalToolRunner.cs:175`
- **Verified:** **FACT**.
  - `RunsCmdShims` is referenced only by `CopilotCommandRunnerTests.cs:59` and `CodexCommandRunnerTests.cs:59`.
  - Production code checks `tool.CreateCommandFailedException is not { } …` directly.

**Excerpt (`ExternalTool.cs:34-49`):**

```csharp
internal sealed record ExternalTool(
    string Name,
    string DisplayName,
    IReadOnlyDictionary<string, string>? EnvironmentVariables,
    Func<IReadOnlyList<string>, string, ExternalToolUnavailableException> CreateNotFoundException,
    Func<IReadOnlyList<string>, string, Exception?, ExternalToolUnavailableException> CreateUnavailableException,
    Func<IReadOnlyList<string>, string, ExternalToolUnavailableException>? CreateCommandFailedException = null)
{
    public bool RunsCmdShims => CreateCommandFailedException is not null;
```

**Excerpt (`ExternalToolRunner.cs:175-179`):**

```csharp
if (tool.CreateCommandFailedException is not { } createCommandFailedException
    || !CmdShimCommandLine.IsCmdShim(executablePath))
{
    return ExternalProcessStart.WithArguments(executablePath, arguments);
}
```

**Impact:** running a tool through `cmd.exe` is a documented, security-relevant exception to the "no shell" rule (BatBadBut, CVE-2024-24576). Today a developer who adds an error factory to `git` or `claude`, for example to get nicer refusal messages, would turn on shell execution without noticing. The property that names the intent (`RunsCmdShims`) is not the one production code reads.

**Proposed solutions:**
1. *(recommended)* Make the opt-in explicit: add a `CmdShimPolicy? CmdShims = null` parameter, a record with the `CreateRefusedArgumentException` factory. Use `RunsCmdShims => CmdShims is not null` in the runner as well. Update the `CLAUDE.md` sentence ("opted in by the last `ExternalTool` parameter" → "opted in by `ExternalTool.CmdShims`").
2. Minimal: let `ExternalToolRunner.CreateProcessStart` check `tool.RunsCmdShims` first, and rename the parameter to `CreateCmdShimRefusalException`, so the name says it only exists for shims.
3. Add a guard test asserting that only the `copilot` and `codex` tools set the opt-in. Today `CopilotCommandRunnerTests` and `CodexCommandRunnerTests` assert it per tool, but nothing asserts it for `git`, `gh` and `claude`.

**Done when:** the opt-in has a name of its own that production code reads, and `CLAUDE.md` matches.

---

### STD-07 · Agent-id switches spread over four CLI places (Shotgun Surgery)

- **Kind / Severity:** Judgement call (Repeated Switches / Shotgun Surgery) / Medium
- **Repository note:** `CLAUDE.md` requires core exceptions to be mapped only in `CliErrorMapper`, so the per-exception arms there are endorsed. The smell concerns the agent-id switches in the CLI text output and option parsing.
- **Location:**
  - `src/CreativeCoders.ShadeKit.Cli/Commands/Integrations/IntegrationsTextFormat.cs:42-47` (`RestartNotes`) and `:179-189` (`FormatDisabledNote` switch)
  - `src/CreativeCoders.ShadeKit.Cli/Commands/Integrations/Install/AgentOptionConverter.cs:35-38` (`SupportedAgentIds`)
  - `src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs:223-250` (one NotFound / Unavailable / CommandFailed arm per agent)
- **Verified:** **FACT**.

**Excerpt (`IntegrationsTextFormat.cs:42-47` and `:179-189`):**

```csharp
private static readonly (string AgentId, string Text)[] RestartNotes =
[
    (ClaudeCodeIntegration.ClaudeAgentId, RestartClaudeText),
    (CopilotCliIntegration.CopilotAgentId, RestartCopilotText),
    (CodexCliIntegration.CodexAgentId, RestartCodexText)
];
…
return agentId switch
{
    ClaudeCodeIntegration.ClaudeAgentId => FormatEnableCommandNote("Claude Code", "claude", pluginId),
    CopilotCliIntegration.CopilotAgentId => FormatEnableCommandNote("GitHub Copilot CLI", "copilot", pluginId),
    CodexCliIntegration.CodexAgentId =>
        $"{"The plugin is disabled in Codex.".ToWarningMarkup()} Enable it in Codex with " +
        $"{"/plugins".ToCommandMarkup()}.",
    _ => …
};
```

**Excerpt (`AgentOptionConverter.cs:35-38`):**

```csharp
public static IReadOnlyList<string> SupportedAgentIds { get; } =
[
    ClaudeCodeIntegration.ClaudeAgentId, CopilotCliIntegration.CopilotAgentId, CodexCliIntegration.CodexAgentId
];
```

**Impact:**
- Adding an agent means editing the core registration plus four CLI places.
- The order is defined twice: in the DI registration order (`IIntegrationService.AgentIds`) and in `SupportedAgentIds` / `RestartNotes`. The doc comment says it follows `IIntegrationService.AgentIds`, but nothing enforces that.
- A forgotten entry fails silently. A missing restart note just drops the note (documented in the XML doc), and a missing disabled note falls back to a generic text.

**Proposed solutions:**
1. *(recommended)* Add one CLI-side descriptor table, `AgentPresentation` (`AgentId`, `DisplayName`, `RestartText`, `DisabledNote` factory), keyed by agent id and ordered by `IIntegrationService.AgentIds`. `RestartNotes`, `FormatDisabledNote` and `SupportedAgentIds` all read from it. Add a test that every id in `IIntegrationService.AgentIds` has a descriptor and the orders match.
2. Keep the code, and add only the consistency test from option 1. It catches a forgotten agent at build time.
3. Leave `CliErrorMapper` as it is: the arms map exception types, as `CLAUDE.md` requires.

**Done when:** a new agent needs one CLI-side entry, or at least a test fails when an entry is missing.

---

### STD-08 · The same agent-specific text is repeated in many skills and has already drifted

- **Kind / Severity:** Judgement call (Duplicated Code / Shotgun Surgery) / Medium
- **Location:**
  - **Question tool parenthetical:**
    - `skills/shade-kit-drop/SKILL.md:21`
    - `skills/shade-kit-finish/SKILL.md:23`
    - `skills/shade-kit-gate-check/SKILL.md:51`
    - `skills/shade-kit-interview/SKILL.md:75`
    - `skills/shade-kit-new/SKILL.md:25`
    - `skills/shade-kit-phase-entry/SKILL.md:59`
  - **Call form rule:**
    - `skills/shade-kit-next-steps/SKILL.md:29,76`
    - `skills/shade-kit-phase-entry/SKILL.md:179`
    - `skills/shade-kit-quick/SKILL.md:32`
    - `src/CreativeCoders.ShadeKit.Core/Init/ManagedInstructionSection.cs:39`
- **Verified:** **FACT**. The copies are not identical:
  - `gate-check:51` adds the per-call limits ("up to 4 questions per call", "up to 3 questions per call").
  - `phase-entry:59` and `interview:75` add "which Codex offers only in plan mode by default".
  - `gate-check` says "by default only in plan mode".
  - The others are shorter.

**Excerpt (`skills/shade-kit-gate-check/SKILL.md:51`):**

```markdown
… with the agent's structured question tool where one exists (in Claude Code `AskUserQuestion`, up to 4 questions
per call; in GitHub Copilot CLI `ask_user`; in OpenAI Codex CLI `request_user_input`, by default only in plan mode,
up to 3 questions per call): …
```

**Excerpt (`skills/shade-kit-phase-entry/SKILL.md:59`):**

```markdown
… ask one question with the agent's structured question tool (in Claude Code `AskUserQuestion`, in GitHub Copilot
CLI `ask_user`, in OpenAI Codex CLI `request_user_input`, which Codex offers only in plan mode by default; without
such a tool, also …
```

**Excerpt (`skills/shade-kit-phase-entry/SKILL.md:179`):**

```markdown
- Write a skill call that you show the human in the form of the agent you run in: `/shade-kit-<skill>` in Claude Code
  and GitHub Copilot CLI, `$shade-kit:shade-kit-<skill>` in OpenAI Codex CLI.
```

**Impact:**
- When a CLI renames a tool or lifts the plan-mode limit, six skill texts and the managed section must change together. Skill texts have no automated tests (`CLAUDE.md`), so a missed copy goes unnoticed.
- The drift has already started: a skill without the "plan mode only" remark lets the agent try `request_user_input` outside plan mode, where it is not available by default. The agent then needs a fallback it was not told about.

**Proposed solutions:**
1. *(recommended)* Pick one canonical wording for each of the two rules and add a guard test, in the style of `ShippedSkillsTests`. The test fails if a skill names `request_user_input` without the canonical parenthetical, or `$shade-kit:` without the canonical call-form sentence. This keeps the texts self-contained, which matters because Claude Code shows the model only the skill body, while still catching drift.
2. Centralise: the question tool rules live only in `shade-kit-interview` ("Asking"), and the call form only in `shade-kit-next-steps`. The other skills say "ask with the agent's structured question tool (see the agent names in `shade-kit-interview`, section Asking)". Trade-off: a skill that does not load `shade-kit-interview` would not see the names. Check with scenario runs before choosing this.
3. Accept it, and add a checklist item to the review by hand of a changed skill: "agent-specific parentheticals identical in all skills".

**Done when:** the texts are identical, and a test or checklist keeps them identical.

---

### STD-09 · `…CommandFailedException` derives from `ExternalToolUnavailableException`

- **Kind / Severity:** Judgement call (Mysterious Name, inherited from the Claude stack in #34) / Low
- **Location:** `Integrations/Exceptions/CopilotCommandFailedException.cs:29`, `Integrations/Exceptions/CodexCommandFailedException.cs:38`; base `Processes/Exceptions/ExternalToolUnavailableException.cs:9`
- **Verified:** **FACT**. The catch filters `catch (ExternalToolUnavailableException) when (isRemoved)` (`CopilotCliIntegration.cs:251`, `CodexCliIntegration.cs:250`, `ClaudeCodeIntegration.cs:229`) depend on this hierarchy, so it carries load.

**Excerpt (`CodexCommandFailedException.cs`, without doc comments):**

```csharp
public sealed class CodexCommandFailedException(
    IReadOnlyList<string> arguments,
    int? exitCode,
    string reason,
    string? codexMessage = null,
    Exception? innerException = null)
    : ExternalToolUnavailableException("codex", "failed", arguments, reason, innerException)
```

**Impact:** a command that ran and failed, for example a plugin list with invalid JSON or exit code 1, has the type "unavailable". Readers of the catch filters can misread `catch (ExternalToolUnavailableException)` as "tool missing", while it also catches every failed step.

**Proposed solutions:**
1. Rename the base to `ExternalToolException`, keeping the abstract class and the same members, in one refactoring commit across git, gh, claude, copilot and codex. It touches many files but changes no behaviour.
2. *(recommended)* Keep the hierarchy, and add a short comment at each `catch (ExternalToolUnavailableException) when (isRemoved)` that it also catches failed commands. Or define the filter once in the shared helper from STD-04, option 2.

**Done when:** the type name or a comment at the catch sites makes the semantics clear.

---

## Spec axis

### SPEC-01 · FP08-AC2 / AC3: the session part is not proven

- **Kind / Severity:** Spec gap / High (acceptance criterion not met)
- **Spec:**
  > **FP08-AC2** After `integrations install --agent copilot`, a Copilot session can load `shade-kit-next-phase` and run the version handshake.

  > **FP08-AC3** After `integrations install --agent codex`, a Codex CLI session can load `shade-kit-next-phase` and run the version handshake.
- **Location:** `docs/feature-packages/08-copilot-and-codex-integration.md:181-182`, `:191` ("Unverified … loading `shade-kit-next-phase` in a session and its version handshake (FP08-AC2, FP08-AC3)")
- **Verified:** **FACT**. The install part is proven by `IntegrationsMultiAgentEndToEndTests` and `Copilot/CodexCliIntegrationTests` with the traits `FP08-AC2` / `FP08-AC3`, all against `FakeCopilot` / `FakeCodex`. No real CLI was run (spec 08, "Unverified: none of the commands has been run").

**Excerpt (spec 08, line 181, end):**

```markdown
… Session part: **Unverified**, a check by hand per the research note's [checks by hand](…#8-checks-by-hand-for-…
```

**Impact:** both acceptance criteria are user-facing: "the skills work in the agent". All automated evidence rests on fakes, which were built from documentation and source reading. Several assumptions are open:
- that `"source": "./"` resolves;
- that a tag works as a ref (`owner/repo#v0.8.0`, `--ref v0.8.0`);
- the JSON shape of `copilot plugin marketplace list --json`;
- the exit codes;
- that the skills load at all.

If one of them is wrong, the integration fails in real use, even though all 6289 tests are green.

**Proposed solutions:**
1. *(recommended)* Before merging, or before tagging a release that claims Copilot/Codex support, run the checks by hand from `research/shade-kit-agent-compatibility.md` §8 on at least macOS:
   1. Install a released or local marketplace (`SHADE_KIT_MARKETPLACE_SOURCE=$PWD shade-kit integrations install --agent copilot`, then `--agent codex`).
   2. Start a session and call `/shade-kit-next-phase` (Copilot) or `$shade-kit:shade-kit-next-phase` (Codex).
   3. Confirm that `shade-kit version --json --expect <x.y.z>` runs.

   Record the CLI versions, the commands and the outcome in spec 08 under AC2/AC3. Also record the real JSON output of the list commands and compare it with the `FakeCopilot` / `FakeCodex` fixtures.
2. Merge with the ACs explicitly marked "install part done, session part open", and track the session part as a release blocker in `docs/feature-packages/README.md` (milestones / consistency log).

**Done when:** AC2 and AC3 have a recorded manual run with the CLI versions, or are explicitly deferred in the README with an owner.

---

### SPEC-02 · FP08-AC4 is met by a reinterpretation; the AC text still says "produced at install time"

- **Kind / Severity:** Spec gap (reinterpretation) / Medium
- **Spec:**
  > **FP08-AC4** The kit repository holds one source per skill; agent-specific differences are produced at install time.
- **Location:**
  - `docs/feature-packages/08-copilot-and-codex-integration.md:183` (AC)
  - `:49` (the decision "One skill source for all three agents … There is no install-time transformation", **Decided** on 2026-11-15, Gate 2)
  - an example of differences now in the shared source: `skills/shade-kit-interview/SKILL.md:75-84`
- **Verified:** **FACT**.
  - The AC sentence is unchanged; only "Implemented with a stricter result: there are no agent-specific differences in the content, so nothing is produced at install time" was added.
  - In fact the differences (tool names, call forms, Codex limits) are written into the one source as agent-conditional text. They are not removed.

**Excerpt (spec 08, line 183):**

```markdown
- **FP08-AC4** The kit repository holds one source per skill; agent-specific differences are produced at install
  time. **Implemented** with a stricter result: there are no agent-specific differences in the content, so nothing
  is produced at install time (Content source). …
```

**Impact:**
- The AC and its "Implemented" note contradict each other. The note says "no agent-specific differences in the content", but the skills do contain agent-specific text (see STD-08). The difference is that each agent reads all variants and picks its own.
- A later reader cannot tell whether the AC was met or replaced.
- The design choice moves cost from the engine to every skill text: every agent reads every variant, and the texts must stay consistent (STD-08).

**Proposed solutions:**
1. *(recommended)* Rewrite AC4 to match the Gate 2 decision of 2026-11-15, and get the human's sign-off. For example: "The kit repository holds one source per skill for all agents; agent-specific differences (tool names, call forms) are written in that source as text for each agent, and nothing is transformed at install time." Correct the "Implemented" note: there *are* differences, held as agent-conditional text. Add an entry to the consistency log in `docs/feature-packages/README.md`.
2. Keep the AC and implement install-time transformation. Not recommended: it contradicts the decided "engine contains and copies no content" (spec 08, line 49; CLAUDE.md "The engine contains no plugin content").

**Done when:** the AC text, the "Implemented" note and the decision on line 49 say the same thing, and the change is in the consistency log.

---

### SPEC-03 · FP08-AC5 rests on one scenario run that left no artifact

- **Kind / Severity:** Spec gap (proof not reproducible) / Medium
- **Spec:**
  > **FP08-AC5** The research skill (package 06) produces the same `researches.md` structure on every agent, also where it runs sequentially.
- **Location:** `docs/feature-packages/08-copilot-and-codex-integration.md:184`
- **Verified:**
  - **FACT**: the AC is marked "Implemented by a scenario run (Task 9, 2026-11-15)", and no output of that run is in the repository.
  - **Unverified**: whether the run behaved as described.
  - The run was a Claude subagent simulating "parallel" and "sequential"; no Copilot or Codex session was involved.

**Excerpt (spec 08, line 184):**

```markdown
- **FP08-AC5** … **Implemented** by a scenario run (Task 9, 2026-11-15): a subagent loaded the changed
  `shade-kit-research` and followed it with canned CLI envelopes once with parallel subagents and once with the
  focuses one after the other in the main session; the two `researches.md` sections had the identical structure …
  No automated test (skill texts have none, CLAUDE.md).
```

**Impact:** the AC says "on every agent". The evidence covers one agent (Claude), simulating the other agents' sequential mode. Nobody can re-check it later, because the two generated `researches.md` sections and the transcript were not kept.

**Proposed solutions:**
1. *(recommended)* Repeat the scenario run and keep its outputs, the two `researches.md` excerpts and the command log, as fixtures. A natural place is `docs/implementation/` (the folder exists, with `06-4-gate-check-skill.md` as precedent). Reference them from AC5.
2. Narrow the "Implemented" note: "on Claude Code with parallel and sequential execution; Copilot and Codex sessions unverified (see SPEC-01)". Fold the cross-agent part into the manual check of SPEC-01.

**Done when:** AC5 points to a kept artifact, or its claim is narrowed to what was observed.

---

### SPEC-04 · `shade-kit-interview` changes more than G3 asks for

- **Kind / Severity:** Spec deviation (small scope creep) / Low
- **Spec (G3):**
  > Tool names: where a skill names a Claude Code tool, it also names Copilot's and Codex's: `AskUserQuestion` / `ask_user` / `request_user_input` (in Codex only in plan mode by default; outside it the skills ask plain questions) … The interview offers at most two alternatives and three questions per call in Codex.
- **Location:** `skills/shade-kit-interview/SKILL.md:78` and `:84`; spec `docs/feature-packages/08-copilot-and-codex-integration.md:169`
- **Verified:** **FACT** (`git diff main...HEAD -- skills/shade-kit-interview/SKILL.md`).

**Excerpt (diff):**

```diff
-- Your proposal is the first option, marked as recommended (in Claude Code with `(Recommended)` at the end of the label). Exactly one option is marked.
+- Your proposal is the first option, marked as recommended with `(Recommended)` at the end of the label, the same in every agent (OpenAI Codex CLI's tool asks for this marker on the first option). Exactly one option is marked.
…
-Without such a tool, ask a plain question in the same structure, one topic per message, starting with the progress (`Topic 3/8`), so that every answer can be matched to its topic.
+Without such a tool, also in OpenAI Codex CLI outside plan mode, ask a plain question in the same structure, one topic per message, starting with the progress (`Topic 3/8`), so that every answer can be matched to its topic: the context, then your proposal first, marked as recommended, with its reason, then the alternatives.
```

**Impact:**
- `(Recommended)` now becomes the marker in Copilot too. G3 does not decide that, and it is unverified that Copilot's `ask_user` handles the marker well.
- The new structure for plain questions applies to every agent without a tool, Claude included. G3 only says "the skills ask plain questions".
- Both changes are sensible, but they are behaviour changes without a recorded decision.

**Proposed solutions:**
1. *(recommended)* Record both changes as decisions in spec 08 (G3 or the "Skill texts" section) with a date, and mention the Copilot marker in the checks by hand of SPEC-01.
2. Revert to the G3 scope: keep `(Recommended)` for Claude Code and Codex only, and drop the plain question structure sentence.

**Done when:** the behaviour is either covered by a recorded decision or reverted.

---

### SPEC-05 · Codex `update` / `install` removes the integration on a network or login failure

- **Kind / Severity:** Risk / High
- **Spec:**
  > Known limits (D5): a check add that fails for another reason (network, login) counts as "other source", so the switch runs and then fails at its add with `IntegrationIncompleteException`; a new run repairs it.
- **Location:** `src/CreativeCoders.ShadeKit.Core/Integrations/Codex/CodexCliIntegration.cs:262-275` (`TryKeepGitMarketplaceAsync`), the switch in `:227-243`; spec 08 line 108
- **Verified:** **FACT** (code path). The behaviour is pinned by the test `CodexCliIntegrationTests.InstallAsync_CheckAddFailsWithAnyNonZeroExitCode_SwitchesMarketplace` (`tests/…/Codex/CodexCliIntegrationTests.cs:454`). **Unverified:** how real `codex` behaves offline: its exit code, and whether `plugin remove` / `marketplace remove` need the network (likely not, as they are local config).

**Excerpt (`CodexCliIntegration.cs:262-275`):**

```csharp
// Returns true if the marketplace has the source and was fetched again, false if codex added it just now, and
// null if codex refused the add, which means that the marketplace has another source or ref.
private async Task<bool?> TryKeepGitMarketplaceAsync(MarketplaceSource source,
    CancellationToken cancellationToken)
{
    CodexMarketplaceAddResult addResult;

    try
    {
        addResult = await _codexPluginCli.AddMarketplaceAsync(source, cancellationToken).ConfigureAwait(false);
    }
    catch (CodexCommandFailedException exception) when (exception.ExitCode is not (null or 0))
    {
        return null;
    }
```

**Excerpt (`CodexCliIntegration.cs:225-243`, the switch that follows a `null`):**

```csharp
else
{
    isSwitching = true;

    if (pluginBefore is not null)
    {
        await _codexPluginCli.RemovePluginAsync(ShadeKitPlugin.Id, cancellationToken).ConfigureAwait(false);
        isRemoved = true;
    }

    await _codexPluginCli.RemoveMarketplaceAsync(ShadeKitPlugin.MarketplaceName, cancellationToken)
        .ConfigureAwait(false);
    isRemoved = true;

    await _codexPluginCli.AddMarketplaceAsync(source, cancellationToken).ConfigureAwait(false);
}
```

**Impact (failure scenario):**
1. The user has a working Codex integration from the GitHub marketplace and runs `shade-kit integrations update` (or `install`) without network, behind a proxy, or with an expired login.
2. The check `codex plugin marketplace add … --json` fails with a non-zero exit code for a network reason.
3. `TryKeepGitMarketplaceAsync` returns `null`, so the code treats it as "other ref".
4. The plugin and the marketplace are removed, the new `add` fails for the same reason, and the command ends with `IntegrationIncompleteException`. `CliErrorMapper.CreateIntegrationIncompleteError` (`CliErrorMapper.cs:793`) maps it with the exit code of the inner failure, which is 52 for a failed `codex` command, and adds the repair hint "'shade-kit integrations install' repairs it".

Result: the skills that worked before are gone from Codex until a later run with network succeeds. An "update" that cannot reach the network should change nothing. Claude and Copilot do not have this problem, because they read the ref from the list output.

**Proposed solutions:**
1. *(recommended)* Tell "other source" apart from other failures before anything is removed:
   - Option a: match codex's refusal message. `CodexCommandFailedException.CodexMessage` holds the last stderr line; take the exact wording of "already added with a different source/ref" from the Codex source at tag `rust-v0.169.0`, which the research note already cites. Only that message leads to the switch; any other failure is rethrown as is, and nothing is removed.
   - Option b: if codex uses a distinct exit code for that refusal, filter on it.

   Change `InstallAsync_CheckAddFailsWithAnyNonZeroExitCode_SwitchesMarketplace` into two tests: refusal → switch; other failure → rethrow, no remove. Update D5 in spec 08.
2. Check reachability first: before the switch, run the existing `IGitRemoteTagChecker` (`git ls-remote`, https, no credential helpers) against the source repository. If it fails, abort with a clear error before removing anything. Cheaper to build, but it adds a git dependency to the Codex path and still does not cover a Codex login failure.
3. Read the ref from Codex's own state. Check whether Codex stores the marketplace ref in its config (for example `~/.codex/…`); **Unverified**. If so, read it directly and drop the probe-by-add. This needs research, and reading another tool's config files is brittle.
4. Keep D5, but make the failure more visible. The incomplete error for Codex names the cause (network/login) and the repair command, and the risk is listed in the release notes. Least effort; the destructive step stays.

**Done when:** an `update` / `install` whose check add fails for a reason other than "other source" removes nothing, and a test proves it.

---

### SPEC-06 · The rule that drops the version arrow also changes Claude Code lines

- **Kind / Severity:** Spec deviation / Low
- **Spec (line 125):**
  > Plain output, one line per agent as in 07. When the previous kit version equals the new one, the old version and the arrow are left out: `copilot: updated shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)`, because Copilot and Codex report every kept integration as `updated`. JSON and the Claude Code lines are unchanged (Claude Code reports `current` instead).
- **Location:** `src/CreativeCoders.ShadeKit.Cli/Commands/Integrations/IntegrationsTextFormat.cs:127-131`; previous version on `main` (`git show main:src/CreativeCoders.ShadeKit.Cli/Commands/Integrations/IntegrationsTextFormat.cs`, lines 96-99)
- **Verified:** **FACT**.
  - The condition does not depend on the agent.
  - Claude still reports `Updated` (not `Current`) when the marketplace was switched, or when a kept marketplace updated the plugin to a new commit with the same kit version. `ClaudeCodeIntegration.cs:265` returns `Current` only for `isMarketplaceKept && update.IsUpToDate`.

**Excerpt (`main`):**

```csharp
if (result is { Outcome: IntegrationOutcome.Updated, PreviousKitVersion: { } previousKitVersion })
{
    version = $"{previousKitVersion.ToEscapedMarkup()} -> {version}";
}
```

**Excerpt (branch, `:127-131`):**

```csharp
// Copilot and Codex report every kept integration as updated, so the same version is not written twice.
if (result is { Outcome: IntegrationOutcome.Updated, PreviousKitVersion: { } previousKitVersion }
    && !string.Equals(previousKitVersion, result.KitVersion, StringComparison.Ordinal))
{
    version = $"{previousKitVersion.ToEscapedMarkup()} -> {version}";
}
```

**Impact:** for Claude, the line `claude: updated … 0.8.0 -> 0.8.0 (main)` becomes `claude: updated … 0.8.0 (main)`. This happens when the plugin is updated to a new commit with the same kit version, for example on the `main` fallback or after a switch from tag `v0.8.0` to `main`. The new output is arguably better, but it contradicts "the Claude Code lines are unchanged". The spec sentence is also inconsistent in itself, because its general rule covers Claude too. JSON is not affected.

**Proposed solutions:**
1. *(recommended)* Keep the code and fix the spec. Replace "JSON and the Claude Code lines are unchanged (Claude Code reports `current` instead)" with: "JSON is unchanged. The rule applies to every agent; for Claude Code it only shows when the plugin was updated to another commit with the same kit version, since an unchanged plugin reports `current`." Optionally add an `IntegrationsTextFormatTests` case for a Claude result.
2. Limit the rule to Copilot and Codex: add `&& result.AgentId != ClaudeCodeIntegration.ClaudeAgentId`. Not recommended: it adds another agent-id switch (STD-07) to keep a redundant arrow.

**Done when:** the spec sentence and the code agree.

---

### SPEC-07 · Copilot's cancellation filter keys on `change == Switch`, Codex uses an explicit flag

- **Kind / Severity:** Risk (fragile, correct today) / Low
- **Spec:** the switch cancellation handling from 07 (`IntegrationSwitchCanceledException`, "A cancellation while the first removal runs counts too").
- **Location:** `src/CreativeCoders.ShadeKit.Core/Integrations/Copilot/CopilotCliIntegration.cs:213-232,255-260`; compare `Codex/CodexCliIntegration.cs:227,254`
- **Verified:** **FACT**.
  - Copilot reports any cancellation inside `try` as a switch cancellation when `change == Switch`.
  - Today the first statement of the `Switch` case is the uninstall/remove, so this is correct.
  - Codex sets `isSwitching = true` right before its first removal.

**Excerpt (`CopilotCliIntegration.cs:255-260`):**

```csharp
catch (OperationCanceledException exception) when (change == MarketplaceChange.Switch &&
                                                   cancellationToken.IsCancellationRequested)
{
    // A cancellation while the first removal runs counts too: the killed copilot may have removed it.
    throw new IntegrationSwitchCanceledException(AgentId, exception);
}
```

**Excerpt (`CodexCliIntegration.cs:225-227` and `:254`):**

```csharp
else
{
    isSwitching = true;
…
catch (OperationCanceledException exception) when (isSwitching && cancellationToken.IsCancellationRequested)
```

**Impact:** if someone adds a step to the Copilot `Switch` case before the removal, for example a pre-check or a refresh, a Ctrl+C during that step would be reported as "switch canceled, the integration may be incomplete", although nothing was removed. The two integrations also express the same rule differently (see STD-04).

**Proposed solutions:**
1. *(recommended)* Use the Codex pattern in Copilot: `var isSwitching = false;`, set it to `true` as the first statement of `case MarketplaceChange.Switch:`, and filter on `isSwitching`. Check Claude (`ClaudeCodeIntegration.cs:233`, same `change == Switch` filter) and align it too. Existing tests should pass unchanged.
2. Combine with STD-04, option 2: the shared failure translation takes `isSwitching` as a parameter.

**Done when:** all three integrations decide "switch canceled" with the same explicit flag.

---

### SPEC-08 · Garbled sentence in package 02

- **Kind / Severity:** Spec deviation (documentation typo) / Low
- **Location:** `docs/feature-packages/02-repository-initialisation.md:212`
- **Verified:** **FACT**.

**Excerpt:**

```markdown
- Content: the repository uses SHADE kit, the kit data lives in `.shadekit/`, that a phase starts only when the user
  calls its phase skill the call form of a skill per agent, where to see the next steps …
```

**Impact:** the list item cannot be parsed ("calls its phase skill the call form of a skill per agent"), so the description of the managed instruction section is unclear.

**Proposed solution:** "… that a phase starts only when the user calls its phase skill, the call form of a skill per agent, where to see the next steps …". Better still, split it into separate list items, matching the order of `ManagedInstructionSection.Text`.

**Done when:** the sentence reads correctly and matches `ManagedInstructionSection.cs`.

---

## Totals

- **Standards:** 1 hard violation (STD-01), 8 judgement calls (STD-02 … STD-09). The most serious is **STD-01**: `CLAUDE.md` leaves out the single-skill-source guard.
- **Spec:**
  - 3 gaps: SPEC-01, SPEC-02, SPEC-03.
  - 3 deviations: SPEC-04, SPEC-06, SPEC-08.
  - 2 risks: SPEC-05, SPEC-07.
  - The most serious are **SPEC-05**, the destructive Codex update on network or login failure, and **SPEC-01**, the open session part of AC2/AC3.

### Suggested order of work

1. SPEC-05: code fix plus tests. It is the only finding that can break a working user setup.
2. SPEC-01: checks by hand on real CLIs. Their results may change the fakes and therefore other findings.
3. STD-01, SPEC-02, SPEC-06, SPEC-08: doc-only fixes, quick.
4. SPEC-07 together with STD-04, option 2: shared failure translation.
5. STD-08: consistency guard for skill texts.
6. STD-06, STD-07, STD-05, STD-02, STD-03, STD-09: refactorings, with no behaviour change.
7. SPEC-03, SPEC-04: record artifacts and decisions.
