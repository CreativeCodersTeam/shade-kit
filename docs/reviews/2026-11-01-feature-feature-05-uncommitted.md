# .NET Code Review — feature/feature-05 (uncommitted)

**Date:** 2026-11-01
**Mode:** uncommitted (origin: provided)
**Report language:** English (origin: provided)
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (all five `*.csproj` target `net10.0`; SDK pinned in `global.json`)
**Checklist:** review-checklist-net10.md, plus the general checklists (security, performance, architecture, code quality)
**Tools run:** build=N · format=N · test=N (origin: provided; build and tests ran in Phase 4 of the calling workflow)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive)
**Diff size:** 54 files, 5034 changed LOC (plus the deleted `Workflow/DefaultTransitionGateProvider.cs` and its tests)
**Scope:** feature package 05 "Gates and checklists" (`docs/feature-packages/05-gates-and-checklists.md`); project conventions from `CLAUDE.md` used as review criteria

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 3 |
| Suggestion | 9 |
| Nitpick | 2 |

Six of the 14 findings (R05-02, R05-03, R05-06, R05-07, R05-08, R05-13) are open points that package 05 already documents and the user has accepted. They are rated here as the caller allowed and marked **known**. R05-04 is partly covered by a known open point. 7 findings are new.

**Top risks:**
1. R05-01: two gates that name the same `checklist` share one feature copy. The second phase start reuses the filled record of the first gate as if it were a reopen. Neither package 05 nor the code mentions this.
2. R05-02 (known): a file at `features/<id>/checks` (or at a nested checklist directory) throws an `IOException` that is not mapped, so the CLI exits with 255 and prints a stack trace, which breaks the JSON agent contract.
3. R05-04 / R05-03 (partly known): profile names, phase names, and checklist values from `gates.json` reach the agent's output and the terminal without a check for control characters.

**Overall:** The implementation is solid and follows the project conventions:
- JSON is read only through `IKitStore.ReadJsonAsync`.
- The copy is a `TextFileContent` written through the store.
- Errors are mapped only in `CliErrorMapper`.
- 47 sits in the 40–49 range.
- `Workflow/Engine/` contains no names.
- The tests fake `git`.
- The cached reader handles concurrency, failure, and cancellation carefully.

No finding blocks a merge. The remaining items are an undocumented edge case of the approved path rule, a few readability and test-style points, and the open points already accepted.

Verified facts this review relied on:
- **FACT:** `KitStore.FileExists`, `DirectoryExists`, `ReadFileAsync`, and `WriteFileAsync` resolve every path below the store root (`RepositoryFilePath.GetFullPathBelowRoot`), so a path outside `.shadekit/` is impossible even without the checklist validation.
- **FACT:** `LoadedFeature.FeatureId` is the resolved folder id, not the id stored in `feature.json`, so the copy always lands in the folder of the feature.
- **FACT:** `KitWarningOrder.Arrange` removes equal records, so warnings that the cached read returns more than once are reported once.
- **FACT:** `VersionedJsonSerializer` uses `AllowDuplicateProperties = false` and `RespectNullableAnnotations = true`, and `GatesConfig.OnDeserialized` follows the same pattern as `KitConfig`, `FeatureDocument`, and `TemplatesLock`.
- **FACT:** warning messages are escaped with `ToEscapedMarkup()` before plain output (`ShadeKitCommandContext.cs:178`).

**Not verified in this review:** the ≥ 90 % line coverage of the new files, the Phase 4 build and test results quoted in the appendix, and whether Spectre.Console removes control characters such as ESC from escaped text (relevant to R05-04).

## Findings — src/CreativeCoders.ShadeKit.Core/Gates/FeatureChecklistWriter.cs

### R05-01 [Minor][Architecture] src/CreativeCoders.ShadeKit.Core/Gates/FeatureChecklistWriter.cs:45
Two gates of one profile that name the same `checklist` share one copy, so the second gate reuses the filled record of the first gate.

`GetFeatureChecklistPath(featureId, checklist)` turns the gate's value straight into the copy's path, and `EnsureCopyAsync` returns `Created: false` for an existing copy. Suppose a team points two transitions at one template, for example `"implement": { "checklist": "checks/security.md" }` and `"finish": { "checklist": "checks/security.md" }`:
- `phase start finish` finds the copy that the `implement` gate left and does not copy the template again.
- The agent then overwrites `Result` and `Evidence` of the earlier gate's record. Only the one-line "Check runs" summary survives.

