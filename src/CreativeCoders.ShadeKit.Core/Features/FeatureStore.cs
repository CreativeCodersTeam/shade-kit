using System.Text;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;

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
    private const string RequestTemplatePath = $"{KitStorePaths.TemplatesDirName}/{KitStorePaths.RequestFileName}";

    private const string RequestTemplateResourcePath = "request.template.md";

    private const string SuggestedBranchPrefix = "feature/";

    // The suffixes of the hidden folders in which a new feature is built and a dropped feature is deleted. Their names
    // start with a dot, so they are never valid feature ids and ListAsync ignores them.
    private const string StagingDirSuffix = ".tmp";

    private const string DroppingDirSuffix = ".dropping";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

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

        var repositoryRootPath = store.GetRepositoryRootPath();

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

        if (store.DirectoryExists(KitStorePaths.GetFeatureDirPath(identity.Id)))
        {
            throw new FeatureAlreadyExistsException(identity.Id);
        }

        // The override is written last. An override that cannot be used is replaced then; any other problem with it
        // surfaces now, before anything is written.
        var (_, overrideProblem) = await CurrentFeatureOverride
            .ReadAsync(store, FormatAccess.Write, cancellationToken)
            .ConfigureAwait(false);

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

        await CreateFeatureDirAsync(store, identity, feature, creator, cancellationToken).ConfigureAwait(false);

        await WriteOverrideOrRemoveFeatureAsync(store, identity.Id, overrideProblem is not null, cancellationToken)
            .ConfigureAwait(false);

        return new NewFeatureResult(feature, FeaturePaths.For(identity.Id), SuggestedBranchPrefix + identity.Id,
            configResult?.Warnings ?? []);
    }

    /// <inheritdoc />
    public async Task<FeatureListResult> ListAsync(IKitStore store, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        List<FeatureSummary> features = [];
        List<FeatureWarning> warnings = [];
        List<FormatWarning> formatWarnings = [];

        // The names come from the file system, so each one is a single path segment inside the features folder.
        foreach (var featureName in store.GetFeatureNames().Order(StringComparer.Ordinal))
        {
            // Hidden folders are the staging and dropping folders of new and dropped features, also those that a
            // failed run left behind.
            if (featureName.StartsWith('.'))
            {
                continue;
            }

            if (!FeatureIds.IsValid(featureName))
            {
                warnings.Add(FeatureWarning.FeatureFolderInvalid(featureName));

                continue;
            }

            var result = await store
                .ReadJsonAsync<FeatureDocument>(GetFeatureFilePath(featureName), FormatIds.Feature,
                    FormatAccess.Read, cancellationToken)
                .ConfigureAwait(false);

            if (result is null)
            {
                warnings.Add(FeatureWarning.FeatureFileMissing(featureName));

                continue;
            }

            warnings.AddRange(CheckFeatureId(featureName, result.Document));
            features.Add(new FeatureSummary(featureName, result.Document.Title, result.Document.Profile,
                result.Document.Status));
            formatWarnings.AddRange(result.Warnings);
        }

        return new FeatureListResult(features, warnings, [.. formatWarnings.Distinct()]);
    }

    /// <inheritdoc />
    public async Task<FeatureShowResult> ShowAsync(IKitStore store, string featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        EnsureFeatureExists(store, featureId);

        var result = await ReadFeatureAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        return new FeatureShowResult(featureId, result.Document, FeaturePaths.For(featureId),
            CheckFeatureId(featureId, result.Document), result.Warnings);
    }

    /// <inheritdoc />
    public async Task<CurrentFeatureResult> GetCurrentAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var resolution = await _currentFeatureResolver.ResolveAsync(store, cancellationToken).ConfigureAwait(false);

        if (!resolution.IsResolved)
        {
            return new CurrentFeatureResult(null, null, null, null, resolution.Warnings, resolution.FormatWarnings);
        }

        var featureId = resolution.FeatureId;
        var result = await ReadFeatureAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        return new CurrentFeatureResult(featureId, result.Document, resolution.Origin, FeaturePaths.For(featureId),
            [.. resolution.Warnings, .. CheckFeatureId(featureId, result.Document)],
            [.. resolution.FormatWarnings.Concat(result.Warnings).Distinct()]);
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

        var (_, overrideProblem) = await CurrentFeatureOverride
            .ReadAsync(store, FormatAccess.Write, cancellationToken)
            .ConfigureAwait(false);

        await CurrentFeatureOverride.WriteAsync(store, featureId, overrideProblem is not null, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<FeatureWarning> warnings =
            branchFeatureId is not null && !string.Equals(branchFeatureId, featureId, StringComparison.Ordinal)
                ? [FeatureWarning.CurrentFeatureMismatch(featureId, branchFeatureId)]
                : [];

        return new SetCurrentFeatureResult(featureId, warnings);
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
        Ensure.NotNull(store);
        EnsureFeatureExists(store, featureId);

        var files = GetFeatureFiles(store, featureId);

        // The override is checked before anything is changed, so a failure while reading it changes nothing.
        var overrideAffected = await OverrideSelectsAsync(store, featureId, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        // The rename is atomic: if it fails, nothing is deleted. After it, the feature no longer exists, even if
        // deleting the hidden folder fails.
        var droppingDir = CreateHiddenDirPath(featureId, DroppingDirSuffix);

        try
        {
            store.MoveDirectory(KitStorePaths.GetFeatureDirPath(featureId), droppingDir);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FeatureIoFailedException(featureId, FeatureIoFailedException.DropOperation, exception);
        }

        try
        {
            store.DeleteDirectory(droppingDir);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The feature is dropped already, so the override must not keep selecting it; the failed delete is the
            // error that is reported.
            ClearOverrideBestEffort(store, overrideAffected);
            throw;
        }

        var overrideCleared = false;

        if (overrideAffected)
        {
            store.DeleteFile(CurrentFeatureOverride.FilePath);
            overrideCleared = true;
        }

        return new FeatureDropResult(featureId, files, overrideAffected, overrideCleared);
    }

    private static string GetFeatureFilePath(string featureName)
    {
        return $"{KitStorePaths.GetFeatureDirPath(featureName)}/{KitStorePaths.FeatureFileName}";
    }

    // The name starts with a dot, so it is never a valid feature id; the random part keeps concurrent runs apart.
    private static string CreateHiddenDirPath(string featureId, string suffix)
    {
        return KitStorePaths.GetFeatureDirPath($".{featureId}.{Guid.NewGuid().ToString("N")[..8]}{suffix}");
    }

    // The id is checked before the path is built, so an invalid id never leads to a file system access.
    private static void EnsureFeatureExists(IKitStore store, string featureId)
    {
        FeatureIds.EnsureValid(featureId);

        if (!store.DirectoryExists(KitStorePaths.GetFeatureDirPath(featureId)))
        {
            throw new FeatureNotFoundException(featureId);
        }
    }

    private static async Task<VersionedReadResult<FeatureDocument>> ReadFeatureAsync(IKitStore store,
        string featureId, CancellationToken cancellationToken)
    {
        var featureFilePath = GetFeatureFilePath(featureId);

        return await store
                   .ReadJsonAsync<FeatureDocument>(featureFilePath, FormatIds.Feature, FormatAccess.Read,
                       cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new InvalidKitFileException(KitStorePaths.GetRepositoryPath(featureFilePath),
                   "The file is missing, although the feature folder exists.");
    }

    // The folder name is the id of the feature; a different id in feature.json is reported, not corrected.
    private static IReadOnlyList<FeatureWarning> CheckFeatureId(string featureId, FeatureDocument feature)
    {
        return string.Equals(feature.Id, featureId, StringComparison.Ordinal)
            ? []
            : [FeatureWarning.FeatureIdMismatch(featureId, feature.Id)];
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
                .ReadJsonAsync<CurrentFeatureDocument>(CurrentFeatureOverride.FilePath, FormatIds.CurrentFeature,
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

    private static void ClearOverrideBestEffort(IKitStore store, bool overrideAffected)
    {
        if (!overrideAffected)
        {
            return;
        }

        try
        {
            store.DeleteFile(CurrentFeatureOverride.FilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The override then reports the dropped feature as missing; the original failure is more important.
        }
    }

    // Builds the feature in a hidden staging folder and moves it into place, so the feature folder appears only when
    // it is complete, and an existing folder, also one created by a concurrent run, is never touched.
    private async Task CreateFeatureDirAsync(IKitStore store, FeatureIdentity identity, FeatureDocument feature,
        GitIdentity creator, CancellationToken cancellationToken)
    {
        var featureDir = KitStorePaths.GetFeatureDirPath(identity.Id);
        var stagingDir = CreateHiddenDirPath(identity.Id, StagingDirSuffix);

        try
        {
            CreateStagingDir(store, stagingDir, identity.Id);

            await store
                .WriteJsonAsync($"{stagingDir}/{KitStorePaths.FeatureFileName}", FormatIds.Feature, feature,
                    FormatAccess.Write, cancellationToken)
                .ConfigureAwait(false);

            await WriteRequestAsync(store, $"{stagingDir}/{KitStorePaths.RequestFileName}", identity, creator,
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            MoveIntoPlace(store, stagingDir, featureDir, identity.Id);
        }
        catch
        {
            // Only the staging folder belongs to this run; the feature folder of another run is never removed.
            DeleteDirectoryBestEffort(store, stagingDir);
            throw;
        }
    }

    private static void CreateStagingDir(IKitStore store, string stagingDir, string featureId)
    {
        try
        {
            store.CreateDirectory(stagingDir);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Usually the features folder is not writable; nothing exists yet that would have to be removed.
            throw new FeatureIoFailedException(featureId, FeatureIoFailedException.CreateOperation, exception);
        }
    }

    private static void MoveIntoPlace(IKitStore store, string stagingDir, string featureDir, string featureId)
    {
        try
        {
            store.MoveDirectory(stagingDir, featureDir);
        }
        catch (Exception exception) when ((exception is IOException or UnauthorizedAccessException)
                                          && store.DirectoryExists(featureDir))
        {
            // Another run created the feature folder after the existence check.
            throw new FeatureAlreadyExistsException(featureId, exception);
        }
        catch (Exception exception) when ((exception is IOException or UnauthorizedAccessException)
                                          && !store.FileExists(featureDir))
        {
            // The folder cannot be renamed, for example because the features folder is not writable or a file in the
            // staging folder is held open. A file at the feature folder path is not wrapped and stays an unexpected
            // failure.
            throw new FeatureIoFailedException(featureId, FeatureIoFailedException.CreateOperation, exception);
        }
    }

    private static async Task WriteOverrideOrRemoveFeatureAsync(IKitStore store, string featureId,
        bool replaceUnusable, CancellationToken cancellationToken)
    {
        try
        {
            await CurrentFeatureOverride.WriteAsync(store, featureId, replaceUnusable, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // This run moved the feature folder into place, so removing it again loses nothing; the override then
            // never selects a feature that was removed.
            RemoveFeatureDirBestEffort(store, featureId);
            throw;
        }
    }

    private static void RemoveFeatureDirBestEffort(IKitStore store, string featureId)
    {
        var stagingDir = CreateHiddenDirPath(featureId, StagingDirSuffix);

        try
        {
            store.MoveDirectory(KitStorePaths.GetFeatureDirPath(featureId), stagingDir);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The complete feature stays in place without override; the original failure is more important.
            return;
        }

        DeleteDirectoryBestEffort(store, stagingDir);
    }

    private async Task WriteRequestAsync(IKitStore store, string requestPath, FeatureIdentity identity,
        GitIdentity creator, CancellationToken cancellationToken)
    {
        var template = await ReadRequestTemplateAsync(store, cancellationToken).ConfigureAwait(false);
        var content = FeatureRequestTemplate.Fill(template, identity, creator);

        await store.WriteFileAsync(requestPath, Utf8WithoutBom.GetBytes(content), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<string> ReadRequestTemplateAsync(IKitStore store, CancellationToken cancellationToken)
    {
        var repositoryTemplate = await store.ReadFileAsync(RequestTemplatePath, cancellationToken)
            .ConfigureAwait(false);

        if (repositoryTemplate is not null)
        {
            return FeatureRequestTemplate.Decode(repositoryTemplate);
        }

        var shippedTemplate = ShippedTemplates.All.Single(x =>
            string.Equals(x.ResourcePath, RequestTemplateResourcePath, StringComparison.Ordinal));

        return _shippedTemplateReader.ReadContent(shippedTemplate);
    }

    private static void DeleteDirectoryBestEffort(IKitStore store, string relativePath)
    {
        try
        {
            store.DeleteDirectory(relativePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The hidden folder stays in place and is ignored; the original failure is more important.
        }
    }
}
