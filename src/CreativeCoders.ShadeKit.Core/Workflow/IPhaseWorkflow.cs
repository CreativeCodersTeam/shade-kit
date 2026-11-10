using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates.Exceptions;
using CreativeCoders.ShadeKit.Core.Git.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Runs the phase commands on the current feature of a SHADE kit store: reads its <c>feature.json</c>, lets the
/// <see cref="IPhaseEngine" /> decide, and writes the change with a <c>history</c> entry.
/// </summary>
/// <remarks>
/// <para>
/// The current feature is resolved like <c>shade-kit feature current</c>: from the branch, then from the local
/// override. Every call checks in this order: the current feature is resolved, its <c>feature.json</c> can be read,
/// its workflow profile is known, its phase state is valid, <c>gates.json</c> can be read (only for
/// <see cref="StartAsync" /> and <see cref="GetNextAsync" />), the feature is not done (only for changing calls), the
/// phase is in the profile, and the transition is allowed. Only then, and only if the state changes, the git
/// identity is read and <c>feature.json</c> is written; <see cref="StartAsync" /> copies the checklist of the gate
/// between the two.
/// </para>
/// <para>
/// A call that changes nothing, also a repeated <c>phase start</c>, leaves <c>feature.json</c> unchanged byte for
/// byte; a repeated <c>phase start</c> of a pending transition only creates a missing checklist copy.
/// <see cref="GetSkillsAsync" />, <see cref="GetNextAsync" />, and <see cref="GetGateInfoAsync" /> never write and
/// never read the git identity; <see cref="GetGateInfoAsync" /> can also read a feature by its id. A
/// feature without phase fields, created before the phase engine, counts as being in the initial state of its
/// profile; the first change writes all phase fields.
/// </para>
/// <para>
/// The warnings of every result are the warnings of resolving the current feature, the format warnings of the read
/// files, a <c>FeatureIdMismatchWarning</c> if <c>feature.json</c> records another id than its folder name, the
/// warnings of reading <c>gates.json</c> (only <see cref="StartAsync" />, <see cref="GetNextAsync" />, and
/// <see cref="GetGateInfoAsync" />, which read the gates), and the warnings of the written files; format warnings come first, and every warning is reported once.
/// </para>
/// </remarks>
public interface IPhaseWorkflow
{
    /// <summary>
    /// Starts the specified phase of the current feature: requests the transition to the next phase or the reopen of
    /// an earlier phase, performs a transition whose gate needs no approval, or reports a pending transition or the
    /// current phase without a change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gates are read from <c>gates.json</c> before the engine checks the request. If the request is refused,
    /// nothing is written.
    /// </para>
    /// <para>
    /// If the transition is requested, performed, or already pending, and its gate has a checklist, the feature folder
    /// gets a copy of the checklist template at the path of the checklist relative to the feature folder, for example
    /// <c>checks/definition-of-ready.md</c>; an existing copy is left unchanged, so a filled checklist survives a
    /// reopen and a repeated start. A reopen has no checklist and copies nothing; an
    /// <see cref="PhaseStartOutcome.AlreadyInProgress" /> or <see cref="PhaseStartOutcome.ResumePending" /> outcome
    /// copies nothing either.
    /// </para>
    /// <para>
    /// If the state changes, the git identity is read first, then the checklist is copied, then <c>feature.json</c>
    /// is written; so a missing identity leaves no copy, and a failing copy leaves <c>feature.json</c> unchanged. If
    /// writing <c>feature.json</c> fails after the copy was created, the copy stays and the next start keeps it. A
    /// pending transition only gets its copy made sure of; it reads no identity and writes no <c>feature.json</c>.
    /// </para>
    /// <para>
    /// The warnings of the result also contain the warnings of reading <c>gates.json</c> and of writing the copy.
    /// </para>
    /// </remarks>
    /// <param name="store">The SHADE kit store of the current feature.</param>
    /// <param name="phase">The name of the requested phase.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the outcome, the transition, the
    /// current phase after the request, the gate, and the skills to load.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="NoCurrentFeatureException">No current feature is resolved.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or another file read for the command is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c> or another file read for the command uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException"><c>feature.json</c> uses an older format version.</exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="PhaseTransitionNotAllowedException">The profile does not allow the transition.</exception>
    /// <exception cref="GitIdentityNotConfiguredException">
    /// The state changes, but the git identity that the <c>history</c> entry records is not configured; nothing is
    /// written.
    /// </exception>
    /// <exception cref="ChecklistTemplateNotFoundException">
    /// The checklist of the gate has to be copied, but neither the copy nor its template exists; nothing is written.
    /// </exception>
    /// <exception cref="PathNotAFileException">
    /// The checklist of the gate has to be copied, but a directory exists at the path of the copy or of the template;
    /// nothing is written.
    /// </exception>
    Task<PhaseStartResult> StartAsync(IKitStore store, string phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the pending transition of the current feature to the specified phase.
    /// </summary>
    /// <remarks>
    /// An approved reopen sets every phase after the reopened phase to pending; files in the feature folder are not
    /// touched.
    /// </remarks>
    /// <param name="store">The SHADE kit store of the current feature.</param>
    /// <param name="phase">The name of the target phase of the pending transition.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the approved transition and the new
    /// current phase.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="NoCurrentFeatureException">No current feature is resolved.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or another file read for the command is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c> or another file read for the command uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException"><c>feature.json</c> uses an older format version.</exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="NoPendingTransitionException">No transition to <paramref name="phase" /> is pending.</exception>
    /// <exception cref="PhaseTransitionNotAllowedException">
    /// The pending transition reopens a phase that the profile no longer allows to reopen.
    /// </exception>
    /// <exception cref="GitIdentityNotConfiguredException">
    /// The git identity that the <c>history</c> entry records is not configured.
    /// </exception>
    Task<PhaseApproveResult> ApproveAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects the pending transition of the current feature to the specified phase, so the current phase is in
    /// progress again.
    /// </summary>
    /// <param name="store">The SHADE kit store of the current feature.</param>
    /// <param name="phase">The name of the target phase of the pending transition.</param>
    /// <param name="note">
    /// The note recorded with the rejection; it is trimmed, may contain line breaks, and is dropped if it is
    /// <see langword="null" />, empty, or whitespace.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the rejected transition, the
    /// current phase, and the recorded note.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="NoCurrentFeatureException">No current feature is resolved.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or another file read for the command is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c> or another file read for the command uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException"><c>feature.json</c> uses an older format version.</exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="NoPendingTransitionException">No transition to <paramref name="phase" /> is pending.</exception>
    /// <exception cref="GitIdentityNotConfiguredException">
    /// The git identity that the <c>history</c> entry records is not configured.
    /// </exception>
    Task<PhaseRejectResult> RejectAsync(IKitStore store, string phase, string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the last phase of the current feature, which makes the feature done, and deletes the local override
    /// if it selects the feature.
    /// </summary>
    /// <remarks>
    /// The override is read before <c>feature.json</c> is written; an override that cannot be read is left alone. It
    /// is deleted after <c>feature.json</c> was written. If that deletion fails, its
    /// <see cref="IOException" /> or <see cref="UnauthorizedAccessException" /> is thrown and the feature stays done,
    /// as with <see cref="IFeatureStore.DropAsync" />, which does not catch a failing deletion of the override after
    /// the feature was dropped either; the override then selects a done feature, on which every changing phase command
    /// stops.
    /// </remarks>
    /// <param name="store">The SHADE kit store of the current feature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the completed phase, the status
    /// <see cref="FeatureStatus.Done" />, and whether the local override was deleted.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="NoCurrentFeatureException">No current feature is resolved.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or another file read for the command is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c> or another file read for the command uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException"><c>feature.json</c> uses an older format version.</exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    /// <exception cref="FeatureDoneException">The feature is done.</exception>
    /// <exception cref="PhaseCompleteNotAllowedException">
    /// The current phase is not the last phase of the profile or is not in progress.
    /// </exception>
    /// <exception cref="GitIdentityNotConfiguredException">
    /// The git identity that the <c>history</c> entry records is not configured.
    /// </exception>
    /// <exception cref="IOException">The local override selects the feature and cannot be deleted.</exception>
    /// <exception cref="UnauthorizedAccessException">
    /// The local override selects the feature and the process may not delete it.
    /// </exception>
    Task<PhaseCompleteResult> CompleteAsync(IKitStore store, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the skills an agent loads for the specified phase of the current feature; allowed on a done feature.
    /// </summary>
    /// <param name="store">The SHADE kit store of the current feature.</param>
    /// <param name="phase">The name of the phase.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the phase skill and the
    /// general skills of <paramref name="phase" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="phase" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="NoCurrentFeatureException">No current feature is resolved.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or another file read for the command is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c> or another file read for the command uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// <c>feature.json</c> uses an older format version and <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    Task<PhaseSkillsResult> GetSkillsAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current phase of the current feature and its possible next steps; allowed on a done feature.
    /// </summary>
    /// <remarks>
    /// The gates are read from <c>gates.json</c>; the warnings of reading it are part of the result. No checklist is
    /// copied.
    /// </remarks>
    /// <param name="store">The SHADE kit store of the current feature.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the current phase, its state,
    /// the status of the feature, and the possible next steps with their gates and skills.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store" /> is <see langword="null" />.</exception>
    /// <exception cref="NoCurrentFeatureException">No current feature is resolved.</exception>
    /// <exception cref="GitUnavailableException">
    /// <c>git</c> cannot be started, or it fails in a way that SHADE kit does not expect.
    /// </exception>
    /// <exception cref="InvalidKitFileException">
    /// <c>feature.json</c> is missing or invalid, or another file read for the command is invalid.
    /// </exception>
    /// <exception cref="FormatTooNewException">
    /// <c>feature.json</c> or another file read for the command uses a format version newer than the engine supports.
    /// </exception>
    /// <exception cref="UpgradeNeededException">
    /// <c>feature.json</c> uses an older format version and <c>outdatedFormatOnRead</c> is <c>stop</c>.
    /// </exception>
    /// <exception cref="UnknownProfileException">The profile in <c>feature.json</c> is not known.</exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <c>feature.json</c> do not fit the profile or contradict each other.
    /// </exception>
    Task<PhaseNextResult> GetNextAsync(IKitStore store, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets everything a gate check needs about the gate into the specified phase of a feature: the feature, its
    /// phases with their state and artifacts, the configured gate into the phase, and the coverage threshold of the
    /// repository; allowed on a done feature.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The steps run in this order, and the first one that fails stops the read: the feature is read and its phase
    /// state is checked against its profile (the feature <paramref name="featureId" />, otherwise the current
    /// feature); then <paramref name="phase" /> is checked against the profile with
    /// <see cref="IPhaseEngine.HasGate" />; then <c>gates.json</c> is read through the
    /// <see cref="ITransitionGateProvider" />; then <c>config.json</c> is read.
    /// </para>
    /// <para>
    /// The gate is the configured gate of the forward transition into <paramref name="phase" />
    /// (<see cref="TransitionGate.ForPhase" />), whatever the state of the phase. The first phase of the profile has no
    /// gate.
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
    /// The id of the feature to read; or <see langword="null" /> to read the current feature.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the feature id, the profile
    /// name, the phases of the feature, the gate into <paramref name="phase" />, the coverage threshold, and the
    /// warnings of every read file.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store" /> or <paramref name="phase" /> is <see langword="null" />.
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
    /// <exception cref="PhaseNotInProfileException"><paramref name="phase" /> is not a phase of the profile.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<GateInfoResult> GetGateInfoAsync(IKitStore store, string phase, string? featureId,
        CancellationToken cancellationToken = default);
}
