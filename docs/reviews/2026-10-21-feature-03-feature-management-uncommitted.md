# .NET Code Review — feature/03-feature-management (uncommitted)

**Date:** 2026-10-21
**Mode:** uncommitted (provided): working tree vs. HEAD, including untracked files
**Detected SDK:** 10.0.402
**Target Framework(s):** net10.0
**Version origin:** repo:src/CreativeCoders.ShadeKit.Core/CreativeCoders.ShadeKit.Core.csproj (all five project files target net10.0; SDK pinned in global.json)
**Checklist:** review-checklist-net10.md (+ security, performance, architecture, code-quality)
**Tools run:** build=N · format=N · test=N (all provided as `no`; the caller reports build and the full test suite green: 0 warnings, 1640/1640 tests)
**Report language:** English (provided)
**Exclusions:** .gitignore, *.min.js, wwwroot/lib/**
**Review strategy:** chunked (auto-selected, non-interactive; large-diff gate triggered: 134 files > 50, 14864 LOC > 2000)
**Diff size:** 134 files, 14864 changed LOC (including Markdown docs and skills; about 110 C# files)
**Review context:** requirement `docs/feature-packages/03-feature-management.md`; agent contract `docs/feature-packages/01-store-foundation-and-format-versioning.md`; the user's binding decisions of 2026-10-20 (`fp03-decisions.md`) and the implementation facts (`fp03-implementation-facts.md`). Decided behaviour is not reported as a defect. Where a decision carries a risk, the finding says so.
**Area tags:** besides the tags of the severity taxonomy, this report uses `Docs` for Markdown statements that contradict the code or are ambiguous, because the caller asked for it.

## Executive Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| Major | 1 |
| Minor | 10 |
| Suggestion | 2 |
| Nitpick | 6 |

Seven of the 19 findings are items the caller already knows about (F02, F06, F07, F11, F15, F18, and part of F10). Each is labelled **Known**.

**Top risks:**
1. **F01:** on Windows, `GitCommandRunner` starts `git` by bare name. `CreateProcess` searches the parent's current directory before `PATH`, and that directory is usually the repository root. A `git.exe` committed to a cloned repository would run when the user calls `feature new`, `current`, or `set`. Windows is a shipped target (`win-x64`, `win-arm64`).
2. **F12:** `show` and `current` return the `id` from `feature.json` but the `paths` of the folder, while `list` uses the folder name. When a folder is copied or renamed, the JSON contract gives an id that `set`, `show`, and `drop` do not resolve to the same folder.
3. **F04:** in JSON mode, the drop preview always returns `overrideCleared: false`, even when the override would be cleared. The plain preview does show this. The agent (skill `shade-kit-drop`, step 4) therefore cannot tell the human that the current-feature selection will go away.

**Overall:** A thorough, well-structured change. Ids are validated before any path is built. `KitStore` checks every path against the kit root, `GetFiles` and `DeleteDirectory` do not follow directory links (tested), and `git` runs without a shell through `ArgumentList`. The `new` order of work and its rollback match the decisions. Every command keeps the one-envelope contract and is covered by unit tests, store tests, and end-to-end tests with faked git. All of FP03-AC1 to AC11 carry `Requirement` traits. Apart from the Windows executable lookup (F01), the findings are edge cases, contract refinements, and documentation precision. F01 should be fixed before a Windows release.

**Verified facts behind the findings** (built CLI `src/CreativeCoders.ShadeKit.Cli/bin/Debug/net10.0/shade-kit.dll`, newer than every `src/**/*.cs`, run in a scratch repository with a local git identity):
- **FACT:** `feature new --source req16.txt --json` with a UTF-16LE file (`FF FE` BOM, content `# Login`) returns `ok: true` and exit 0. The title falls back to the file name (`req16`), and `request.md` contains `EF BF BD EF BF BD` (two U+FFFD) followed by NUL bytes (`# \0L\0o\0g\0i\0n\0`). (F08)
- **FACT:** After `cp -R .shadekit/features/20261021-0853-req16 .shadekit/features/copy-of-req`, `feature show copy-of-req --json` returns `id: 20261021-0853-req16` with `paths.featureDir: .shadekit/features/copy-of-req/`. `feature list` prints `copy-of-req` as the id. (F12)
- **FACT:** With the override pointing to the feature, `feature drop <id> --json` returns `overrideCleared: false`. Plain `feature drop <id> </dev/null` prints "The local override points to this feature and would be cleared." (F04)
- **FACT:** `src/` contains no `Console.CancelKeyPress`, `PosixSignalRegistration`, or `CancellationTokenSource`. `CreativeCoders.Cli.Hosting.dll` 6.10.0 contains none of the strings `CancelKeyPress`, `PosixSignal`, or `CancellationToken`, and the feature commands call `IFeatureStore` without a token. (F05)
- **FACT:** `GetIssueNumber` reads only the last non-empty path segment (`FeatureIdFactory.cs:167`), so `/issues/142/comments` stops with exit 33. (F16)
- **Unverified (no Windows host):** that .NET's `Process.Start` on Windows passes `lpApplicationName = null` to `CreateProcess`, which makes the documented `CreateProcess` search order apply. This is from the runtime source as I know it; this review did not reproduce it. (F01)

