using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Resolves the current feature of a SHADE kit store from the current git branch and the local override.
/// </summary>
/// <remarks>
/// The branch selects the feature named by the part after its last <c>/</c>, for example <c>gh-142-login-sso</c>
/// for <c>feature/gh-142-login-sso</c>, if that is a valid id whose folder exists; otherwise, and for a detached
/// <c>HEAD</c>, it selects no feature and no warning is reported.
/// </remarks>
public interface ICurrentFeatureResolver
{
    /// <summary>
    /// Resolves the current feature of the specified store.
    /// </summary>
    /// <remarks>
    /// The branch wins over the local override. The override is always read and never changed; an invalid or missing
    /// feature gives a <see cref="CurrentFeatureMissingWarning" />, an unusable format a
    /// <see cref="CurrentFeatureInvalidWarning" />, and a feature other than the branch's a
    /// <see cref="CurrentFeatureMismatchWarning" />. Format problems of <c>config.json</c> still stop the resolution.
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
