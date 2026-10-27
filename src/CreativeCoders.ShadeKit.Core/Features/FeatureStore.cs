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
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Creates, lists, reads, selects, and drops the features of a SHADE kit store.
/// </summary>
/// <remarks>
/// The <c>request.md</c> of a new feature holds the request content that the <see cref="IFeatureIdFactory" /> loaded
/// from the source, unchanged except for its line endings; no template is used, so a <c>templates/request.md</c> in
/// the repository is neither read nor changed.
/// </remarks>
/// <param name="featureIdFactory">
/// The factory that builds the id and the identity of a new feature and loads its request content.
/// </param>
/// <param name="currentFeatureResolver">The resolver of the current feature.</param>
/// <param name="gitRepositoryInfo">The source of the git identity recorded as creator of a new feature.</param>
/// <param name="workflowProfileCatalog">
/// The catalog that the workflow profile of a new or a read feature must be known to.
/// </param>
/// <param name="phaseEngine">
/// The engine that creates the phase state of a new feature and normalizes the phase state of a read feature.
/// </param>
public sealed class FeatureStore(
    IFeatureIdFactory featureIdFactory,
    ICurrentFeatureResolver currentFeatureResolver,
    IGitRepositoryInfo gitRepositoryInfo,
    IWorkflowProfileCatalog workflowProfileCatalog,
    IPhaseEngine phaseEngine)
    : IFeatureStore
{
    private readonly IFeatureIdFactory _featureIdFactory = Ensure.NotNull(featureIdFactory);

    private readonly ICurrentFeatureResolver _currentFeatureResolver = Ensure.NotNull(currentFeatureResolver);

    private readonly IGitRepositoryInfo _gitRepositoryInfo = Ensure.NotNull(gitRepositoryInfo);

    private readonly IWorkflowProfileCatalog _workflowProfileCatalog = Ensure.NotNull(workflowProfileCatalog);

    private readonly IPhaseEngine _phaseEngine = Ensure.NotNull(phaseEngine);

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

        var profile = _workflowProfileCatalog.GetProfile(request.Profile ?? config.DefaultProfile);

        // The override and the git identity do not depend on the source, so they are checked before the factory loads
        // the request content: an unreadable override or a missing identity stops the command before gh runs or a
        // request is sent.
        //
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

        var identity = await _featureIdFactory
            .CreateAsync(request.Source, request.Title, request.WorkingDirectory, config.Trackers, repositoryRootPath,
                cancellationToken)
            .ConfigureAwait(false);

        // The id can depend on the loaded content, for example on the issue title, so it is checked only now.
        if (store.FeatureExists(identity.Id))
        {
            throw new FeatureAlreadyExistsException(identity.Id);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var feature = new FeatureDocument
        {
            Id = identity.Id,
            Title = identity.Title,
            Profile = profile.Name,
            Source = identity.Source,
            CreatedAt = identity.CreatedAt,
            CreatedBy = new FeatureCreator { Name = creator.Name, Email = creator.Email },
            Status = FeatureStatus.Active
        };

        FeaturePhaseFields.Apply(feature,
            _phaseEngine.CreateInitialState(profile, feature.Id, FeaturePhaseFields.GetFeatureFilePath(feature.Id)));

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

        var featureDirWarnings = await CreateFeatureDirAsync(store, feature, identity.RequestContent, cancellationToken)
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

            var phaseState = ReadPhaseState(result.Document, featureName);

            warnings.AddRange(result.Warnings);
            warnings.AddRange(KitStoreFeatureExtensions.CheckFeatureId(featureName, result.Document));
            features.Add(new FeatureSummary(featureName, result.Document.Title, result.Document.Profile,
                result.Document.Status, phaseState.CurrentPhase));
        }

        return new FeatureListResult(features, KitWarningOrder.Arrange(warnings));
    }

    /// <inheritdoc />
    public async Task<FeatureShowResult> ShowAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        EnsureFeatureExists(store, featureId);

        var result = await store.ReadFeatureAsync(featureId, FormatAccess.Read, cancellationToken)
            .ConfigureAwait(false);

        return new FeatureShowResult(featureId, result.Document, ReadPhaseState(result.Document, featureId),
            KitWarningOrder.Arrange(
                [.. result.Warnings, .. KitStoreFeatureExtensions.CheckFeatureId(featureId, result.Document)]));
    }

    /// <inheritdoc />
    public async Task<CurrentFeatureResult> GetCurrentAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var resolution = await _currentFeatureResolver.ResolveAsync(store, cancellationToken).ConfigureAwait(false);

        if (!resolution.IsResolved)
        {
            return new CurrentFeatureResult(null, null, null, null, resolution.Warnings);
        }

        var featureId = resolution.FeatureId;
        var result = await store.ReadFeatureAsync(featureId, FormatAccess.Read, cancellationToken)
            .ConfigureAwait(false);

        return new CurrentFeatureResult(featureId, result.Document, resolution.Origin,
            ReadPhaseState(result.Document, featureId),
            KitWarningOrder.Arrange(
            [
                .. resolution.Warnings, .. result.Warnings,
                .. KitStoreFeatureExtensions.CheckFeatureId(featureId, result.Document)
            ]));
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
        var overrideAffected = await CurrentFeatureOverride.SelectsAsync(store, featureId, cancellationToken)
            .ConfigureAwait(false);

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

    // The reading commands show the same normalized phase state as the phase commands, without writing it.
    private FeaturePhaseState ReadPhaseState(FeatureDocument document, string featureId)
    {
        return FeaturePhaseFields.ReadState(document, featureId, _workflowProfileCatalog, _phaseEngine);
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

    private static IReadOnlyList<string> GetFeatureFiles(IKitStore store, string featureId)
    {
        return [.. store.GetFiles(KitStorePaths.GetFeatureDirPath(featureId)).Select(KitStorePaths.GetRepositoryPath)];
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
    private static async Task<IReadOnlyList<KitWarning>> CreateFeatureDirAsync(IKitStore store,
        FeatureDocument feature, string requestContent, CancellationToken cancellationToken)
    {
        List<KitWarning> warnings = [];

        try
        {
            await store
                .CreateDirectoryExclusiveAsync(KitStorePaths.GetFeatureDirPath(feature.Id),
                    async (featureDirWriter, token) => warnings.AddRange(
                        await WriteFeatureFilesAsync(featureDirWriter, feature, requestContent, token)
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
    private static async Task<IReadOnlyList<KitWarning>> WriteFeatureFilesAsync(
        IKitDirectoryWriter featureDirWriter, FeatureDocument feature, string requestContent,
        CancellationToken cancellationToken)
    {
        var featureFileWarnings = await featureDirWriter
            .WriteJsonAsync(KitStorePaths.FeatureFileName, FormatIds.Feature, feature, FormatAccess.Write,
                cancellationToken)
            .ConfigureAwait(false);

        var requestWarnings = await WriteRequestAsync(featureDirWriter, requestContent, cancellationToken)
            .ConfigureAwait(false);

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

    private static async Task<IReadOnlyList<KitWarning>> WriteRequestAsync(IKitDirectoryWriter featureDirWriter,
        string requestContent, CancellationToken cancellationToken)
    {
        // The content is written as it was loaded, with the line ending of the repository and without a final line
        // ending that the content does not have.
        return await featureDirWriter
            .WriteFileAsync(KitStorePaths.RequestFileName, TextFileContent.FromText(requestContent), cancellationToken)
            .ConfigureAwait(false);
    }
}