## Findings — src/CreativeCoders.ShadeKit.Core/Git/GitCommandRunner.cs

### [Major][Security] src/CreativeCoders.ShadeKit.Core/Git/GitCommandRunner.cs:68
**F01.** `git` is started by bare name. On Windows this can run a `git.exe` from the current directory (the repository) instead of the one on `PATH` (OWASP A08, binary planting).

With `UseShellExecute = false`, .NET builds a command line and calls `CreateProcess` without an application name (**Unverified** in this review, see above). `CreateProcess` then searches four places in order: the application directory, the **current directory of the parent process**, the system directories, and finally `PATH`. `shade-kit` normally runs with the repository root as its current directory. `WorkingDirectory` only affects the child process, not this search. So a cloned repository that contains `git.exe` (or `git.com` / `git.bat`, depending on `PATHEXT` handling) gets code execution as soon as `feature new`, `current`, or `set` runs. The `S4036` suppression says "looked up on PATH by design", which is not accurate on Windows.

Resolve the executable to an absolute path yourself. Use only fully qualified `PATH` entries, so the current directory is never searched, then start that path:

```csharp
private static string ResolveGitExecutable(IReadOnlyList<string> arguments)
{
    string[] names = OperatingSystem.IsWindows() ? ["git.exe"] : ["git"];
    var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

    foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        // Relative entries (".", "bin") would resolve against the current directory, i.e. the repository.
        if (!Path.IsPathFullyQualified(directory))
        {
            continue;
        }

        foreach (var name in names)
        {
            var candidate = Path.Combine(directory, name);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    throw new GitUnavailableException(arguments, "git was not found in any absolute directory of PATH.");
}

// CreateStartInfo
var startInfo = new ProcessStartInfo(ResolveGitExecutable(arguments)) { /* unchanged */ };
```

### [Minor][Tests] src/CreativeCoders.ShadeKit.Core/Git/GitCommandRunner.cs:23
**F02 (Known, decided).** `GitCommandRunner` has no tests. Its start failure, exit-code passthrough, UTF-8 decoding, and kill-on-cancel paths were checked only by a manual smoke run.

The decision rules out real `git` in tests. The risky logic around the process (cancellation, the start failure that maps to `GitUnavailableException`, and the executable resolution from F01) can still be tested without git. Inject the executable name or path and point it at a file that does not exist (start failure) or at a tiny test-host script. If that is not wanted, keep this as an accepted gap and tag the smoke run as its evidence.

