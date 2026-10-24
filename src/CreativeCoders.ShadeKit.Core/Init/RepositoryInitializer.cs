using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Init.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Initialises a git repository for SHADE kit, as <c>shade-kit init</c> does.
/// </summary>
/// <param name="gitRepositoryLocator">The locator that finds the root of the git repository.</param>
/// <param name="kitStoreFactory">The factory that creates the store of the new <c>.shadekit</c> directory.</param>
/// <param name="shippedTemplateReader">The reader that provides the content of the shipped templates.</param>
/// <param name="gitIgnorePlanner">The planner that computes the local-only entries to add to <c>.gitignore</c>.</param>
/// <param name="instructionSectionPlanner">
/// The planner that computes the managed section to add to <c>CLAUDE.md</c> and <c>AGENTS.md</c>.
/// </param>
/// <param name="engineVersionProvider">The provider of the engine version recorded as kit version.</param>
/// <param name="textFileWriter">
/// The writer that writes <c>.gitignore</c>, <c>CLAUDE.md</c>, and <c>AGENTS.md</c> with the line ending of the
/// repository.
/// </param>
/// <param name="lineEndingResolver">
/// The resolver that is prepared for the line endings of all written files before the first write.
/// </param>
public sealed class RepositoryInitializer(
    IGitRepositoryLocator gitRepositoryLocator,
    IKitStoreFactory kitStoreFactory,
    IShippedTemplateReader shippedTemplateReader,
    IGitIgnorePlanner gitIgnorePlanner,
    IInstructionSectionPlanner instructionSectionPlanner,
    IEngineVersionProvider engineVersionProvider,
    ITextFileWriter textFileWriter,
    ILineEndingResolver lineEndingResolver)
    : IRepositoryInitializer
{
    private const string GitIgnoreFileName = ".gitignore";

    private const string FeaturesDir = KitStorePaths.FeaturesDirName + "/";

    private static readonly string[] InstructionFileNames = ["CLAUDE.md", "AGENTS.md"];

    private readonly IGitRepositoryLocator _gitRepositoryLocator = Ensure.NotNull(gitRepositoryLocator);

    private readonly IKitStoreFactory _kitStoreFactory = Ensure.NotNull(kitStoreFactory);

    private readonly IShippedTemplateReader _shippedTemplateReader = Ensure.NotNull(shippedTemplateReader);

    private readonly IGitIgnorePlanner _gitIgnorePlanner = Ensure.NotNull(gitIgnorePlanner);

    private readonly IInstructionSectionPlanner _instructionSectionPlanner = Ensure.NotNull(instructionSectionPlanner);

    private readonly IEngineVersionProvider _engineVersionProvider = Ensure.NotNull(engineVersionProvider);

    private readonly ITextFileWriter _textFileWriter = Ensure.NotNull(textFileWriter);

    private readonly ILineEndingResolver _lineEndingResolver = Ensure.NotNull(lineEndingResolver);

    /// <inheritdoc />
    public async Task<RepositoryInitResult> InitializeAsync(string path, CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var repositoryRootPath = _gitRepositoryLocator.GetRepositoryRootPath(path);

        // The store creates the kit root with its first file.
        var kitStore = _kitStoreFactory.CreateForRepositoryRoot(repositoryRootPath);

        if (File.Exists(kitStore.RootPath))
        {
            throw new KitRootNotADirectoryException(kitStore.RootPath);
        }

        if (Directory.Exists(kitStore.RootPath))
        {
            throw new KitAlreadyInitializedException(kitStore.RootPath);
        }

        // Planning reads and checks every repository file, so an invalid file stops the initialisation before
        // anything is written.
        var repositoryFileChanges = await PlanRepositoryFilesAsync(repositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        // Every written file is known now, so the git attributes of all of them are read at once instead of once per
        // write.
        await _lineEndingResolver
            .PrepareAsync(repositoryRootPath, GetWrittenFilePaths(repositoryFileChanges), cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var result = new InitResultBuilder();

        try
        {
            await WriteKitRootAsync(kitStore, result, cancellationToken).ConfigureAwait(false);
            await WriteRepositoryFilesAsync(repositoryRootPath, repositoryFileChanges, result, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // A kit root left behind would block the next init. It did not exist before this run, so removing it
            // loses nothing; the repository files that are already written are recognised by the next planning.
            DeleteKitRoot(kitStore.RootPath);
            throw;
        }

        return result.Build(repositoryRootPath);
    }

    // The paths relative to the repository root of the files that InitializeAsync writes.
    private static string[] GetWrittenFilePaths(List<(string FileName, PlannedFileChange Change)> repositoryFileChanges)
    {
        return
        [
            KitStorePaths.GetRepositoryPath(KitStorePaths.ConfigFileName),
            KitStorePaths.GetRepositoryPath(KitStorePaths.GatesFileName),
            .. ShippedTemplates.All.Select(shippedTemplate => shippedTemplate.RepositoryPath),
            KitStorePaths.GetRepositoryPath(KitStorePaths.TemplatesLockFileName),
            .. repositoryFileChanges.Select(change => change.FileName)
        ];
    }

    private static void DeleteKitRoot(string kitRootPath)
    {
        try
        {
            Directory.Delete(kitRootPath, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The kit root stays in place; the next init then reports it as already initialised.
        }
    }

    /// <summary>
    /// Computes the changes of <c>.gitignore</c>, <c>CLAUDE.md</c>, and <c>AGENTS.md</c>, in this order.
    /// </summary>
    /// <returns>The name and change of every file that needs a change.</returns>
    private async Task<List<(string FileName, PlannedFileChange Change)>> PlanRepositoryFilesAsync(
        string repositoryRootPath, CancellationToken cancellationToken)
    {
        var changes = new List<(string FileName, PlannedFileChange Change)>();
        var plannedFilePaths = new HashSet<string>(StringComparer.Ordinal);

        await PlanFileAsync(GitIgnoreFileName, _gitIgnorePlanner.PlanAsync).ConfigureAwait(false);

        foreach (var fileName in InstructionFileNames)
        {
            await PlanFileAsync(fileName, _instructionSectionPlanner.PlanAsync).ConfigureAwait(false);
        }

        return changes;

        async Task PlanFileAsync(string fileName,
            Func<string, CancellationToken, Task<PlannedFileChange?>> planAsync)
        {
            var filePath = Path.Combine(repositoryRootPath, fileName);

            // A link to a file that is already planned, for example AGENTS.md linked to CLAUDE.md, is that file: its
            // change is already planned and would otherwise be reported twice.
            if (!plannedFilePaths.Add(ResolveFinalPath(filePath)))
            {
                return;
            }

            if (await planAsync(filePath, cancellationToken).ConfigureAwait(false) is { } change)
            {
                changes.Add((fileName, change));
            }
        }
    }

    private static string ResolveFinalPath(string filePath)
    {
        // ResolveLinkTarget throws for a missing path and returns null for a path that is not a link.
        return Path.Exists(filePath)
            ? File.ResolveLinkTarget(filePath, returnFinalTarget: true)?.FullName ?? filePath
            : filePath;
    }

    private async Task WriteKitRootAsync(IKitStore kitStore, InitResultBuilder result,
        CancellationToken cancellationToken)
    {
        var kitVersion = _engineVersionProvider.Version;

        // Every other setting keeps the default of KitConfig.
        var config = new KitConfig { KitVersion = kitVersion };

        await WriteDocumentAsync(KitStorePaths.ConfigFileName, FormatIds.Config, config).ConfigureAwait(false);

        await WriteDocumentAsync(KitStorePaths.GatesFileName, FormatIds.Gates, new GatesConfig())
            .ConfigureAwait(false);

        var templatesLock = await WriteTemplatesAsync(kitStore, kitVersion, result, cancellationToken)
            .ConfigureAwait(false);

        await WriteDocumentAsync(KitStorePaths.TemplatesLockFileName, FormatIds.TemplatesLock, templatesLock)
            .ConfigureAwait(false);

        kitStore.CreateDirectory(KitStorePaths.FeaturesDirName);
        result.Add(CreatedInKitRoot(FeaturesDir), []);

        async Task WriteDocumentAsync<T>(string relativePath, string formatId, T document)
            where T : VersionedDocument
        {
            var writeWarnings = await kitStore
                .WriteJsonAsync(relativePath, formatId, document, FormatAccess.Write,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            result.Add(CreatedInKitRoot(relativePath), writeWarnings);
        }
    }

    /// <summary>
    /// Writes every shipped template as UTF-8 without byte order mark, with the line ending of the repository, in
    /// catalog order.
    /// </summary>
    /// <returns>The lock with one entry per template, sorted ordinally by path.</returns>
    private async Task<TemplatesLock> WriteTemplatesAsync(IKitStore kitStore, string kitVersion,
        InitResultBuilder result, CancellationToken cancellationToken)
    {
        var lockEntries = new List<TemplatesLockEntry>();

        foreach (var shippedTemplate in ShippedTemplates.All)
        {
            var content = TextFileContent.FromText(_shippedTemplateReader.ReadContent(shippedTemplate));

            var writeWarnings = await kitStore
                .WriteFileAsync(shippedTemplate.KitPath, content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            result.Add(CreatedInKitRoot(shippedTemplate.KitPath), writeWarnings);

            lockEntries.Add(new TemplatesLockEntry
            {
                Path = shippedTemplate.RepositoryPath,
                KitVersion = kitVersion,
                Hash = TemplatesLockEntry.ComputeHash(content)
            });
        }

        return new TemplatesLock { Templates = [.. lockEntries.OrderBy(entry => entry.Path, StringComparer.Ordinal)] };
    }

    private async Task WriteRepositoryFilesAsync(string repositoryRootPath,
        List<(string FileName, PlannedFileChange Change)> changes, InitResultBuilder result,
        CancellationToken cancellationToken)
    {
        foreach (var (fileName, change) in changes)
        {
            var writeWarnings = await _textFileWriter
                .WriteAsync(repositoryRootPath, fileName, change.Content, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            result.Add(new InitializedFile(fileName, change.Action), writeWarnings);
        }
    }

    private static InitializedFile CreatedInKitRoot(string relativePath)
    {
        return new InitializedFile(KitStorePaths.GetRepositoryPath(relativePath), InitializedFileAction.Created);
    }

    // Collects the written files and the warnings of their writes in write order; the kit root is written first, so
    // its warnings come first.
    private sealed class InitResultBuilder
    {
        private readonly List<InitializedFile> _files = new List<InitializedFile>();

        private readonly List<KitWarning> _warnings = new List<KitWarning>();

        public void Add(InitializedFile file, IReadOnlyList<KitWarning> warnings)
        {
            _files.Add(file);
            _warnings.AddRange(warnings);
        }

        public RepositoryInitResult Build(string repositoryRootPath)
        {
            return new RepositoryInitResult(repositoryRootPath, _files, KitWarningOrder.Arrange(_warnings));
        }
    }
}
