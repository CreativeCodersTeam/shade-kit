using System.Security.Cryptography;
using System.Text;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Initialises a git repository for SHADE kit, as <c>shade-kit init</c> does.
/// </summary>
/// <param name="gitRepositoryLocator">The locator that finds the root of the git repository.</param>
/// <param name="kitStoreFactory">The factory that creates the store of the new <c>.shadekit</c> directory.</param>
/// <param name="shippedTemplateReader">The reader that provides the content of the shipped templates.</param>
/// <param name="gitIgnoreWriter">The writer that adds the local-only files to <c>.gitignore</c>.</param>
/// <param name="instructionSectionWriter">
/// The writer that adds the managed section to <c>CLAUDE.md</c> and <c>AGENTS.md</c>.
/// </param>
/// <param name="engineVersionProvider">The provider of the engine version recorded as kit version.</param>
public sealed class RepositoryInitializer(
    IGitRepositoryLocator gitRepositoryLocator,
    IKitStoreFactory kitStoreFactory,
    IShippedTemplateReader shippedTemplateReader,
    IGitIgnoreWriter gitIgnoreWriter,
    IInstructionSectionWriter instructionSectionWriter,
    IEngineVersionProvider engineVersionProvider)
    : IRepositoryInitializer
{
    private const string GitIgnoreFileName = ".gitignore";

    private const string HashPrefix = "sha256:";

    private const string DefaultProfile = "feature";

    private static readonly string[] InstructionFileNames = ["CLAUDE.md", "AGENTS.md"];

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly IGitRepositoryLocator _gitRepositoryLocator = Ensure.NotNull(gitRepositoryLocator);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly IShippedTemplateReader _shippedTemplateReader = Ensure.NotNull(shippedTemplateReader);

    private readonly IGitIgnoreWriter _gitIgnoreWriter = Ensure.NotNull(gitIgnoreWriter);

    private readonly IInstructionSectionWriter _instructionSectionWriter = Ensure.NotNull(instructionSectionWriter);

    private readonly IEngineVersionProvider _engineVersionProvider = Ensure.NotNull(engineVersionProvider);

    /// <inheritdoc />
    public async Task<RepositoryInitResult> InitializeAsync(string path, CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var repositoryRootPath = _gitRepositoryLocator.GetRepositoryRootPath(path);
        var kitRootPath = Path.Combine(repositoryRootPath, KitStorePaths.RootPath);

        if (Path.Exists(kitRootPath))
        {
            throw new KitAlreadyInitializedException(kitRootPath);
        }

        await ValidateRepositoryFilesAsync(repositoryRootPath, cancellationToken).ConfigureAwait(false);

        var files = new List<InitializedFile>();

        await WriteKitRootAsync(repositoryRootPath, kitRootPath, files, cancellationToken).ConfigureAwait(false);
        await WriteRepositoryFilesAsync(repositoryRootPath, files, cancellationToken).ConfigureAwait(false);

        return new RepositoryInitResult(repositoryRootPath, files);
    }

    /// <summary>
    /// Checks, before anything is written, that <c>.gitignore</c>, <c>CLAUDE.md</c>, and <c>AGENTS.md</c> can be
    /// changed.
    /// </summary>
    private async Task ValidateRepositoryFilesAsync(string repositoryRootPath, CancellationToken cancellationToken)
    {
        await _gitIgnoreWriter.ValidateAsync(Path.Combine(repositoryRootPath, GitIgnoreFileName), cancellationToken)
            .ConfigureAwait(false);

        foreach (var fileName in InstructionFileNames)
        {
            await _instructionSectionWriter.ValidateAsync(Path.Combine(repositoryRootPath, fileName), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task WriteKitRootAsync(string repositoryRootPath, string kitRootPath, List<InitializedFile> files,
        CancellationToken cancellationToken)
    {
        var kitVersion = _engineVersionProvider.Version;

        // The store creates the kit root with its first file.
        var kitStore = _kitStoreFactory.CreateForKitRoot(kitRootPath);

        // A cancellation after the preconditions must not leave a kit root behind that blocks the next init.
        cancellationToken.ThrowIfCancellationRequested();

        await kitStore.WriteJsonAsync(KitStorePaths.ConfigFileName, FormatIds.Config, CreateConfig(kitVersion),
            FormatAccess.Write, cancellationToken).ConfigureAwait(false);
        files.Add(CreatedInKitRoot(KitStorePaths.ConfigFileName));

        await kitStore.WriteJsonAsync(KitStorePaths.GatesFileName, FormatIds.Gates, new GatesConfig(),
            FormatAccess.Write, cancellationToken).ConfigureAwait(false);
        files.Add(CreatedInKitRoot(KitStorePaths.GatesFileName));

        var templatesLock = await WriteTemplatesAsync(repositoryRootPath, kitVersion, files, cancellationToken)
            .ConfigureAwait(false);

        await kitStore.WriteJsonAsync(KitStorePaths.TemplatesLockFileName, FormatIds.TemplatesLock, templatesLock,
            FormatAccess.Write, cancellationToken).ConfigureAwait(false);
        files.Add(CreatedInKitRoot(KitStorePaths.TemplatesLockFileName));

        Directory.CreateDirectory(Path.Combine(kitRootPath, KitStorePaths.FeaturesDirName));
        files.Add(CreatedInKitRoot(KitStorePaths.FeaturesDirName + "/"));
    }

    private async Task WriteRepositoryFilesAsync(string repositoryRootPath, List<InitializedFile> files,
        CancellationToken cancellationToken)
    {
        var gitIgnoreAction = await _gitIgnoreWriter
            .EnsureEntriesAsync(Path.Combine(repositoryRootPath, GitIgnoreFileName), cancellationToken)
            .ConfigureAwait(false);
        AddIfChanged(files, GitIgnoreFileName, gitIgnoreAction);

        foreach (var fileName in InstructionFileNames)
        {
            var action = await _instructionSectionWriter
                .ApplyAsync(Path.Combine(repositoryRootPath, fileName), cancellationToken)
                .ConfigureAwait(false);
            AddIfChanged(files, fileName, action);
        }
    }

    /// <summary>
    /// Writes every shipped template to its repository path, in catalog order.
    /// </summary>
    /// <returns>The lock with one entry per template, sorted ordinally by path.</returns>
    private async Task<TemplatesLock> WriteTemplatesAsync(string repositoryRootPath, string kitVersion,
        List<InitializedFile> files, CancellationToken cancellationToken)
    {
        var lockEntries = new List<TemplatesLockEntry>();

        foreach (var shippedTemplate in ShippedTemplates.All)
        {
            lockEntries.Add(await WriteTemplateAsync(repositoryRootPath, shippedTemplate, kitVersion, cancellationToken)
                .ConfigureAwait(false));
            files.Add(new InitializedFile(shippedTemplate.RepositoryPath, InitializedFileAction.Created));
        }

        return new TemplatesLock { Templates = [.. lockEntries.OrderBy(entry => entry.Path, StringComparer.Ordinal)] };
    }

    /// <summary>
    /// Writes a shipped template to its repository path as UTF-8 without byte order mark.
    /// </summary>
    /// <returns>The lock entry with the SHA-256 hash of the bytes written.</returns>
    private async Task<TemplatesLockEntry> WriteTemplateAsync(string repositoryRootPath,
        ShippedTemplate shippedTemplate, string kitVersion, CancellationToken cancellationToken)
    {
        var bytes = Utf8WithoutBom.GetBytes(_shippedTemplateReader.ReadContent(shippedTemplate));
        var fullPath = Path.Combine(repositoryRootPath,
            shippedTemplate.RepositoryPath.Replace('/', Path.DirectorySeparatorChar));

        // A repository path always has a parent directory below the repository root.
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);

        return new TemplatesLockEntry
        {
            Path = shippedTemplate.RepositoryPath,
            KitVersion = kitVersion,
            Hash = HashPrefix + Convert.ToHexStringLower(SHA256.HashData(bytes))
        };
    }

    private static KitConfig CreateConfig(string kitVersion)
    {
        return new KitConfig
        {
            KitVersion = kitVersion,
            DefaultProfile = DefaultProfile,
            OutdatedFormatOnRead = OutdatedFormatOnReadPolicy.MigrateAndWarn,
            Trackers = new Dictionary<string, string> { ["github.com"] = "gh" },
            Report = ReportPolicy.Ask
        };
    }

    private static InitializedFile CreatedInKitRoot(string relativePath)
    {
        return new InitializedFile($"{KitStorePaths.RootPath}/{relativePath}", InitializedFileAction.Created);
    }

    private static void AddIfChanged(List<InitializedFile> files, string path, InitializedFileAction? action)
    {
        if (action is { } changedAction)
        {
            files.Add(new InitializedFile(path, changedAction));
        }
    }
}