```csharp
// Test seam without real git: the executable is injectable (internal ctor), default stays "git".
internal GitCommandRunner(string executable) => _executable = Ensure.IsNotNullOrWhitespace(executable);

[Fact]
public async Task RunAsync_ExecutableMissing_ThrowsGitUnavailableException()
{
    var runner = new GitCommandRunner(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
    var act = () => runner.RunAsync(Path.GetTempPath(), ["--version"], TestContext.Current.CancellationToken);
    await act.Should().ThrowAsync<GitUnavailableException>();
}
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Git/GitCommandRunner.cs:50
**F03.** On cancellation, the two `ReadToEndAsync` tasks are left behind and the process is then disposed. Their later faults go unobserved.

This is harmless today, but it can raise `TaskScheduler.UnobservedTaskException` noise. Observe them before you rethrow.

```csharp
catch (OperationCanceledException)
{
    KillProcessTree(process);
    await Task.WhenAll(standardOutputTask, standardErrorTask)
        .ContinueWith(static _ => { }, TaskScheduler.Default).ConfigureAwait(false);
    throw;
}
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Commands/Feature/Drop/FeatureDropCommand.cs

### [Minor][Architecture] src/CreativeCoders.ShadeKit.Cli/Commands/Feature/Drop/FeatureDropCommand.cs:107
**F04.** The JSON preview hard-codes `overrideCleared: false` and drops `FeatureDropPreview.OverrideAffected`. An agent cannot learn what the plain preview tells a human: that the override would be cleared.

FP03-AC8 says the preview "returns what it would delete". The skill `shade-kit-drop` (step 4) has to show the preview to the human before asking. With the current shape, `overrideCleared` means "was cleared" after `--yes` and always `false` before it, so the preview carries no information about the override. Either set `overrideCleared` to `OverrideAffected` in the preview (it is unambiguous together with `dropped: false`), or add a separate field and document it in FP03 "Output" and in the skill.

```csharp
if (options.Json)
{
    // dropped: false marks the preview; overrideCleared tells what --yes would clear.
    _jsonWriter.Write(CliJsonEnvelope.Success(
        new FeatureDropCommandData(preview.FeatureId, false, preview.Files, preview.OverrideAffected)));

    return CommandResult.Success;
}
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Commands/Feature/New/FeatureNewCommand.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Commands/Feature/New/FeatureNewCommand.cs:87
**F05.** The rollback "if `new` is cancelled" cannot happen from the CLI. No command passes a cancellation token, and Ctrl+C ends the process without running `catch` or `finally`, so a half-written feature folder stays behind.

`docs/feature-packages/03-feature-management.md:144` and `IFeatureStore.CreateAsync` (`IFeatureStore.cs:39`) promise that a cancelled `new` removes the folder. In the CLI, SIGINT (Ctrl+C) uses the runtime's default handling: the process ends and the rollback in `FeatureStore.CreateAsync` never runs. A leftover folder without `request.md` or without the override then blocks the id with exit 30. Either wire Ctrl+C to a token that reaches the core (the behaviour the docs describe), or narrow the docs to "cancelled through the API token" and accept the gap.

```csharp
// Program.RunAsync: turn Ctrl+C into cooperative cancellation for the run.
using var cancellation = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
Console.CancelKeyPress += onCancel;
try { /* host run; commands read the token from an injected ICliCancellation service */ }
finally { Console.CancelKeyPress -= onCancel; }