Package 05 says that "the filled copy is the record of the gate". Nothing in `GatesConfig` validation, the reader warnings, or the documentation covers a shared path. This is new and is not one of the known open points.

Either reject or warn about a checklist path that more than one gate of a profile uses, or document that gates sharing a template also share one record. A warning keeps the file valid:

```csharp
// GatesConfigReader.CheckEntries, after the per-phase checks of a known profile
foreach (var shared in gates
             .Where(x => x.Value.Checklist is not null)
             .GroupBy(x => x.Value.Checklist, StringComparer.Ordinal)
             .Where(x => x.Count() > 1))
{
    // new reason, mapped in CliErrorMapper like the other reasons
    yield return new GateEntryIgnoredWarning(profileName, shared.Last().Key,
        GateEntryIgnoredReason.SharedChecklist);
}
```

### R05-02 [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Gates/FeatureChecklistWriter.cs:64 — known open point
A file at `features/<id>/checks`, or at any directory of a nested checklist path, makes `WriteFileAsync` throw an `IOException` from `Directory.CreateDirectory` (`TextFileWriter.cs:36`). `CliErrorMapper` does not map it, so the command exits with 255 and a stack trace. With `--json` the agent gets no failure envelope.

Package 05 documents this ("Open points"). It is rated Minor here because it breaks the agent contract for a state that is easy to reach (a stray file). The fix is to check the parent directories before the write and throw a typed exception that `CliErrorMapper` maps:

```csharp
// before reading the template
var parent = copyPath[..copyPath.LastIndexOf('/')];
for (var dir = parent; dir.Contains('/'); dir = dir[..dir.LastIndexOf('/')])
{
    if (store.FileExists(dir))
    {
        // new exception (or reuse of a "path not a directory" code), mapped in CliErrorMapper, e.g. exit code 14 range
        throw new PathNotADirectoryException(KitStorePaths.GetRepositoryPath(dir));
    }
}
```

### R05-05 [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Gates/FeatureChecklistWriter.cs:50
The writer builds the store's full path for `PathNotAFileException` itself with `Path.GetFullPath(Path.Combine(store.RootPath, copyPath))`. The comment says this must equal "the same full path that the store puts into the exception". That is a copy of `KitStore`'s path logic (`RepositoryFilePath.GetFullPathBelowRoot`) outside the store. It can drift, for example if the store's display path or normalisation changes, and then the 14 message of the copy differs from the one of the template.

Let the store own that path. One option is a store method that returns the full path. Another is a store method that throws `PathNotAFileException` for a directory:

```csharp
// IKitStore
string GetFullPath(string relativePath);   // or: void EnsureNotDirectory(string relativePath);

// FeatureChecklistWriter
if (store.DirectoryExists(copyPath))
{
    throw new PathNotAFileException(store.GetFullPath(copyPath));
}
```

### R05-06 [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Gates/FeatureChecklistWriter.cs:53 — known open point
The existence check (`FileExists`) and the write (`WriteFileAsync`) are not atomic. Two concurrent `phase start` calls can both create the copy, and the second overwrites the first. Both write the same template content, so the practical impact is small. Package 05 documents this.

If it ever matters, create the copy with `FileMode.CreateNew` semantics through the writer and treat "already exists" as `Created: false`:

```csharp
// ITextFileWriter / IKitStore: an option that fails if the file exists
var warnings = await store.WriteFileAsync(copyPath, copy, FileWriteMode.CreateNew, cancellationToken)
    .ConfigureAwait(false);   // IOException "exists" -> new FeatureChecklistCopyResult(false, [])
```

## Findings — src/CreativeCoders.ShadeKit.Core/Gates/GatesConfig.cs

### R05-03 [Minor][Security] src/CreativeCoders.ShadeKit.Core/Gates/GatesConfig.cs:160 — known open point
`IsValidChecklistPath` accepts control characters and `:`. On Windows, `checks/x.md:hidden.md` names an alternate data stream of `checks/x.md`. A value with ESC or other control characters is echoed in `phase start` / `phase next` output and in the 22 / 47 messages (OWASP A03, terminal escape injection). `gates.json` is team-owned repository content, so the attacker needs write access to the repository. Package 05 documents this.

Reject these characters in the same validation that the reader and the writer already share:

