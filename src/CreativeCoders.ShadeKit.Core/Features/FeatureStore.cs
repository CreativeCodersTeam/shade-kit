using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Store.Exceptions;
using CreativeCoders.ShadeKit.Core.Templates;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Creates, lists, reads, selects, and drops the features of a SHADE kit store.
/// </summary>
/// <param name="featureIdFactory">The factory that builds the id and the identity of a new feature.</param>
/// <param name="currentFeatureResolver">The resolver of the current feature.</param>
/// <param name="gitRepositoryInfo">The source of the git identity recorded as creator of a new feature.</param>
/// <param name="shippedTemplateReader">
/// The reader of the shipped <c>request.md</c> template, used when the repository has no template of its own.
/// </param>
public sealed class FeatureStore(
    IFeatureIdFactory featureIdFactory,
    ICurrentFeatureResolver currentFeatureResolver,
    IGitRepositoryInfo gitRepositoryInfo,
    IShippedTemplateReader shippedTemplateReader)
    : IFeatureStore
{
    private readonly IFeatureIdFactory _featureIdFactory = Ensure.NotNull(featureIdFactory);

    private readonly ICurrentFeatureResolver _currentFeatureResolver = Ensure.NotNull(currentFeatureResolver);

    private readonly IGitRepositoryInfo _gitRepositoryInfo = Ensure.NotNull(gitRepositoryInfo);

    private readonly IShippedTemplateReader _shippedTemplateReader = Ensure.NotNull(shippedTemplateReader);

    /// <inheritdoc />
    public async Task<NewFeatureResult> CreateAsync(IKitStore store, NewFeatureRequest request,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(request);

        var repositoryRootPath = store.RepositoryRootPath;

        var configResult = await store
            .ReadJsonAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config, FormatAccess.Read,
                cancellationToken)
            .ConfigureAwait(false);
        var config = configResult?.Document ?? new KitConfig();

        var profile = request.Profile ?? config.DefaultProfile;

        if (!WorkflowProfiles.IsKnown(profile))
        {
            throw new UnknownProfileException(profile);
        }

        var identity = await _featureIdFactory
            .CreateAsync(request.Source, request.Title, request.WorkingDirectory, config.Trackers, repositoryRootPath,
                cancellationToken)
            .ConfigureAwait(false);

        if (store.FeatureExists(identity.Id))
        {
            throw new FeatureAlreadyExistsException(identity.Id);
        }

        // The override is written last and replaces an override that cannot be used because of its format, so only an
        // override that cannot be read at all would stop that write. It is read now as for a write, which never reads
        // config.json, so such an override stops the command before anything is written, and a format problem does not.
        try
        {
            await store
                .ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath, FormatIds.CurrentFeature,
                    FormatAccess.Write, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (KitFileFormatException)
        {
            // The override is replaced when it is written.
        }

        var creator = await _gitRepositoryInfo.GetIdentityAsync(repositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var feature = new FeatureDocument
        {
            Id = identity.Id,
            Title = identity.Title,
            Profile = profile,
            Source = identity.Source,
            CreatedAt = identity.CreatedAt,
            CreatedBy = new FeatureCreator { Name = creator.Name, Email = creator.Email },
            Status = FeatureStatus.Active
        };

        // The three files are known now, so their git attributes are read at once instead of once per write; the
        // feature files are prepared with the paths of the feature folder, for which their line endings are
        // determined.
        await store
            .PrepareLineEndingsAsync(
            [
                KitStorePaths.GetFeatureFilePath(feature.Id),
                $"{KitStorePaths.GetFeatureDirPath(feature.Id)}/{KitStorePaths.RequestFileName}",
                KitStorePaths.CurrentFeatureFilePath
            ], cancellationToken)
            .ConfigureAwait(false);

        var featureDirWarnings = await CreateFeatureDirAsync(store, feature, identity.OriginalInput, cancellationToken)
            .ConfigureAwait(false);

        var overrideWarnings = await WriteOverrideOrRemoveFeatureAsync(store, identity.Id, cancellationToken)
            .ConfigureAwait(false);

        return new NewFeatureResult(feature,
            KitWarningOrder.Arrange([.. configResult?.Warnings ?? [], .. featureDirWarnings, .. overrideWarnings]));
    }

    /// <inheritdoc />
    public async Task<FeatureListResult> ListAsync(IKitStore store, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        List<FeatureSummary> features = [];
        List<KitWarning> warnings = [];

        // The names come from the file system, so each one is a single path segment inside the features folder.
        foreach (var featureName in store.GetFeatureNames().Order(StringComparer.Ordinal))
        {
            // Hidden folders are the staging and dropping folders of new and dropped features, also those that a
            // failed run left behind.
            if (KitStoreDirectoryExtensions.IsHiddenName(featureName))
            {
                continue;
            }

            if (!FeatureIds.IsValid(featureName))
            {
                warnings.Add(new FeatureFolderInvalidWarning(featureName));

                continue;
            }

            var result = await store
                .ReadJsonAsync<FeatureDocument>(KitStorePaths.GetFeatureFilePath(featureName), FormatIds.Feature,
                    FormatAccess.Read, cancellationToken)
                .ConfigureAwait(false);

            if (result is null)
            {
                warnings.Add(new FeatureFileMissingWarning(featureName));

                continue;
            }

            warnings.AddRange(result.Warnings);
            warnings.AddRange(CheckFeatureId(featureName, result.Document));
            features.Add(new FeatureSummary(featureName, result.Document.Title, result.Document.Profile,
                result.Document.Status));
        }

        return new FeatureListResult(features, KitWarningOrder.Arrange(warnings));
    }

    /// <inheritdoc />
    public async Task<FeatureShowResult> ShowAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        EnsureFeatureExists(store, featureId);

        var result = await ReadFeatureAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        return new FeatureShowResult(featureId, result.Document,
            KitWarningOrder.Arrange([.. result.Warnings, .. CheckFeatureId(featureId, result.Document)]));
    }

    /// <inheritdoc />
    public async Task<CurrentFeatureResult> GetCurrentAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var resolution = await _currentFeatureResolver.ResolveAsync(store, cancellationToken).ConfigureAwait(false);

        if (!resolution.IsResolved)
        {
            return new CurrentFeatureResult(null, null, null, resolution.Warnings);
        }

        var featureId = resolution.FeatureId;
        var result = await ReadFeatureAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        return new CurrentFeatureResult(featureId, result.Document, resolution.Origin,
            KitWarningOrder.Arrange(
                [.. resolution.Warnings, .. result.Warnings, .. CheckFeatureId(featureId, result.Document)]));
    }

    /// <inheritdoc />
    public async Task<SetCurrentFeatureResult> SetCurrentAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        EnsureFeatureExists(store, featureId);

        // The branch is resolved first, so a failing git leaves the override unchanged.
        var branchFeatureId = await _currentFeatureResolver.ResolveBranchFeatureIdAsync(store, cancellationToken)
            .ConfigureAwait(false);

        var overrideWarnings = await CurrentFeatureOverride.WriteAsync(store, featureId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<KitWarning> mismatchWarnings =
            branchFeatureId is not null && !string.Equals(branchFeatureId, featureId, StringComparison.Ordinal)
                ? [new CurrentFeatureMismatchWarning(featureId, branchFeatureId)]
                : [];

        return new SetCurrentFeatureResult(featureId,
            KitWarningOrder.Arrange([.. mismatchWarnings, .. overrideWarnings]));
    }

    /// <inheritdoc />
    public async Task<FeatureDropPreview> PreviewDropAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        EnsureFeatureExists(store, featureId);

        var files = GetFeatureFiles(store, featureId);
        var overrideAffected = await OverrideSelectsAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        return new FeatureDropPreview(featureId, files, overrideAffected);
    }

    /// <inheritdoc />
    public async Task<FeatureDropResult> DropAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default)
    {
        // The override is checked before anything is changed, so a failure while reading it changes nothing.
        var preview = await PreviewDropAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        // The rename is atomic: if it fails, nothing is deleted. After it, the feature no longer exists, even if
        // deleting the hidden folder fails.
        try
        {
            store.DeleteDirectoryAtomically(KitStorePaths.GetFeatureDirPath(featureId));
        }
        catch (KitDirectoryIoFailedException exception)
        {
            throw new FeatureIoFailedException(featureId, FeatureIoFailedException.DropOperation,
                exception.FileSystemException);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The feature is dropped already, so the override must not keep selecting it; the failed delete is the
            // error that is reported.
            if (preview.OverrideAffected)
            {
                ClearOverrideBestEffort(store);
            }

            throw;
        }

        if (preview.OverrideAffected)
        {
            store.DeleteFile(KitStorePaths.CurrentFeatureFilePath);
        }

        return new FeatureDropResult(featureId, preview.Files, preview.OverrideAffected);
    }

    // An invalid id is reported as such, not as a missing feature.
    private static void EnsureFeatureExists(IKitStore store, string featureId)
    {
        FeatureIds.EnsureValid(featureId);

        if (!store.FeatureExists(featureId))
        {
            throw new FeatureNotFoundException(featureId);
        }
    }

    private static async Task<VersionedReadResult<FeatureDocument>> ReadFeatureAsync(IKitStore store,
        string featureId, CancellationToken cancellationToken)
    {
        var featureFilePath = KitStorePaths.GetFeatureFilePath(featureId);

        return await store
                   .ReadJsonAsync<FeatureDocument>(featureFilePath, FormatIds.Feature, FormatAccess.Read,
                       cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidKitFileException(KitStorePaths.GetRepositoryPath(featureFilePath),
                   "The file is missing, although the feature folder exists.");
    }

    // The folder name is the id of the feature; a different id in feature.json is reported, not corrected.
    private static IReadOnlyList<KitWarning> CheckFeatureId(string featureId, FeatureDocument feature)
    {
        return string.Equals(feature.Id, featureId, StringComparison.Ordinal)
            ? []
            : [new FeatureIdMismatchWarning(featureId, feature.Id)];
    }

    private static IReadOnlyList<string> GetFeatureFiles(IKitStore store, string featureId)
    {
        return [.. store.GetFiles(KitStorePaths.GetFeatureDirPath(featureId)).Select(KitStorePaths.GetRepositoryPath)];
    }

    private static async Task<bool> OverrideSelectsAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await store
                .ReadJsonAsync<CurrentFeatureDocument>(KitStorePaths.CurrentFeatureFilePath, FormatIds.CurrentFeature,
                    FormatAccess.Read, cancellationToken)
                .ConfigureAwait(false);

            return result is not null && string.Equals(result.Document.FeatureId, featureId, StringComparison.Ordinal);
        }
        catch (KitFileFormatException)
        {
            // An override that cannot be read, also because of config.json, selects no feature that can be proven;
            // it is left alone.
            return false;
        }
    }

    private static void ClearOverrideBestEffort(IKitStore store)
    {
        try
        {
            store.DeleteFile(KitStorePaths.CurrentFeatureFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The override then reports the dropped feature as missing; the original failure is more important.
        }
    }

    // Builds the feature in a hidden staging folder and moves it into place, so the feature folder appears only when
    // it is complete, and an existing folder, also one created by a concurrent run, is never touched. The names of
    // the hidden folders start with a dot, so they are never valid feature ids and ListAsync ignores them. Returns the
    // warnings of the written files.
    private async Task<IReadOnlyList<KitWarning>> CreateFeatureDirAsync(IKitStore store, FeatureDocument feature,
        string originalInput, CancellationToken cancellationToken)
    {
        List<KitWarning> warnings = [];

        try
        {
            await store
                .CreateDirectoryExclusiveAsync(KitStorePaths.GetFeatureDirPath(feature.Id),
                    async (featureDirWriter, token) => warnings.AddRange(
                        await WriteFeatureFilesAsync(store, featureDirWriter, feature, originalInput, token)
                            .ConfigureAwait(false)),
                    cancellationToken)
                .ConfigureAwait(false);

            return warnings;
        }
        catch (KitDirectoryExistsException exception)
        {
            // Another run created the feature folder after the existence check.
            throw new FeatureAlreadyExistsException(feature.Id, exception.FileSystemException);
        }
        catch (KitDirectoryIoFailedException exception)
        {
            // Usually the features folder is not writable or a file in the staging folder is held open; nothing was
            // changed.
            throw new FeatureIoFailedException(feature.Id, FeatureIoFailedException.CreateOperation,
                exception.FileSystemException);
        }
    }

    // The writer writes into the staging folder, but determines the line endings for the feature folder in which the
    // files end up, so git attributes and .editorconfig sections for that path apply.
    private async Task<IReadOnlyList<KitWarning>> WriteFeatureFilesAsync(IKitStore store,
        IKitDirectoryWriter featureDirWriter, FeatureDocument feature, string originalInput,
        CancellationToken cancellationToken)
    {
        var featureFileWarnings = await featureDirWriter
            .WriteJsonAsync(KitStorePaths.FeatureFileName, FormatIds.Feature, feature, FormatAccess.Write,
                cancellationToken)
            .ConfigureAwait(false);

        var requestWarnings = await WriteRequestAsync(store, featureDirWriter, feature, originalInput,
            cancellationToken).ConfigureAwait(false);

        return [.. featureFileWarnings, .. requestWarnings];
    }

    private static async Task<IReadOnlyList<KitWarning>> WriteOverrideOrRemoveFeatureAsync(IKitStore store,
        string featureId, CancellationToken cancellationToken)
    {
        try
        {
            return await CurrentFeatureOverride.WriteAsync(store, featureId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // This run moved the feature folder into place, so removing it again loses nothing; the override then
            // never selects a feature that was removed. If even the move back fails, the complete feature stays in
            // place without override; the original failure is more important.
            store.UndoCreateDirectoryExclusive(KitStorePaths.GetFeatureDirPath(featureId));
            throw;
        }
    }

    private async Task<IReadOnlyList<KitWarning>> WriteRequestAsync(IKitStore store,
        IKitDirectoryWriter featureDirWriter, FeatureDocument feature, string originalInput,
        CancellationToken cancellationToken)
    {
        var template = await ReadRequestTemplateAsync(store, cancellationToken).ConfigureAwait(false);
        var content = FeatureRequestTemplate.Fill(template, feature, originalInput);

        // The whole file, the original input included, is written with the line ending of the repository.
        return await featureDirWriter
            .WriteFileAsync(KitStorePaths.RequestFileName, TextFileContent.FromText(content), cancellationToken)
            .ConfigureAwait(false);
    }

    // The store reports a repository template that is not UTF-8 text as an invalid kit file. Its byte order mark is
    // not kept, so request.md is written without one.
    private async Task<string> ReadRequestTemplateAsync(IKitStore store, CancellationToken cancellationToken)
    {
        var repositoryTemplate = await store.ReadFileAsync(ShippedTemplates.Request.KitPath, cancellationToken)
            .ConfigureAwait(false);

        return repositoryTemplate?.ToText(LineEnding.Lf)
               ?? _shippedTemplateReader.ReadContent(ShippedTemplates.Request);
    }
}
