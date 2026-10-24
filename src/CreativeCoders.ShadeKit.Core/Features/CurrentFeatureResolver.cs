using CreativeCoders.Core;
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
            .ReadAsync(store, cancellationToken)
            .ConfigureAwait(false);

        // The format warnings of the override come first, as in every result.
        List<KitWarning> warnings = [.. overrideResult?.Warnings ?? []];
        string? overrideFeatureId = null;

        if (overrideProblem is not null)
        {
            warnings.Add(new CurrentFeatureInvalidWarning(overrideProblem));
        }
        else if (overrideResult is not null)
        {
            var storedFeatureId = overrideResult.Document.FeatureId;

            if (store.FeatureExists(storedFeatureId))
            {
                overrideFeatureId = storedFeatureId;
            }
            else
            {
                warnings.Add(new CurrentFeatureMissingWarning(storedFeatureId));
            }
        }

        if (branchFeatureId is not null)
        {
            if (overrideFeatureId is not null && !string.Equals(overrideFeatureId, branchFeatureId,
                    StringComparison.Ordinal))
            {
                warnings.Add(new CurrentFeatureMismatchWarning(overrideFeatureId, branchFeatureId));
            }

            return new CurrentFeatureResolution(branchFeatureId, CurrentFeatureOrigin.Branch, warnings);
        }

        return overrideFeatureId is not null
            ? new CurrentFeatureResolution(overrideFeatureId, CurrentFeatureOrigin.Override, warnings)
            : new CurrentFeatureResolution(null, null, warnings);
    }

    /// <inheritdoc />
    public async Task<string?> ResolveBranchFeatureIdAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var repositoryRootPath = store.RepositoryRootPath;

        var branchName = await _gitRepositoryInfo.GetCurrentBranchAsync(repositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        if (branchName is null)
        {
            return null;
        }

        var candidateFeatureId = branchName[(branchName.LastIndexOf('/') + 1)..];

        return store.FeatureExists(candidateFeatureId) ? candidateFeatureId : null;
    }
}
