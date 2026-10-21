using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Resolves the current feature of a SHADE kit store from the current git branch, read through an
/// <see cref="IGitRepositoryInfo" />, and the local override.
/// </summary>
/// <param name="gitRepositoryInfo">The reader of the current git branch.</param>
public sealed class CurrentFeatureResolver(IGitRepositoryInfo gitRepositoryInfo) : ICurrentFeatureResolver
{
    private readonly IGitRepositoryInfo _gitRepositoryInfo = Ensure.NotNull(gitRepositoryInfo);

    /// <inheritdoc />
    public async Task<CurrentFeatureResolution> ResolveAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var branchFeatureId = await ResolveBranchFeatureIdAsync(store, cancellationToken).ConfigureAwait(false);

        var (overrideResult, overrideProblem) = await CurrentFeatureOverride
            .ReadAsync(store, FormatAccess.Read, cancellationToken)
            .ConfigureAwait(false);

        var formatWarnings = overrideResult?.Warnings ?? [];
        List<FeatureWarning> warnings = [];
        string? overrideFeatureId = null;

        if (overrideProblem is not null)
        {
            warnings.Add(FeatureWarning.CurrentFeatureInvalid(DescribeProblem(overrideProblem)));
        }
        else if (overrideResult is not null)
        {
            var storedFeatureId = overrideResult.Document.FeatureId;

            if (FeatureExists(store, storedFeatureId))
            {
                overrideFeatureId = storedFeatureId;
            }
            else
            {
                warnings.Add(FeatureWarning.CurrentFeatureMissing(storedFeatureId));
            }
        }

        if (branchFeatureId is not null)
        {
            if (overrideFeatureId is not null && !string.Equals(overrideFeatureId, branchFeatureId,
                    StringComparison.Ordinal))
            {
                warnings.Add(FeatureWarning.CurrentFeatureMismatch(overrideFeatureId, branchFeatureId));
            }

            return new CurrentFeatureResolution(branchFeatureId, CurrentFeatureOrigin.Branch, warnings,
                formatWarnings);
        }

        return overrideFeatureId is not null
            ? new CurrentFeatureResolution(overrideFeatureId, CurrentFeatureOrigin.Override, warnings, formatWarnings)
            : new CurrentFeatureResolution(null, null, warnings, formatWarnings);
    }

    /// <inheritdoc />
    public async Task<string?> ResolveBranchFeatureIdAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var repositoryRootPath = store.GetRepositoryRootPath();

        var branchName = await _gitRepositoryInfo.GetCurrentBranchAsync(repositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        if (branchName is null)
        {
            return null;
        }

        var candidateFeatureId = branchName[(branchName.LastIndexOf('/') + 1)..];

        return FeatureExists(store, candidateFeatureId) ? candidateFeatureId : null;
    }

    private static string DescribeProblem(KitFileFormatException exception)
    {
        return exception switch
        {
            InvalidKitFileException e => e.Reason.TrimEnd('.'),
            FormatTooNewException e =>
                $"It uses format version {e.FileVersion}, but this engine supports only up to version " +
                $"{e.SupportedVersion}",
            UpgradeNeededException e =>
                $"It uses the older format version {e.FileVersion} (current version {e.CurrentVersion}), and " +
                "'outdatedFormatOnRead' is 'stop'",
            _ => exception.Message.TrimEnd('.')
        };
    }

    // The id is checked before the path is built, so an invalid id never leads to a file system access.
    private static bool FeatureExists(IKitStore store, string featureId)
    {
        return FeatureIds.IsValid(featureId)
               && store.DirectoryExists(KitStorePaths.GetFeatureDirPath(featureId));
    }
}