// FeatureNewCommand
() => _featureStore.CreateAsync(_kitStoreFactory.Create(), request, _cancellation.Token)
```

## Findings — src/CreativeCoders.ShadeKit.Cli/Program.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Cli/Program.cs:109
**F06 (Known).** A missing required argument (`feature show` without an id, `feature new` without `--source`) ends with exit 255 (`host-error`) and a full stack trace on standard error. A usage error is reported like a crash.

JSON mode still writes exactly one failure envelope, so the agent contract holds. But the error code `host-error` hides a predictable user error, and plain mode prints internals. Map the parser's `RequiredArgumentMissingException` (and similar parser exceptions) to a usage exit code with a short message. This affects every command with a required option, not only package 03.

```csharp
catch (Exception e) when (Unwrap(e) is RequiredArgumentMissingException missing)
{
    const string message = "A required argument is missing. Run 'shade-kit <command> --help' for the usage.";
    await TryWriteErrorAsync(error, $"{message} ({missing.Message})").ConfigureAwait(false);
    return jsonMode
        ? await CompleteFailedJsonRunAsync(jsonWriter, error, message).ConfigureAwait(false) // or a new usage code
        : ShadeKitCliExitCodes.HostError; // or a new usage exit code
}
```

## Findings — src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs:95
**F07 (Known).** An unreadable `--source` file ends with exit 255. The file exists but cannot be read (permissions, a lock on Windows), and the `IOException` / `UnauthorizedAccessException` is not mapped.

Wrap the read and throw `InvalidFeatureSourceException` (exit 33), whose message already tells the user what to pass. Then nothing is written (the read happens before the folder is created) and the error code is stable. The same guard also limits the size, so `--source /dev/zero` or a huge file cannot exhaust memory. On Unix, `File.Exists` is true for device files and FIFOs.

```csharp
private static async Task<byte[]> ReadSourceFileAsync(string source, string filePath, CancellationToken ct)
{
    const long MaxSourceBytes = 1024 * 1024;

    try
    {
        var info = new FileInfo(filePath);
        if (info.Length > MaxSourceBytes)
        {
            throw new InvalidFeatureSourceException(source, $"The file is larger than {MaxSourceBytes} bytes.");
        }

        return await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        throw new InvalidFeatureSourceException(source, $"The file cannot be read: {e.Message}");
    }
}
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs:96
**F08.** A file source that is not UTF-8 is decoded silently with replacement characters. A UTF-16 file (what Windows PowerShell 5.1 `>` writes) creates a feature with a fallback title and a `request.md` full of U+FFFD and NUL bytes, with `ok: true`.

**FACT:** reproduced with the built CLI (see the summary). The decision says "decoded as UTF-8", but the kit already has a pattern for unsupported encodings (`UnsupportedFileEncodingException` in `Core/Files`). The input is the "original input, unchanged" that later phases rely on, so a silent corruption is worse than a clear stop. Detect UTF-16/UTF-32 BOMs and invalid UTF-8, and stop with exit 33 before anything is written. Alternatively, decode UTF-16 with a BOM correctly.

```csharp
private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

private static string DecodeUtf8(string source, byte[] content)
{
    var bytes = content.AsSpan();
    if (bytes.StartsWith(Utf8Bom)) bytes = bytes[Utf8Bom.Length..];

    try
    {
        return StrictUtf8.GetString(bytes);
    }
    catch (DecoderFallbackException)
    {
        throw new InvalidFeatureSourceException(source,
            "The file is not UTF-8 encoded. Save it as UTF-8 and run the command again.");
    }
}
```

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureIdFactory.cs:44
**F09.** An explicit `--title` is only trimmed. Line breaks and other control characters go unchanged into `feature.json`, the `# Request:` heading of `request.md`, and the one-line-per-feature plain output of `feature list`.

Titles from text and file sources are single lines by construction. Only `--title` can carry `\n`. Collapse whitespace runs to one space, or reject control characters with `InvalidFeatureTitleException`.

```csharp
var explicitTitle = string.IsNullOrWhiteSpace(title)
    ? null
    : WhitespaceRunRegex().Replace(title, " ").Trim(); // [GeneratedRegex(@"\s+")]
```

## Findings — src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs

### [Minor][Architecture] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:82
**F10 (partly Known).** A broken or too-new `current-feature.json` blocks `feature new` (format pre-check), `feature current` (even when the branch alone would resolve), and `feature set`. No CLI command can repair it. `drop` leaves it alone by design.

