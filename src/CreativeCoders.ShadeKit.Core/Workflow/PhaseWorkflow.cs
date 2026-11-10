using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Runs the phase commands on the current feature of a SHADE kit store with the <see cref="IPhaseEngine" />.
/// </summary>
/// <param name="currentFeatureResolver">The resolver of the current feature.</param>
/// <param name="workflowProfileCatalog">The catalog that the workflow profile of the feature must be known to.</param>
/// <param name="phaseEngine">The engine that decides the transitions.</param>
/// <param name="transitionGateProvider">
/// The provider of the gates of the transitions and of the warnings of reading the gate configuration, which the
/// results of <see cref="StartAsync" />, <see cref="GetNextAsync" />, and <see cref="GetGateInfoAsync" /> report.
/// </param>
/// <param name="featureChecklistWriter">
/// The writer that copies the checklist of a gate into the feature folder when <see cref="StartAsync" /> requests,
/// performs, or repeats a transition into a phase whose gate has a checklist.
/// </param>
/// <param name="gitRepositoryInfo">The source of the git identity that a <c>history</c> entry records.</param>
/// <param name="timeProvider">The source of the local time that a <c>history</c> entry records.</param>
public sealed class PhaseWorkflow(
    ICurrentFeatureResolver currentFeatureResolver,
    IWorkflowProfileCatalog workflowProfileCatalog,
    IPhaseEngine phaseEngine,
    ITransitionGateProvider transitionGateProvider,
    IFeatureChecklistWriter featureChecklistWriter,
    IGitRepositoryInfo gitRepositoryInfo,
    TimeProvider timeProvider)
    : IPhaseWorkflow
{
    private readonly ICurrentFeatureResolver _currentFeatureResolver = Ensure.NotNull(currentFeatureResolver);

    private readonly IWorkflowProfileCatalog _workflowProfileCatalog = Ensure.NotNull(workflowProfileCatalog);

    private readonly IPhaseEngine _phaseEngine = Ensure.NotNull(phaseEngine);

    private readonly ITransitionGateProvider _transitionGateProvider = Ensure.NotNull(transitionGateProvider);

    private readonly IFeatureChecklistWriter _featureChecklistWriter = Ensure.NotNull(featureChecklistWriter);

    private readonly IGitRepositoryInfo _gitRepositoryInfo = Ensure.NotNull(gitRepositoryInfo);

    private readonly TimeProvider _timeProvider = Ensure.NotNull(timeProvider);

    /// <inheritdoc />
    public async Task<PhaseStartResult> StartAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, null, FormatAccess.Write, cancellationToken).ConfigureAwait(false);
        var gates = await _transitionGateProvider.GetGatesAsync(store, feature.State.Profile, cancellationToken)
            .ConfigureAwait(false);

        var decision = _phaseEngine.Start(feature.State, phase, gates.Gates);

        // A pending transition changes nothing, so no identity is read for it. The identity is read before the
        // checklist copy, so a missing identity leaves no copy behind.
        GitIdentity? identity = decision.HasChanges
            ? await GetIdentityAsync(store, cancellationToken).ConfigureAwait(false)
            : null;

        var copyResult = await EnsureChecklistCopyAsync(store, feature.FeatureId, decision, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<KitWarning> copyWarnings = copyResult?.Warnings ?? [];

        // The copy is made before feature.json is written, so a failing copy leaves feature.json unchanged. If writing
        // feature.json fails after the copy was created, the copy stays; the next start of the phase finds it and
        // keeps it as it is.
        IReadOnlyList<KitWarning> writeWarnings = decision.HasChanges && identity is not null
            ? await WriteChangeAsync(store, feature, decision.Change, identity, cancellationToken)
                .ConfigureAwait(false)
            : [];

        var gate = decision is { Gate: { } transitionGate, Transition: { } transition }
            ? new PhaseGate(transition.To, transitionGate)
            : null;

        return new PhaseStartResult(feature.FeatureId, feature.State.Profile.Name, decision.Outcome,
            decision.Transition, CreateSnapshot(decision.State), gate, copyResult?.Created, decision.Skills,
            feature.ArrangeWarnings([.. gates.Warnings, .. copyWarnings, .. writeWarnings]));
    }

    /// <inheritdoc />
    public async Task<PhaseApproveResult> ApproveAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, null, FormatAccess.Write, cancellationToken).ConfigureAwait(false);

        var decision = _phaseEngine.Approve(feature.State, phase);

        var writeWarnings = await PersistAsync(store, feature, decision.Change, cancellationToken)
            .ConfigureAwait(false);

        return new PhaseApproveResult(feature.FeatureId, decision.Transition, CreateSnapshot(decision.Change.State),
            feature.ArrangeWarnings(writeWarnings));
    }

    /// <inheritdoc />
    public async Task<PhaseRejectResult> RejectAsync(IKitStore store, string phase, string? note,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, null, FormatAccess.Write, cancellationToken).ConfigureAwait(false);

        var decision = _phaseEngine.Reject(feature.State, phase, note);

        var writeWarnings = await PersistAsync(store, feature, decision.Change, cancellationToken)
            .ConfigureAwait(false);

        return new PhaseRejectResult(feature.FeatureId, decision.Rejected, CreateSnapshot(decision.Change.State),
            decision.Note, feature.ArrangeWarnings(writeWarnings));
    }

    /// <inheritdoc />
    public async Task<PhaseCompleteResult> CompleteAsync(IKitStore store,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var feature = await LoadAsync(store, null, FormatAccess.Write, cancellationToken).ConfigureAwait(false);

        var decision = _phaseEngine.Complete(feature.State);

        var identity = await GetIdentityAsync(store, cancellationToken).ConfigureAwait(false);

        // The override is checked before anything is changed, so a failure while reading it changes nothing.
        var overrideSelectsFeature = await CurrentFeatureOverride
            .SelectsAsync(store, feature.FeatureId, cancellationToken)
            .ConfigureAwait(false);

        var writeWarnings = await WriteChangeAsync(store, feature, decision.Change, identity, cancellationToken)
            .ConfigureAwait(false);

        // The feature is done now; a failing deletion is thrown as in FeatureStore.DropAsync.
        if (overrideSelectsFeature)
        {
            store.DeleteFile(KitStorePaths.CurrentFeatureFilePath);
        }

        return new PhaseCompleteResult(feature.FeatureId, decision.Phase, decision.Change.State.Status,
            overrideSelectsFeature, feature.ArrangeWarnings(writeWarnings));
    }

    /// <inheritdoc />
    public async Task<PhaseSkillsResult> GetSkillsAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, null, FormatAccess.Read, cancellationToken).ConfigureAwait(false);

        var skills = _phaseEngine.GetSkills(feature.State.Profile, phase);

        return new PhaseSkillsResult(feature.FeatureId, feature.State.Profile.Name, phase, skills,
            feature.ArrangeWarnings([]));
    }

    /// <inheritdoc />
    public async Task<PhaseNextResult> GetNextAsync(IKitStore store, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var feature = await LoadAsync(store, null, FormatAccess.Read, cancellationToken).ConfigureAwait(false);
        var gates = await _transitionGateProvider.GetGatesAsync(store, feature.State.Profile, cancellationToken)
            .ConfigureAwait(false);

        var next = _phaseEngine.GetNext(feature.State, gates.Gates);

        return new PhaseNextResult(feature.FeatureId, CreateSnapshot(next.State), next.State.Status, next.Continue,
            next.Pending, next.Forward, next.Reopen, feature.ArrangeWarnings(gates.Warnings));
    }

    /// <inheritdoc />
    public async Task<GateInfoResult> GetGateInfoAsync(IKitStore store, string phase, string? featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, featureId, FormatAccess.Read, cancellationToken).ConfigureAwait(false);
        var profile = feature.State.Profile;

        // The phase is checked before gates.json and config.json are read, so a wrong phase name is reported as such
        // even if one of them cannot be read.
        var hasGate = _phaseEngine.HasGate(profile, phase);

        var gates = await _transitionGateProvider.GetGatesAsync(store, profile, cancellationToken)
            .ConfigureAwait(false);

        var configResult = await store
            .ReadJsonAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config, FormatAccess.Read,
                cancellationToken)
            .ConfigureAwait(false);

        var gate = hasGate
            ? CreateGateInfo(store, feature.FeatureId, new PhaseGate(phase, TransitionGate.ForPhase(gates.Gates, phase)))
            : null;

        return new GateInfoResult(feature.FeatureId, profile.Name, FeaturePhase.ListOf(feature.State), gate,
            configResult?.Document.CoverageThreshold,
            feature.ArrangeWarnings([.. gates.Warnings, .. configResult?.Warnings ?? []]));
    }

    // The copy path follows the rule of FeatureChecklistWriter: the checklist path mirrored below the feature folder.
    private static GateInfoGate CreateGateInfo(IKitStore store, string featureId, PhaseGate gate)
    {
        if (gate.Gate.Checklist is not { } checklist)
        {
            return new GateInfoGate(gate, ChecklistPath: null, ChecklistExists: null);
        }

        var copyPath = KitStorePaths.GetFeatureChecklistPath(featureId, checklist);

        return new GateInfoGate(gate, KitStorePaths.GetRepositoryPath(copyPath), store.FileExists(copyPath));
    }

    private static PhaseSnapshot CreateSnapshot(FeaturePhaseState state)
    {
        return new PhaseSnapshot(state.CurrentPhase, state.CurrentPhaseState);
    }

    // Resolves the feature (the specified one, otherwise the current one), reads its feature.json, and validates its
    // phase state against its profile. A specified feature must exist; the current feature is resolved like
    // shade-kit feature current.
    private async Task<LoadedFeature> LoadAsync(IKitStore store, string? featureId, FormatAccess access,
        CancellationToken cancellationToken)
    {
        var (resolvedId, resolveWarnings) = await ResolveAsync(store, featureId, cancellationToken)
            .ConfigureAwait(false);

        var readResult = await store.ReadFeatureAsync(resolvedId, access, cancellationToken).ConfigureAwait(false);
        var document = readResult.Document;

        var state = FeaturePhaseFields.ReadState(document, resolvedId, _workflowProfileCatalog, _phaseEngine);

        return new LoadedFeature(resolvedId, document, state,
        [
            .. resolveWarnings, .. readResult.Warnings,
            .. KitStoreFeatureExtensions.CheckFeatureId(resolvedId, document)
        ]);
    }

    private async Task<(string FeatureId, IReadOnlyList<KitWarning> Warnings)> ResolveAsync(IKitStore store,
        string? featureId, CancellationToken cancellationToken)
    {
        if (featureId is not null)
        {
            store.EnsureFeatureExists(featureId);

            return (featureId, []);
        }

        var resolution = await _currentFeatureResolver.ResolveAsync(store, cancellationToken).ConfigureAwait(false);

        return resolution.IsResolved
            ? (resolution.FeatureId, resolution.Warnings)
            : throw new NoCurrentFeatureException();
    }

    // Makes sure that the feature folder contains the copy of the checklist of the decided gate. The engine decides
    // a gate only for a requested, performed, or pending transition, and a reopen always has the default gate without
    // a checklist, so the checklist of the gate alone decides whether a copy is made. Returns null if no copy is made.
    private async Task<FeatureChecklistCopyResult?> EnsureChecklistCopyAsync(IKitStore store, string featureId,
        PhaseStartDecision decision, CancellationToken cancellationToken)
    {
        if (decision.Gate?.Checklist is not { } checklist)
        {
            return null;
        }

        return await _featureChecklistWriter
            .EnsureCopyAsync(store, featureId, checklist, cancellationToken)
            .ConfigureAwait(false);
    }

    // The git identity is read only now, when a write follows, so a missing identity never stops a no-op.
    private async Task<IReadOnlyList<KitWarning>> PersistAsync(IKitStore store, LoadedFeature feature,
        PhaseStateChange change, CancellationToken cancellationToken)
    {
        var identity = await GetIdentityAsync(store, cancellationToken).ConfigureAwait(false);

        return await WriteChangeAsync(store, feature, change, identity, cancellationToken).ConfigureAwait(false);
    }

    private Task<GitIdentity> GetIdentityAsync(IKitStore store, CancellationToken cancellationToken)
    {
        return _gitRepositoryInfo.GetIdentityAsync(store.RepositoryRootPath, cancellationToken);
    }

    private async Task<IReadOnlyList<KitWarning>> WriteChangeAsync(IKitStore store, LoadedFeature feature,
        PhaseStateChange change, GitIdentity identity, CancellationToken cancellationToken)
    {
        FeaturePhaseFields.ApplyChange(feature.Document, change, _timeProvider.GetLocalNow(),
            new PhaseHistoryActor { Name = identity.Name, Email = identity.Email });

        return await store
            .WriteJsonAsync(KitStorePaths.GetFeatureFilePath(feature.FeatureId), FormatIds.Feature, feature.Document,
                FormatAccess.Write, cancellationToken)
            .ConfigureAwait(false);
    }

    // The current feature as it was read: its folder name, its document, its validated phase state, and the warnings
    // of resolving and reading it.
    private sealed record LoadedFeature(
        string FeatureId,
        FeatureDocument Document,
        FeaturePhaseState State,
        IReadOnlyList<KitWarning> Warnings)
    {
        public IReadOnlyList<KitWarning> ArrangeWarnings(IReadOnlyList<KitWarning> writeWarnings)
        {
            return KitWarningOrder.Arrange([.. Warnings, .. writeWarnings]);
        }
    }
}
