# .NET Code Review — feature/03-feature-management (uncommitted)

**Date:** 2026-10-21
**Mode:** uncommitted (provided): working tree vs. HEAD, including untracked files
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (all five project files target net10.0; SDK pinned in global.json)
**Checklist:** review-checklist-net10.md (+ security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (all provided as `no`; the caller reports build and the full test suite green: 0 warnings, 1988/1988 tests)
**Report language:** English (provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; large-diff gate triggered: 155 files > 50, 21843 LOC > 2000)
**Diff size:** 155 files, 21843 changed LOC (including Markdown docs, skills, and the first review report)
**Review context:** second pass after the rework. First review: `docs/reviews/2026-10-21-feature-03-feature-management-uncommitted.md` (F01–F19). Binding decisions: `fp03-decisions.md` ("Rework after code review", "R3 correction"); rework facts: `fp03-rework-facts.md` (exit code 130 `canceled` and the R2/R4 decisions confirmed by the user); implementation facts: `fp03-implementation-facts.md`. F03 is accepted by the user and not re-reported. Decided behaviour is not reported as a defect. Where a decision carries a risk, the finding says so.
**Area tags:** besides the tags of the severity taxonomy, this report uses `Docs` for Markdown statements that contradict the code or are imprecise, as in the first review.

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 0 |
| Minor | 2 |
| Suggestion | 1 |
| Nitpick | 4 |

**Resolution of the first review:** 18 of 18 findings in scope (F01, F02, F04–F19) are resolved. F02 is resolved within the decided scope: the process run itself stays untested, which is documented as an accepted gap. F03 stays accepted. Details are in [Resolution of F01–F19](#resolution-of-f01f19).

**Top risks:**
1. **N01:** the new rename-based `drop` turns a rename failure (a locked file on Windows, missing write permission) into exit 255 `host-error` with a full stack trace. **FACT:** reproduced on macOS. The data is safe: the rename is atomic and nothing is deleted. For `new`, the same rename can fail briefly on Windows right after the files are written, while a virus scanner or indexer holds them (**Unverified**, no Windows host).
2. **N02:** `GitExecutableLocator` takes the first file named `git`, even when it is not executable. A shell would skip it and find the real git; `shade-kit` stops with exit 16. **FACT:** reproduced. This is a small regression from the F01 fix, because the runtime's own lookup checked the execute bit.
3. **N03:** a title may contain Unicode bidi overrides such as U+202E (category `Format`, not `Control`). The title then shows reversed text in the plain `list` output. **FACT:** reproduced with `--title`.

**Overall:** The rework is thorough and does what the decisions say. Every finding in scope is fixed in the code, covered by tests, and described in 01/03, `CLAUDE.md`, the skills, and the template. The new findings are edge cases of the new mechanisms (rename failures, the executable lookup, title characters) plus documentation precision. None of them blocks shipping.

**Verified facts behind this review** (built CLI `src/CreativeCoders.ShadeKit.Cli/bin/Debug/net10.0/shade-kit.dll`, newer than every `src/**/*.cs`, run in a scratch repository with a local git identity under the session scratchpad; no repository file was changed):
- **FACT:** `feature show` without an id prints one line, `Invalid arguments: the required argument <FeatureId> is missing. Run 'shade-kit feature show --help' for usage.`, and exits with 2. `feature new --json` without `--source` writes one `invalid-arguments` envelope and exits with 2. (F06)
- **FACT:** SIGINT sent to the process group while a fake `git` sleeps (`sh` with `sleep`, and `exec sleep` so that the child itself dies from the signal): 13 of 13 runs of `feature current --json` returned exit 130 with one `canceled` envelope and an empty stderr. SIGQUIT (Ctrl+\) gives the same result in 2 of 2 runs. (F05, N05)
- **FACT:** `--source $'Fix\tlogin'` → exit 34; a 1,048,577-byte file → exit 33; a UTF-16 LE file with BOM → exit 33 ("encoded as UTF-16 LE"). (F07–F09)
- **FACT:** a broken `current-feature.json` → `feature current` succeeds with the warning `current-feature-invalid`; the next `feature new` replaces it. (F10)
- **FACT:** a copied folder `copy-x` is listed as `copy-x` with the warning `feature-id-mismatch`; a folder `Bad_Name` is skipped with `feature-folder-invalid`; the drop preview contains `overrideAffected`. (F04, F12, F15)
- **FACT:** with `.shadekit/features/` read-only, `feature drop <id> --yes` exits with 255 (`host-error`, `IOException: Access to the path ... is denied` from `KitStore.MoveDirectory`). After write access is restored, the feature folder is complete and still selected by the override. (F13, N01)
- **FACT:** with a non-executable file `git` in the first `PATH` entry, `sh -c 'command -v git'` finds `/usr/bin/git`, but `feature current --json` exits with 16 ("git could not be started ... Permission denied"). (N02)
- **FACT:** `--title $'A‮evil'` is accepted; `feature.json` and `feature list --json` contain the title with U+202E. (N03)
- **FACT:** no `S4036` suppression is left in `src/`. No copy of the repository root, the override path, the `features/<id>` path, the BOM-stripping decode, or the `createdAt` format exists outside its new home. Checked by search. (F01, F14)

## Resolution of F01–F19

| Id | Severity (first review) | Status | Evidence |
|---|---|---|---|
| F01 | Major | Resolved | `src/CreativeCoders.ShadeKit.Core/Git/GitExecutableLocator.cs:88-105` uses only fully qualified `PATH` entries (`IsFullyQualified`, lines 141-167). `GitCommandRunner.cs:34-49` starts the absolute path and refuses an empty or relative one; `:83` passes it as `FileName`. The S4036 suppression is gone. Tests: `GitExecutableLocatorTests.cs` (21 test methods), `GitCommandRunnerTests.cs:29-176`. The Windows `CreateProcess` search no longer applies, because an absolute path is started (the Windows run itself is **Unverified**, no Windows host). |
| F02 | Minor | Resolved (decided scope) | `GitCommandRunnerTests.cs:29-176` covers git not found, an empty or relative path, a start failure (`:112`), a canceled token, and argument guards, without real git. The process run (capture, exit code, kill) stays untested; this is documented in `docs/feature-packages/03-feature-management.md:345`. This review ran the process path after the rework with a fake `git` (see the facts). |
| F03 | Nitpick | Accepted | Not re-reviewed (user decision). |
| F04 | Minor | Resolved | `FeatureDropCommand.cs:113-115` writes `preview.OverrideAffected`; `FeatureDropCommandData` has `overrideAffected` and `overrideCleared`; `FeatureDropCommandTests.cs:104-115`. Docs: 03:218, `skills/shade-kit-drop/SKILL.md:18-20`. |
| F05 | Minor | Resolved | `Program.cs:117-129` (a `ConsoleCancellation` for each run, catch filter with `MapCancellation`); `ConsoleCancellation.cs:42-110`; every feature command and `init` pass `_cancellation.Token`. Tests: `ConsoleCancellationTests.cs`, `FeatureCommandsEndToEndTests.cs:443`, `:506`. FACT: a real SIGINT gives 130 `canceled`. |
| F06 | Minor | Resolved | `CliErrorMapper.cs:180-197` (`MapArgumentError`), `Program.cs:128-139`; exit 2 `invalid-arguments`, no stack trace. Tests: `ProgramTests.cs:811-1071`. FACT: reproduced. |
| F07 | Minor | Resolved | `FeatureIdFactory.cs:218-248`: reads at most limit + 1 bytes through a stream (`MaxSourceFileSize`, `:41`); `IOException` and `UnauthorizedAccessException` → 33 (`:234`). FACT: 1 MiB + 1 → 33. |
| F08 | Minor | Resolved | `FeatureIdFactory.cs:288-314` (`DecodeUtf8`: UTF-16/32 BOM, NUL, strict UTF-8) with the shared `Files/TextFileEncoding.cs`. FACT: UTF-16 LE → 33. |
| F09 | Suggestion | Resolved | `FeatureIdFactory.cs:115`, `:146`, `:163` → `EnsureNoControlCharacter` (`:316-328`) for every title origin. FACT: TAB → 34. Bidi format characters are still accepted (new finding N03). |
| F10 | Minor | Resolved | `CurrentFeatureOverride.cs:37-83` returns the override's own format problem instead of throwing it and can replace the file; `CurrentFeatureResolver.cs:33` adds `current-feature-invalid`; `FeatureStore.cs` `CreateAsync` / `SetCurrentAsync` (`:194`) replace it. FACT: reproduced. |
| F11 | Minor | Resolved | `FeatureStore.cs:361-402` (staging `features/.<id>.<hex>.tmp/`, `MoveIntoPlace`, rollback deletes only the staging folder), `:404-436` (moves the feature away when the override write fails); `KitStore.cs:222-245` (`MoveDirectory` refuses an existing destination). Test: `FeatureStoreCreateTests.cs:1032`. |
| F12 | Minor | Resolved | `FeatureStore.cs:160-171` (`ShowAsync` returns the folder id), `GetCurrentAsync`, `ListAsync`; `CheckFeatureId` (`:310`) gives `feature-id-mismatch`. FACT: reproduced. |
| F13 | Minor | Resolved | `FeatureStore.cs:249` renames to `.<id>.<hex>.dropping` before the delete (`:253`); a failed delete still clears the override (`:259`). Tests: `FeatureStoreTests.cs:1068`, `:1100`, `:1141`. FACT: a failed rename deletes nothing. The exit code of that failure is still 255 (new finding N01). |
| F14 | Suggestion | Resolved | `KitStoreExtensions.cs:27` (repository root, single place), `KitStorePaths.cs:33` (`CurrentFeatureFilePath`), `:96` (`GetFeatureDirPath`), `FeatureTextFormat.cs:15` (`createdAt`), `Files/TextFileEncoding.cs` (BOM handling). FACT: no copies left (search). |
| F15 | Nitpick | Resolved | `FeatureStore.cs:126` (hidden folders ignored), `:133` (`feature-folder-invalid`). FACT: reproduced. |
| F16 | Nitpick | Resolved | `docs/feature-packages/03-feature-management.md:52`: "the last path segment, which must consist only of ASCII digits ... `/issues/142/comments` has no issue number". |
| F17 | Nitpick | Resolved | `skills/shade-kit-drop/SKILL.md:3`: "clears the local override only if it points to the dropped feature". |
| F18 | Nitpick | Resolved | `src/CreativeCoders.ShadeKit.Core/Templates/Resources/request.template.md:8`: "Source kind: text \| url \| file, as `feature new` writes it". |
| F19 | Nitpick | Resolved | `tests/CreativeCoders.ShadeKit.Core.Tests/Features/FeatureStoreTests.cs:1262-1286`: a `[Theory]` with four `InlineData` cases. |

## Findings — src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs

### [Minor][Architecture] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:249
**N01.** A failed rename in `drop` (and in `new`, `MoveIntoPlace` at line 395) ends as exit 255 `host-error` with a full stack trace on stderr. A locked file is the predictable Windows case that F13 named.

**FACT:** with a read-only `features/` folder on macOS, `feature drop <id> --yes --json` returns exit 255 and the message "The command failed unexpectedly: Access to the path ... is denied". Plain mode prints the stack trace. The data is safe: nothing was deleted. The user decided that two failures stay 255: a failed delete after the rename, and a file at `features/<id>`. The rename failure itself is not in that list. The 03 doc (line 175) only says "nothing is deleted" and names no exit code; line 266 covers it only as "any other failure". An agent cannot tell "a file is open, close it and retry" apart from a crash.

A second, **Unverified** effect (no Windows host): on Windows, renaming a folder whose files were written milliseconds before often fails for a short time while a virus scanner or the search indexer holds them open, the "EPERM on rename" known from npm. `new` would then fail now and then with 255 (the staging folder is removed, so nothing is lost).

Map the rename failure to a feature-area exit code with an actionable message. On Windows, consider a short bounded retry around the rename. This needs a user decision (a new exit code, for example 37).

```csharp
// Core: FeatureFolderUnavailableException(string featureId, string reason, Exception inner)
private static void MoveFeatureDirectory(IKitStore store, string featureId, string source, string destination)
{
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            store.MoveDirectory(source, destination);
            return;
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && attempt < 5)
        {
            // A scanner or indexer briefly holds files that were just written.
            Thread.Sleep(TimeSpan.FromMilliseconds(50 * attempt));
        }
        catch (Exception e) when (e is UnauthorizedAccessException
                                  || (e is IOException && !store.DirectoryExists(destination)))
        {
            throw new FeatureFolderUnavailableException(featureId,
                "The feature folder cannot be renamed; a file in it may be open in another program.", e);
        }
    }
}

// DropAsync
MoveFeatureDirectory(store, featureId, KitStorePaths.GetFeatureDirPath(featureId), droppingDir);

// CliErrorMapper.Map
FeatureFolderUnavailableException e => new CliError(ShadeKitCliExitCodes.FeatureFolderUnavailable,
    ShadeKitCliErrorCodes.FeatureFolderUnavailable,
    $"The folder of feature '{e.FeatureId}' cannot be changed: {e.Reason} Nothing was deleted. Close the " +
    "program that uses it, then run the command again."),
```

## Findings — src/CreativeCoders.ShadeKit.Core/Git/GitExecutableLocator.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Git/GitExecutableLocator.cs:104
**N02.** On Unix, the first existing file named `git` wins even when it has no execute bit. A shell skips such a file and keeps searching. `shade-kit` starts it and stops with exit 16.

**FACT:** with a mode-644 file `git` in the first `PATH` entry, `command -v git` returns `/usr/bin/git`, but `feature current --json` fails with `git-unavailable` ("Permission denied"). Before the rework, `Process.Start("git")` used the runtime's own `PATH` search, which on Unix checks that the file is executable. So this is a small regression of the F01 fix. It is rare (a stray file in a `bin` directory), but the error message ("make sure git is installed, on the PATH") misleads, because git is installed. `File.GetUnixFileMode` is a documented API (.NET 7+). Keep the injected seam: change only the default check.

```csharp
public GitExecutableLocator()
    : this(static () => Environment.GetEnvironmentVariable(PathVariableName), OperatingSystem.IsWindows(),
        IsExecutableFile)
{
}

private const UnixFileMode AnyExecute =
    UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

// Like the PATH search of a shell: on Unix, a file without any execute bit is skipped.
private static bool IsExecutableFile(string path)
{
    if (!File.Exists(path))
    {
        return false;
    }

    return OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & AnyExecute) != 0;
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs

### [Suggestion][Security] src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs:318
**N03.** The title check uses `char.IsControl`, which covers only the category `Control` (Cc). Bidi overrides and isolates (U+202A–U+202E, U+2066–U+2069, U+200E/U+200F, U+061C, category `Format`) and the line and paragraph separators U+2028/U+2029 pass.

**FACT:** `--title $'A‮evil'` is accepted. The title is stored in `feature.json` and `request.md` and printed reversed in the plain `feature list` and `show` output (Trojan Source style, CVE-2021-42574). An agent may pass an issue title from an untrusted tracker as `--title`. The slug is not affected, because it keeps only letters and digits. Rejecting the whole `Format` category would also reject the zero-width joiner in emoji sequences, so reject only the bidi controls and the two separators.

```csharp
private static bool IsForbiddenTitleCharacter(char character)
{
    return char.IsControl(character)
           || character is '؜' or '‎' or '‏' or (>= '‪' and <= '‮')
               or (>= '⁦' and <= '⁩')
           || CharUnicodeInfo.GetUnicodeCategory(character)
               is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}

// EnsureNoControlCharacter
if (!title.Any(IsForbiddenTitleCharacter))
{
    return;
}

var forbiddenCharacter = title.First(IsForbiddenTitleCharacter);
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs:327
**N06.** The `invalid-title` message ends with "Pass such a title with --title." For a title passed with `--title`, this sends the user in a circle. For a derived title, "such a title" can be read as the rejected one.

**FACT:** the text-source case prints "The title taken from the first line of the text contains the control character U+0009; ... Pass such a title with --title." Name what to pass, and add the `--title` hint only when the title was derived.

```csharp
var hint = titleOrigin == ExplicitTitleOrigin
    ? string.Empty
    : " Pass a single-line title without control characters with --title.";

throw new InvalidFeatureTitleException(
    $"The title {titleOrigin} contains the control character U+{(int)controlCharacter:X4}; a title must be " +
    $"a single line without tabs or other control characters.{hint}");
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Output/ConsoleCancellation.cs

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Output/ConsoleCancellation.cs:107
**N05.** The handler does not check `e.SpecialKey`. Ctrl+Break (Windows) and Ctrl+\ (SIGQUIT, which .NET on Unix reports as `ControlBreak`) also cancel the run and end with 130. The docs (03:245, 03:264, 01:91) say "Ctrl+C" and "128 + SIGINT".

**FACT:** SIGQUIT sent to the process group gives exit 130 with a `canceled` envelope (2 of 2 runs). Treating both keys the same is a reasonable choice, so either document it or limit the cooperative path to Ctrl+C.

```csharp
private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
{
    // Only Ctrl+C (SIGINT) cancels cooperatively; Ctrl+Break and SIGQUIT keep the default handling.
    e.Cancel = e.SpecialKey == ConsoleSpecialKey.ControlC && HandleCancelKeyPress();
}
```

## Findings — docs/feature-packages/03-feature-management.md

### [Nitpick][Docs] docs/feature-packages/03-feature-management.md:245
**N04.** "so `new` and `init` stop at their next check and roll back what they created in this run" (also line 264: "remove what they created in this run"). This overstates what `init` does.

`RepositoryInitializer.InitializeAsync` (`src/CreativeCoders.ShadeKit.Core/Init/RepositoryInitializer.cs:86-91`) deletes only `.shadekit/` on a failure or cancellation. A `.gitignore`, `CLAUDE.md`, or `AGENTS.md` written or created before the cancellation stays, and the next planning recognises it. The 02 doc (line 65) describes this correctly.

```markdown
- The first Ctrl+C of a run keeps the process alive and cancels the run's token. Every feature command and `init`
  pass the token to the core, so they stop at their next check: `new` removes its staging or feature folder, and
  `init` removes `.shadekit/` (repository files it already changed stay and are recognised by the next `init`,
  package 02).
```

## Findings — tests/CreativeCoders.ShadeKit.Cli.Tests/Output/ConsoleCancellationTests.cs

### [Nitpick][Tests] tests/CreativeCoders.ShadeKit.Cli.Tests/Output/ConsoleCancellationTests.cs:199
**N07.** `Dispose_TokenTakenBefore_KeepsItsState(bool canceled)` branches with `if (canceled)` in its Arrange section. Two facts read more clearly and name the scenario.

```csharp
[Fact]
public void Dispose_UncanceledTokenTakenBefore_StaysUncanceled()
{
    // Arrange
    var cancellation = new ConsoleCancellation();
    var token = cancellation.Token;

    // Act
    cancellation.Dispose();

    // Assert
    token.IsCancellationRequested
        .Should().BeFalse();
}

[Fact]
public void Dispose_CanceledTokenTakenBefore_StaysCanceled()
{
    // Arrange
    var cancellation = new ConsoleCancellation();
    var token = cancellation.Token;
    cancellation.HandleCancelKeyPress();

    // Act
    cancellation.Dispose();

    // Assert
    token.IsCancellationRequested
        .Should().BeTrue();
}
```

## Checked without findings

- **Ctrl+C while git runs:** on Unix, SIGINT also reaches the `git` child. In theory, `WaitForExitAsync` could see the child exit (exit code 130 → `GitUnavailableException`, exit 16) before the handler cancels the token. **FACT:** not reproduced: 13 of 13 runs ended with `canceled`, including runs where the child itself dies from the signal. On Windows the child runs with `CreateNoWindow`, so it does not get the console's Ctrl+C.
- **`ConsoleCancellation`:** subscribe, cancel, and dispose run under one `System.Threading.Lock`. The first press cancels and keeps the process alive; later presses and presses after `Dispose` get the default handling. The token is read in `Program`'s exception filter while the instance is still in its `using` scope. Concurrency is tested (`ConsoleCancellationTests.cs:129`).
- **`CliErrorMapper.MapArgumentError` / `MapCancellation` and the `Program` catch filters:** `TargetInvocationException` is unwrapped in a loop. `OptionCreationFailedException` stays 255 (tested, `ProgramTests.cs:1059`). An `OperationCanceledException` without a canceled token stays unexpected. An envelope that was already written is never duplicated (`CompleteFailedJsonRunAsync`). Plain mode prints one line without a stack trace.
- **`FeatureIdFactory` file reading:** at most limit + 1 bytes, without using `Length`; `FileShare.Read`; a read error → 33; UTF-16/32 BOM, NUL, and invalid UTF-8 → 33; the UTF-8 BOM is counted in the limit and stripped before decoding. The FIFO residual risk is documented (03:65, 03:347).
- **`FeatureStore` staging, move, and rollback:** the rollback of `new` touches only its own staging folder or the feature folder that this run moved into place. A concurrent destination gives `FeatureAlreadyExistsException` and leaves the other folder untouched (tested). Hidden folders can never be valid ids. `KitStore.MoveDirectory` keeps both paths inside the kit root (`GetFullPath`) and refuses an existing file or folder at the destination.
- **`CurrentFeatureOverride`:** only a format problem of the override itself is swallowed (`FilePath` filter); a problem of `config.json` still stops the command. `RespectNullableAnnotations = true` rejects a null `featureId`, so the resolver never sees null.
- **`FeatureTextFormat`, list/show/current warnings, drop data:** the warnings are ordered format first, then feature. `show` now reports feature warnings. `overrideAffected` is set in the preview and after the drop; `overrideCleared` is set only after the delete.
- **Docs:** the exit code tables (01:91-114, 03:251-266), the warning code table (03:270-279), `CLAUDE.md:54`, `docs/feature-packages/README.md:143`, the drop skill, and the template match the code. The exceptions are N04 and N05.

## Tool Output Appendix

### dotnet build
- Skipped (provided `no`). The caller reports 0 warnings.

### dotnet test
- Skipped (provided `no`). The caller reports 1988/1988 passed.

### dotnet format
- Skipped (provided `no`).

### Manual verification
- Built CLI runs in a scratch repository under the session scratchpad: parser errors (F06), SIGINT/SIGQUIT with a fake `git` (F05, N05), title and source rules (F07–F09, N03, N06), unusable override (F10), list/drop warnings and data (F04, F12, F15), drop with a read-only `features/` folder (F13, N01), and a non-executable `git` on `PATH` (N02). No repository file was changed.
