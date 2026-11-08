using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Config;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Reads the gate information of a feature through the <see cref="IFeatureStore" />, the
/// <see cref="ITransitionGateProvider" />, and the store, without writing anything.
/// </summary>
/// <param name="featureStore">
/// The store that reads the feature, given by its id or as the current feature, and normalizes its phase state.
/// </param>
/// <param name="transitionGateProvider">The provider of the configured gates of <c>gates.json</c>.</param>
public sealed class GateInfoReader(IFeatureStore featureStore, ITransitionGateProvider transitionGateProvider)
    : IGateInfoReader
{
    private readonly IFeatureStore _featureStore = Ensure.NotNull(featureStore);

    private readonly ITransitionGateProvider _transitionGateProvider = Ensure.NotNull(transitionGateProvider);

    /// <inheritdoc />
    public async Task<GateInfoResult> ReadAsync(IKitStore store, string phase, string? featureId,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(store);
        Ensure.NotNull(phase);

        var feature = await LoadFeatureAsync(store, featureId, cancellationToken).ConfigureAwait(false);
        var profile = feature.PhaseState.Profile;

        // The phase is checked before gates.json and config.json are read, so a wrong phase name is reported as such
        // even if one of them cannot be read.
        var phaseIndex = profile.IndexOf(phase);

        if (phaseIndex < 0)
        {
            throw new PhaseNotInProfileException(phase, profile.Name, [.. profile.Phases.Select(x => x.Name)]);
        }

        var gates = await _transitionGateProvider.GetGatesAsync(store, profile, cancellationToken)
            .ConfigureAwait(false);

        var configResult = await store
            .ReadJsonAsync<KitConfig>(KitStorePaths.ConfigFileName, FormatIds.Config, FormatAccess.Read,
                cancellationToken)
            .ConfigureAwait(false);

        // No transition leads into the first phase of a profile, so it has no gate.
        var gate = phaseIndex == 0
            ? null
            : CreateGate(store, feature.FeatureId, phase,
                gates.Gates.GetValueOrDefault(phase) ?? TransitionGate.Default);

        IReadOnlyList<PhaseSnapshot> phases =
        [
            .. profile.Phases.Select(x =>
                new PhaseSnapshot(x.Name, feature.PhaseState.GetPhaseState(x.Name) ?? PhaseState.Pending))
        ];

        return new GateInfoResult(feature.FeatureId, profile.Name, feature.Paths, phases, gate,
            configResult?.Document.CoverageThreshold,
            KitWarningOrder.Arrange([.. feature.Warnings, .. gates.Warnings, .. configResult?.Warnings ?? []]));
    }

    // The feature store checks the id, reads feature.json, and validates the phase state against the profile; a
    // done feature is read like an active one.
    private async Task<LoadedFeature> LoadFeatureAsync(IKitStore store, string? featureId,
        CancellationToken cancellationToken)
    {
        if (featureId is not null)
        {
            var shown = await _featureStore.ShowAsync(store, featureId, cancellationToken).ConfigureAwait(false);

            return new LoadedFeature(shown.FeatureId, shown.PhaseState, shown.Paths, shown.Warnings);
        }

        var current = await _featureStore.GetCurrentAsync(store, cancellationToken).ConfigureAwait(false);

        if (!current.IsResolved)
        {
            throw new NoCurrentFeatureException();
        }

        return new LoadedFeature(current.FeatureId, current.PhaseState, current.Paths, current.Warnings);
    }

    // The copy path follows the rule of FeatureChecklistWriter: the checklist path mirrored below the feature folder.
    private static GateInfoGate CreateGate(IKitStore store, string featureId, string phase, TransitionGate gate)
    {
        if (gate.Checklist is not { } checklist)
        {
            return new GateInfoGate(phase, gate.Approval, Checklist: null, ChecklistPath: null,
                ChecklistExists: null);
        }

        var copyPath = KitStorePaths.GetFeatureChecklistPath(featureId, checklist);

        return new GateInfoGate(phase, gate.Approval, checklist, KitStorePaths.GetRepositoryPath(copyPath),
            store.FileExists(copyPath));
    }

    // The feature as the feature store read it: its folder name, its normalized phase state, its paths, and the
    // warnings of resolving and reading it.
    private sealed record LoadedFeature(
        string FeatureId,
        FeaturePhaseState PhaseState,
        FeaturePaths Paths,
        IReadOnlyList<KitWarning> Warnings);
}