```csharp
if (checklist.Any(c => char.IsControl(c) || c == ':')
    || !checklist.StartsWith(ChecklistDirectory, StringComparison.Ordinal)
    || ...)
{
    return false;
}
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs

### R05-04 [Suggestion][Security] src/CreativeCoders.ShadeKit.Cli/Output/CliErrorMapper.cs:519 — partly known
`CreateGateEntryIgnoredMessage` puts the profile and phase **keys** of `gates.json` into the `gate-entry-ignored` message. The validation messages in `GatesConfig` (lines 124, 139, 145), which become the 22 `invalid-file` message, do the same with keys and values. Keys are not validated at all: any JSON string is a valid unknown profile or phase. The known open point covers only `checklist` *values*.

`ToEscapedMarkup()` protects only against Spectre markup. Whether Spectre.Console removes ESC and other C0 characters is **unverified**. If it does not, a key such as `"\u001b]0;pwned\u0007"` reaches the terminal raw. In JSON mode the agent receives it unchanged.

Treat keys like the checklist values: reject control characters in `OnDeserialized`, or at least make them visible in the message:

```csharp
private static string Printable(string value) =>
    string.Concat(value.Select(c => char.IsControl(c) ? $"\\u{(int)c:x4}" : c.ToString()));

GateEntryIgnoredReason.UnknownProfile =>
    $"'{gatesPath}' configures gates of the unknown profile '{Printable(profileName)}'; they are ignored.",
```

## Findings — src/CreativeCoders.ShadeKit.Core/Gates/GatesConfigReader.cs

### R05-07 [Suggestion][Architecture] src/CreativeCoders.ShadeKit.Core/Gates/GatesConfigReader.cs:82 — known open point
Every repository initialised before 2026-11-01 has a `gates.json` without `profiles`. It gets `gates-defaults-used` on every `phase start` and `phase next` until package 18 (`upgrade`) writes `profiles`. The skills do not show warnings yet (package 05 "Skill skeletons not aligned"), so the warning may go unseen and the repeated noise has no remedy before 18. Package 05 documents this.

Until `upgrade` exists, name the remedy in the warning text, which needs no format change:

```csharp
: $"'{gatesPath}' has no 'profiles' field, so the built-in gate defaults apply. Copy the 'profiles' field " +
  "that 'shade-kit init' writes into the file to configure the gates and to stop this warning.";
```

### R05-08 [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Gates/GatesConfigReader.cs:92 — known open point
The cached `GatesConfig` and its nested dictionaries are one shared, mutable instance per store. They are returned to every caller and protected only by the XML doc ("Callers must not change the returned configuration"). Today no caller changes them (**FACT**: `GatesConfigTransitionGateProvider` and `PhaseWorkflow` only read). Package 05 documents this.

For an enforced contract, have the read result expose a read-only view and keep the document internal:

```csharp
public sealed record GatesConfigReadResult(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionGate>> Profiles,
    IReadOnlyList<KitWarning> Warnings);
```

### R05-13 [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Gates/GatesConfigReader.cs:100 — known open point
The order of the `gate-entry-ignored` warnings relies on `Dictionary<,>` enumeration order. In practice that is insertion (file) order, but .NET does not guarantee it. Package 05 documents this.

If file order becomes a contract, deserialize into an order-preserving type, or sort the warnings explicitly:

```csharp
// .NET 9+: OrderedDictionary<TKey, TValue> keeps insertion order by contract
public OrderedDictionary<string, OrderedDictionary<string, GateConfig>>? Profiles { get; set; }
```

## Findings — src/CreativeCoders.ShadeKit.Core/Workflow/PhaseWorkflow.cs

### R05-09 [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Workflow/PhaseWorkflow.cs:70
`StartAsync` calls `EnsureChecklistCopyAsync` with the same arguments in both branches of `if (decision.HasChanges)`, and declares `copyWarnings` without assigning it. The order "identity → copy → write" is easier to see without the duplicate:

```csharp
GitIdentity? identity = decision.HasChanges
    ? await GetIdentityAsync(store, cancellationToken).ConfigureAwait(false)
    : null;

// Identity before the copy (a missing identity leaves no copy); copy before feature.json.
var copyWarnings = await EnsureChecklistCopyAsync(store, feature.FeatureId, decision, cancellationToken)
    .ConfigureAwait(false);

IReadOnlyList<KitWarning> writeWarnings = identity is null
    ? []
    : await WriteChangeAsync(store, feature, decision.Change, identity, cancellationToken).ConfigureAwait(false);
