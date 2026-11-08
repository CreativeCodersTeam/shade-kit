using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Reads everything a gate check needs about the gate into one phase of one feature, without changing any file.
/// </summary>
public interface IGateInfoReader
{
    /// <summary>
    /// Reads the feature, the phases of its workflow profile with their state, the configured gate into the specified
    /// phase, and the coverage threshold of the repository.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The steps run in this order, and the first one that fails stops the read: the feature is read and its phase
    /// state is checked against its profile (<see cref="IFeatureStore.ShowAsync" /> with
    /// <paramref name="featureId" />, otherwise <see cref="IFeatureStore.GetCurrentAsync" />); then
    /// <paramref name="phase" /> is checked against the profile; then <c>gates.json</c> is read through the
    /// <see cref="ITransitionGateProvider" />; then <c>config.json</c> is read.
    /// </para>
    /// <para>
    /// The gate is the configured gate of the forward transition into <paramref name="phase" />, or
    /// <see cref="TransitionGate.Default" /> if <c>gates.json</c> has no entry for it, whatever the state of the phase.
    /// The first phase of the profile has no gate. A done feature is read like an active one.
    /// </para>
    /// <para>
    /// Nothing is written or deleted: neither <c>feature.json</c>, the local override, <c>gates.json</c>, nor
    /// <c>config.json</c>, and no checklist is copied. A missing <c>gates.json</c> gives the built-in gate defaults, a
    /// missing <c>config.json</c> a <see langword="null" /> coverage threshold.
    /// </para>
    /// </remarks>
    /// <param name="store">The SHADE kit store of the feature.</param>
    /// <param name="phase">The name of the target phase of the gate, for example <c>plan</c>.</param>
    /// <param name="featureId">
    /// The id of the feature to read; or <see langword="null" /> to read the current feature, resolved as
    /// <c>shade-kit feature current</c> resolves it.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the feature id, the profile
    /// name, the feature paths, the phases of the profile with their state, the gate into <paramref name="phase" />, the
    /// coverage threshold, and the warnings of every read file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="featureId" /> is <see langword="null" /> and the root directory of <paramref name="store" /> has
    /// no parent directory.
    /// </exception>
    /// <exception cref="InvalidFeatureIdException">
    /// <paramref name="featureId" /> is not <see langword="null" /> and not a valid feature id.
    /// </exception>
    /// <exception cref="FeatureNotFoundException">
    /// The folder of the feature <paramref name="featureId" /> does not exist.
    /// </exception>
    /// <exception cref="NoCurrentFeatureException">
    /// <paramref name="featureId" /> is <see langword="null" /> and no current feature is resolved.
    /// </exception>
    /// <exception cref="GitUnavailableException">
    /// <paramref name="featureId" /> is <see langword="null" /> and <c>git</c> cannot be started, or it fails in a way
    /// that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or <c>gates.json</c> or <c>config.json</c> is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c>, <c>gates.json</c>, or <c>config.json</c> uses a format version newer than the engine
    /// supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// <c>feature.json</c>, <c>gates.json</c>, or <c>config.json</c> uses an older format version and
    /// <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    /// <exception cref="PhaseNotInProfileException">
    /// <paramref name="phase" /> is not a phase of the profile of the feature.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<GateInfoResult> ReadAsync(IKitStore store, string phase, string? featureId,
        CancellationToken cancellationToken = default);
}