The open points list only the `set` part. Together, the three commands make a local, gitignored, purely derived file a hard stop for the main workflow, and the only fix is to delete it by hand. The exit-22 message names the path, which helps. Consider letting `feature set` replace an override that cannot be read (it holds no other data), or having the resolver ignore a broken override with a warning when the branch resolves. At least add the manual fix to the message or the docs.

```csharp
// SetCurrentAsync: the override is a pure pointer, so an unreadable one may be replaced.
try
{
    await WriteOverrideAsync(store, featureId, cancellationToken).ConfigureAwait(false);
}
catch (KitFileFormatException)
{
    store.DeleteFile(CurrentFeatureFilePath);
    await WriteOverrideAsync(store, featureId, cancellationToken).ConfigureAwait(false);
    warnings.Add(FeatureWarning.CurrentFeatureReplaced(featureId)); // new warning kind
}
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:103
**F11 (Known, aggravated).** `new` is not atomic. `CreateDirectory` succeeds when the folder exists, so two concurrent runs with the same id both pass the existence check. If one of them then fails, its rollback deletes the folder, including the feature the other run just wrote successfully.

The known gap is "not atomic". The aggravation is that the best-effort rollback can delete data that does not belong to this run. Create the folder exclusively. For example, build it under a unique temporary name and `Directory.Move` it to the final id (the move fails if the target exists). Only roll back what this run created.

```csharp
var stagingDir = $"{KitStorePaths.FeaturesDirName}/.new-{Guid.NewGuid():N}";
store.CreateDirectory(stagingDir);
try
{
    // write feature.json and request.md into stagingDir ...
    store.MoveDirectory(stagingDir, featureDir); // new IKitStore member; throws IOException if featureDir exists
}
catch
{
    DeleteFeatureDirBestEffort(store, stagingDir); // never touches featureDir
    throw;
}
await WriteOverrideAsync(store, identity.Id, cancellationToken).ConfigureAwait(false);
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:168
**F12.** The feature id is taken from two different sources. `ShowAsync`, `GetCurrentAsync` (line 186), and `CreateAsync` return `FeatureDocument.Id`, while `ListAsync` (line 151), the paths, `set`, and `drop` use the folder name. A copied or renamed folder gives JSON whose `id` and `paths` disagree.

**FACT:** reproduced (see the summary): `show copy-of-req` returns `id: 20261021-0853-req16` with `featureDir: .shadekit/features/copy-of-req/`. Under the rule "a feature exists if and only if its folder exists", the folder name is the identity. Either report the folder name everywhere, or validate `document.Id == folder` when reading and stop with `InvalidKitFileException` (exit 22). Add a warning if it is merely reported.

```csharp
private static async Task<VersionedReadResult<FeatureDocument>> ReadFeatureAsync(IKitStore store,
    string featureId, CancellationToken cancellationToken)
{
    var featureFilePath = GetFeatureFilePath(featureId);
    var result = await store.ReadJsonAsync<FeatureDocument>(featureFilePath, FormatIds.Feature,
                     FormatAccess.Read, cancellationToken).ConfigureAwait(false)
                 ?? throw new InvalidKitFileException(KitStorePaths.GetRepositoryPath(featureFilePath),
                     "The file is missing, although the feature folder exists.");

    return string.Equals(result.Document.Id, featureId, StringComparison.Ordinal)
        ? result
        : throw new InvalidKitFileException(KitStorePaths.GetRepositoryPath(featureFilePath),
            $"The id '{result.Document.Id}' does not match the feature folder '{featureId}'.");
}
```

### [Minor][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:238
**F13.** `drop` deletes the folder in place with a recursive `Directory.Delete`. If that fails partway (a file locked by an editor or indexer on Windows, or a permission problem), part of the feature is gone, the override is untouched, and the command ends with exit 255.