```

### R05-10 [Suggestion][Architecture] src/CreativeCoders.ShadeKit.Core/Workflow/PhaseWorkflow.cs:242
The approved design keeps `ITransitionGateProvider` per phase, backed by the cached reader. To get the warnings, though, `PhaseWorkflow` now also depends on `IGatesConfigReader` directly and reads `gates.json` itself. Two consequences:
- The seam is no longer the only source of gate information. A replacement provider (the registration is `TryAddSingleton`, so it can be replaced) would still have `phase start` / `phase next` fail on an invalid `gates.json` and report its warnings.
- The comment "asking it after the provider reads gates.json no second time" relies on the provider using the same reader instance. That holds only for the default registration.

This follows the approved decision, so it is rated Suggestion. A follow-up option is to let the seam carry the warnings, so `PhaseWorkflow` has one gate dependency:

```csharp
public interface ITransitionGateProvider
{
    Task<ProfileGates> GetGatesAsync(IKitStore store, WorkflowProfile profile,
        CancellationToken cancellationToken = default);   // gates per target phase + warnings
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Workflow/IPhaseWorkflow.cs

### R05-14 [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Workflow/IPhaseWorkflow.cs:31
A doc comment line was not reflowed after the edit. It is 206 characters long, while the rest of the file wraps at 120. `src/CreativeCoders.ShadeKit.Core/Gates/IGatesConfigReader.cs:41` (147) and `GatesConfigReader.cs:19` (125) are the same.

```csharp
/// byte; a repeated <c>phase start</c> of a pending transition only creates a missing checklist copy.
/// <see cref="GetSkillsAsync" /> and <see cref="GetNextAsync" /> never write and never read the git identity. A
/// feature without phase fields, created before the phase engine, counts as being in the initial state of its
/// profile; the first change writes all phase fields.
```

## Findings — tests/CreativeCoders.ShadeKit.Core.Tests/Gates/GatesConfigReaderTests.cs

### R05-11 [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Gates/GatesConfigReaderTests.cs:823
`WaitUntilReadStartedAsync` polls `Fake.GetCalls(store)` every 5 ms with no bound of its own. If a regression means the read never starts, the test does not fail. It hangs until the test token is canceled, which only happens if the test platform has a timeout configured. A `TaskCompletionSource` that the fake completes on the call is deterministic and fails fast:

```csharp
var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._, A<CancellationToken>._))
    .Invokes(() => readStarted.TrySetResult())
    .ReturnsLazily(call => WaitForCancellationAsync(call.GetArgument<CancellationToken>(3)));

await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
```

## Findings — tests/CreativeCoders.ShadeKit.Core.Tests/Workflow/PhaseWorkflowRobustnessTests.cs

### R05-12 [Suggestion][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Workflow/PhaseWorkflowRobustnessTests.cs:266
`GetNextAsync_GateProviderOrGatesConfigReaderFails_ThrowsItsExceptionAndWritesNothing` branches on `providerFails` in its Arrange section. `tests/CreativeCoders.ShadeKit.Core.Tests/Init/RepositoryInitializerTests.cs:593` loops over the two checklists in its Assert section. Both are conditional logic in a test body. The project accepted a similar case before (N07 of package 03), so this is only a Suggestion. Two facts, or a theory whose data is the arranged fake, make each failure point at one case:

```csharp
public static TheoryData<Action<PhaseWorkflowTestContext, Exception>> FailingGateSources =>
[
    (c, e) => A.CallTo(() => c.GateProvider.GetGateAsync(A<IKitStore>._, A<WorkflowProfile>._, A<string>._,
        A<CancellationToken>._)).ThrowsAsync(e),
    (c, e) => A.CallTo(() => c.GatesConfigReader.ReadAsync(A<IKitStore>._, A<CancellationToken>._)).ThrowsAsync(e)
];
```

## Tool Output Appendix

### dotnet build
- Not run in this review (build=N, provided). The calling workflow reports for Phase 4: 0 warnings, 0 errors (not re-verified here).

### dotnet test
- Not run in this review (test=N, provided). The calling workflow reports for Phase 4: 4336 tests, 4335 passed, 1 skipped (pre-existing, Windows-only) (not re-verified here). Coverage of the new files was not measured in this review.

### dotnet format
- Skipped (format=N, provided). The overlong doc lines in R05-14 are the only formatting points found by hand.
