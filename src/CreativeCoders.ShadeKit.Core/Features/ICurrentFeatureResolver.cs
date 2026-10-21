using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Resolves the current feature of a SHADE kit store from the current git branch and the local override.
/// </summary>
/// <remarks>
/// <para>
/// The repository root is the parent directory of <see cref="IKitStore.RootPath" />, because the SHADE kit root
/// directory <c>.shadekit</c> lies at the root of the git repository. <c>git</c> is run there to read the current
/// branch.
/// </para>
/// <para>
/// The current branch selects the feature whose id is the part of the branch name after its last <c>/</c>, for example
/// <c>gh-142-login-sso</c> for <c>feature/gh-142-login-sso</c>, or the whole name if it contains no <c>/</c>. The
/// branch counts only if that part is a valid feature id (<see cref="FeatureIds.IsValid" />) and the folder
/// <c>features/&lt;id&gt;</c> of the store exists; otherwise, and for a detached <c>HEAD</c>, the branch selects no
/// feature and no warning is reported.
/// </para>
/// </remarks>
public interface ICurrentFeatureResolver
{
    /// <summary>
    /// Resolves the current feature of the specified store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The feature selected by the current branch wins. Otherwise the local override
    /// <c>features/current-feature.json</c>, read with <see cref="FormatAccess.Read" /> access, selects the feature.
    /// Otherwise no current feature is resolved.
    /// </para>
    /// <para>
    /// An override whose id is not a valid feature id or whose folder does not exist is ignored with a
    /// <see cref="FeatureWarningKind.CurrentFeatureMissing" /> warning, also when the branch selects a feature; no path
    /// is built from an invalid id. If the branch and the override select different existing features, the branch
    /// wins with a <see cref="FeatureWarningKind.CurrentFeatureMismatch" /> warning. The override is read even when
    /// the branch selects a feature, so its problems are always reported.
    /// </para>
    /// <para>
    /// An override that cannot be used because of its own format (invalid, a format version newer than the engine
    /// supports, or an older format version while <c>outdatedFormatOnRead</c> is <c>stop</c>) is ignored with a
    /// <see cref="FeatureWarningKind.CurrentFeatureInvalid" /> warning that describes the problem; the branch can
    /// still select a feature, and the file is left unchanged. A format problem of <c>config.json</c>, which is read
    /// for the policy of an outdated override, still stops the resolution.
    /// </para>
    /// </remarks>
    /// <param name="store">The SHADE kit store whose current feature is resolved.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous resolve operation. The task result contains the current feature and
    /// its origin, or neither if no current feature is resolved, together with the feature and format warnings.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// The root directory of <paramref name="store" /> has no parent directory.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// The local override uses an older format version and <c>config.json</c> is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// The local override uses an older format version and <c>config.json</c> uses a format version newer than the
    /// engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// The local override and <c>config.json</c> both use an older format version and <c>outdatedFormatOnRead</c> is
    /// <c>stop</c>.
    /// </exception>
    Task<CurrentFeatureResolution> ResolveAsync(IKitStore store, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the feature that the current git branch selects in the specified store, without reading the local
    /// override.
    /// </summary>
    /// <remarks>
    /// <c>shade-kit feature set</c> uses this method to warn when the branch selects a feature other than the one it
    /// sets.
    /// </remarks>
    /// <param name="store">The SHADE kit store whose features the branch is matched against.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous resolve operation. The task result contains the id of the feature
    /// that the current branch selects, or <see langword="null" /> if <c>HEAD</c> is detached or the branch selects no
    /// existing feature.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// The root directory of <paramref name="store" /> has no parent directory.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    Task<string?> ResolveBranchFeatureIdAsync(IKitStore store, CancellationToken cancellationToken = default);
}