The JSON envelope is still written (`host-error`), but the feature is left half-deleted and `feature show` then fails with exit 22. Rename the folder first, which is atomic within the same directory and fails cleanly when something is locked, then delete the renamed folder and the override. A failure while deleting the renamed folder then leaves only an invisible leftover, not a broken feature.

```csharp
var trashDir = $"{KitStorePaths.FeaturesDirName}/.dropped-{featureId}-{Guid.NewGuid():N}";
store.MoveDirectory(GetFeatureDir(featureId), trashDir); // atomic; nothing deleted yet if it fails

if (overrideAffected)
{
    store.DeleteFile(CurrentFeatureFilePath);
}

store.DeleteDirectory(trashDir); // a failure here leaves only the hidden leftover
```

### [Suggestion][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:248
**F14.** The same piece of knowledge is written down in several places: the repository root as `Path.GetDirectoryName(store.RootPath)` (here and `CurrentFeatureResolver.cs:89`), the override path constant (here, `CurrentFeatureResolver.cs:15`, and `CliErrorMapper.MapWarning`), the feature dir `features/<id>` (here, the resolver, `FeaturePaths`, and `CliErrorMapper`), the BOM-stripping UTF-8 decode (`FeatureIdFactory.cs:174` and `FeatureRequestTemplate.cs:37`), and the `createdAt` format (`FeatureRequestTemplate.cs:28` and `FeatureShowCommand`).

Each copy is small, but a change to the layout (for example the separate spec store, concept §4.4) would have to touch all of them. Move the paths into `KitStorePaths` and the decode into one internal helper.

```csharp
public static class KitStorePaths
{
    public const string CurrentFeatureFilePath = $"{FeaturesDirName}/{CurrentFeatureFileName}";

    public static string GetFeatureDir(string featureId) => $"{FeaturesDirName}/{featureId}";
}

internal static class Utf8Text
{
    public static string DecodeWithoutBom(ReadOnlySpan<byte> bytes) => /* shared implementation */;
}
```

### [Nitpick][Code-Quality] src/CreativeCoders.ShadeKit.Core/Features/FeatureStore.cs:137
**F15 (Known).** `ListAsync` lists folders whose names are not valid ids, using the folder name as the id. None of these can be used with `set`, `show`, or `drop` (exit 32).

Skip them with a warning, the same way a missing `feature.json` is handled. The path is safe either way, because the names come from the file system.

```csharp
if (!FeatureIds.IsValid(featureName))
{
    warnings.Add(FeatureWarning.InvalidFeatureFolder(featureName)); // new kind, e.g. "feature-folder-invalid"
    continue;
}
```

## Findings — docs/feature-packages/03-feature-management.md

### [Nitpick][Docs] docs/feature-packages/03-feature-management.md:50
**F16.** "The issue number is the last path segment that consists only of ASCII digits" can be read as "the last of the digit-only segments". The code requires the last segment itself to be digits (`/issues/142/comments` → exit 33). The mapper message says it correctly ("the last segment of the URL path is not an issue number").

```markdown
The issue number is the last path segment, which must consist only of ASCII digits (`/issues/142`, `/pull/142`,
`/-/issues/142`; `/issues/142/comments` has no issue number); query and fragment are ignored.
```

## Findings — skills/shade-kit-drop/SKILL.md

### [Nitpick][Docs] skills/shade-kit-drop/SKILL.md:3
**F17.** The frontmatter `description` says the skill "clears the current feature". The CLI clears only the local override, and only if it points to the dropped feature. Step 5 of the same file already says this correctly.

```markdown
description: SHADE kit skill to drop a feature from any phase. Deletes the feature after explicit confirmation and clears the local override if it points to it. Use when the user wants to drop, cancel, abandon, or delete a SHADE kit feature.
```

## Findings — src/CreativeCoders.ShadeKit.Core/Templates/Resources/request.template.md

### [Nitpick][Docs] src/CreativeCoders.ShadeKit.Core/Templates/Resources/request.template.md:8
**F18 (Known, Open in docs).** The guidance comment names the source kinds `issue | file | text | discussion`, while `feature new` writes `text`, `url`, or `file`. It is tracked as **Open** for package 09.

