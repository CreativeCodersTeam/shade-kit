using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats;
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
/// <param name="transitionGateProvider">The provider of the gates of the transitions.</param>
/// <param name="gitRepositoryInfo">The source of the git identity that a <c>history</c> entry records.</param>
/// <param name="timeProvider">The source of the local time that a <c>history</c> entry records.</param>
public sealed class PhaseWorkflow(
    ICurrentFeatureResolver currentFeatureResolver,
    IWorkflowProfileCatalog workflowProfileCatalog,
    IPhaseEngine phaseEngine,
    ITransitionGateProvider transitionGateProvider,
    IGitRepositoryInfo gitRepositoryInfo,
    TimeProvider timeProvider)
    : IPhaseWorkflow
{
    private readonly ICurrentFeatureResolver _currentFeatureResolver = Ensure.NotNull(currentFeatureResolver);

    private readonly IWorkflowProfileCatalog _workflowProfileCatalog = Ensure.NotNull(workflowProfileCatalog);

    private readonly IPhaseEngine _phaseEngine = Ensure.NotNull(phaseEngine);

    private readonly ITransitionGateProvider _transitionGateProvider = Ensure.NotNull(transitionGateProvider);

    private readonly IGitRepositoryInfo _gitRepositoryInfo = Ensure.NotNull(gitRepositoryInfo);

    private readonly TimeProvider _timeProvider = Ensure.NotNull(timeProvider);

    /// <inheritdoc />
    public async Task<PhaseStartResult> StartAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, FormatAccess.Write, cancellationToken).ConfigureAwait(false);
        var gates = await GetGatesAsync(store, feature.State.Profile, cancellationToken).ConfigureAwait(false);

        var decision = _phaseEngine.Start(feature.State, phase, gates);

        var writeWarnings = decision.HasChanges
            ? await PersistAsync(store, feature, decision.Change, cancellationToken).ConfigureAwait(false)
            : [];

        var gate = decision is { Gate: { } transitionGate, Transition: { } transition }
            ? new PhaseGate(transition.To, transitionGate)
            : null;

        return new PhaseStartResult(feature.FeatureId, feature.State.Profile.Name, decision.Outcome,
            decision.Transition, CreateSnapshot(decision.State), gate, decision.Skills,
            feature.ArrangeWarnings(writeWarnings));
    }

    /// <inheritdoc />
    public async Task<PhaseApproveResult> ApproveAsync(IKitStore store, string phase,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadAsync(store, FormatAccess.Write, cancellationToken).ConfigureAwait(false);

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

        var feature = await LoadAsync(store, FormatAccess.Write, cancellationToken).ConfigureAwait(false);

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

        var feature = await LoadAsync(store, FormatAccess.Write, cancellationToken).ConfigureAwait(false);

        var decision = _phaseEngine.Complete(feature.State);

        var identity = await _gitRepositoryInfo.GetIdentityAsync(store.RepositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

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

        var feature = await LoadAsync(store, FormatAccess.Read, cancellationToken).ConfigureAwait(false);

        var skills = _phaseEngine.GetSkills(feature.State.Profile, phase);

        return new PhaseSkillsResult(feature.FeatureId, feature.State.Profile.Name, phase, skills,
            feature.ArrangeWarnings([]));
    }

    /// <inheritdoc />
    public async Task<PhaseNextResult> GetNextAsync(IKitStore store, CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);

        var feature = await LoadAsync(store, FormatAccess.Read, cancellationToken).ConfigureAwait(false);
        var gates = await GetGatesAsync(store, feature.State.Profile, cancellationToken).ConfigureAwait(false);

        var next = _phaseEngine.GetNext(feature.State, gates);

        return new PhaseNextResult(feature.FeatureId, CreateSnapshot(next.State), next.State.Status, next.Continue,
            next.Pending, next.Forward, next.Reopen, feature.ArrangeWarnings([]));
    }

    private static PhaseSnapshot CreateSnapshot(FeaturePhaseState state)
    {
        return new PhaseSnapshot(state.CurrentPhase, state.CurrentPhaseState);
    }

    // Resolves the current feature, reads its feature.json, and validates its phase state against its profile.
    private async Task<LoadedFeature> LoadAsync(IKitStore store, FormatAccess access,
        CancellationToken cancellationToken)
    {
        var resolution = await _currentFeatureResolver.ResolveAsync(store, cancellationToken).ConfigureAwait(false);

        if (!resolution.IsResolved)
        {
            throw new NoCurrentFeatureException();
        }

        var featureId = resolution.FeatureId;

        var readResult = await store.ReadFeatureAsync(featureId, access, cancellationToken).ConfigureAwait(false);
        var document = readResult.Document;

        var state = FeaturePhaseFields.ReadState(document, featureId, _workflowProfileCatalog, _phaseEngine);

        return new LoadedFeature(featureId, document, state,
        [
            .. resolution.Warnings, .. readResult.Warnings,
            .. KitStoreFeatureExtensions.CheckFeatureId(featureId, document)
        ]);
    }

    private async Task<IReadOnlyDictionary<string, TransitionGate>> GetGatesAsync(IKitStore store,
        WorkflowProfile profile, CancellationToken cancellationToken)
    {
        var gates = new Dictionary<string, TransitionGate>(StringComparer.Ordinal);

        foreach (var phaseName in profile.Phases.Select(x => x.Name))
        {
            gates[phaseName] = await _transitionGateProvider
                .GetGateAsync(store, profile, phaseName, cancellationToken)
                .ConfigureAwait(false);
        }

        return gates;
    }

    // The git identity is read only now, when a write follows, so a missing identity never stops a no-op.
    private async Task<IReadOnlyList<KitWarning>> PersistAsync(IKitStore store, LoadedFeature feature,
        PhaseStateChange change, CancellationToken cancellationToken)
    {
        var identity = await _gitRepositoryInfo.GetIdentityAsync(store.RepositoryRootPath, cancellationToken)
            .ConfigureAwait(false);

        return await WriteChangeAsync(store, feature, change, identity, cancellationToken).ConfigureAwait(false);
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