```markdown
- Source kind: url | file | text (written by `feature new`). Source reference: the URL, the repository-relative file path, or "n/a" for text.
```

## Findings — tests/CreativeCoders.ShadeKit.Core.Tests/Features/FeatureStoreTests.cs

### [Nitpick][Tests] tests/CreativeCoders.ShadeKit.Core.Tests/Features/FeatureStoreTests.cs:980
**F19.** `FeatureIdMethods_NullFeatureId_ThrowArgumentNullException` loops over four calls inside the Assert section. The first failure hides the rest, and the test name does not say which method broke.

```csharp
public static TheoryData<string> FeatureIdMethods => ["show", "set", "preview-drop", "drop"];

[Theory]
[MemberData(nameof(FeatureIdMethods))]
public async Task FeatureIdMethod_NullFeatureId_ThrowsArgumentNullException(string method)
{
    // Arrange
    var featureStore = _context.CreateFeatureStore();
    var store = _context.CreateStore();
    var ct = TestContext.Current.CancellationToken;
    Func<Task> act = method switch
    {
        "show" => () => featureStore.ShowAsync(store, null!, ct),
        "set" => () => featureStore.SetCurrentAsync(store, null!, ct),
        "preview-drop" => () => featureStore.PreviewDropAsync(store, null!, ct),
        _ => () => featureStore.DropAsync(store, null!, ct)
    };

    // Act / Assert
    await act.Should().ThrowAsync<ArgumentNullException>();
}
```

## Checked without findings

- **Path traversal:** ids from `set`, `show`, `drop`, the branch, and the override are checked against `^[a-z0-9]+(-[a-z0-9]+)*\z` before any path is built. `KitStore.GetFullPath` rejects paths outside the kit root. A tracker prefix from `config.json` that makes an invalid id is caught by `CreateIdentity`. `DeleteDirectory` on a link removes only the link, and `GetFiles` does not follow directory links (both tested).
- **Process execution:** no shell, arguments go through `ArgumentList`, the start failure maps to exit 16, and an unexpected exit code maps to exit 16 with stderr. The commands used (`symbolic-ref`, `config`) do not read the index, so repository-controlled `core.fsmonitor` hooks are not triggered.
- **Agent contract:** every command goes through `CliCommandAbort` and `FeatureCommandOutput.Write`. Unmapped exceptions still produce exactly one `host-error` envelope (`Program.RunAsync`). Warnings are ordered format first, then feature. `CliErrorMapper` is the only place that maps errors (guard test FP01-AC8).
- **DI:** all new services are stateless singletons registered with `TryAdd*`. The end-to-end seam removes overrides of `IAnsiConsole` and `ICliJsonWriter`, so tests cannot break the one-envelope contract.
- **Tests:** no test starts real `git`. Tests that change the current directory are in the `DisableParallelization` collection. Every FP03-AC1 to AC11 has `[Trait("Requirement", "FP03-ACn")]` tests in the files the docs name. The `new` rollback is covered for template read, override write, cancellation, and cleanup failure.
- **Docs vs. code:** exit/error/warning code tables (01, 03), JSON `data` shapes, plain output lines, the order of work for `new`, git commands, "`list`, `show`, and `drop` do not call git", and the README/CLAUDE.md command lists all match the code. The exceptions are F05, F16, F17, and F18.

## Tool Output Appendix

### dotnet build
- Skipped (provided `no`). The caller reports 0 warnings.

### dotnet test
- Skipped (provided `no`). The caller reports 1640/1640 passed.

### dotnet format
- Skipped (provided `no`).

### Manual verification
- Built CLI runs in a scratch repository under the session scratchpad: UTF-16 file source (F08), copied feature folder with `show` / `list` (F12), and JSON vs. plain drop preview (F04). No repository file was changed.
