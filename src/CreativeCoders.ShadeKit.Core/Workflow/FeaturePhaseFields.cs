using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Maps the phase fields of a <see cref="FeatureDocument" /> to the phase state of the <see cref="IPhaseEngine" />
/// and back.
/// </summary>
/// <remarks>
/// The only place that knows which fields of <c>feature.json</c> hold the phase state; the phase commands and the
/// reading feature commands derive the phase state of a stored feature here. A state that is applied
/// always writes all phase fields: <c>currentPhase</c>, <c>phases</c> in the order of the state, <c>history</c>
/// (empty if the document has none yet), and <c>pendingTransition</c> only while a transition is pending.
/// </remarks>
internal static class FeaturePhaseFields
{
    /// <summary>
    /// Creates the stored phase state of the specified feature from its document, as
    /// <see cref="IPhaseEngine.ReadState" /> expects it.
    /// </summary>
    /// <param name="document">The document read from the <c>feature.json</c> file of the feature.</param>
    /// <param name="featureId">The id of the feature, which is the name of its folder.</param>
    /// <returns>
    /// The phase fields of <paramref name="document" /> with <paramref name="featureId" /> and the repository-relative
    /// path of the <c>feature.json</c> file of the feature, which messages name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="document" /> is <see langword="null" />.</exception>
    public static StoredPhaseState ToStoredState(FeatureDocument document, string featureId)
    {
        Ensure.NotNull(document);

        return new StoredPhaseState(featureId, GetFeatureFilePath(featureId), document.Status, document.CurrentPhase,
            document.Phases, document.PendingTransition);
    }

    /// <summary>
    /// Reads the validated phase state of the specified feature from its document against its workflow profile.
    /// </summary>
    /// <remarks>
    /// The profile applies live: a document without phase fields, written by an older engine, gives the initial
    /// state of its profile. Nothing is written.
    /// </remarks>
    /// <param name="document">The document read from the <c>feature.json</c> file of the feature.</param>
    /// <param name="featureId">The id of the feature, which is the name of its folder.</param>
    /// <param name="workflowProfileCatalog">
    /// The catalog that the profile of <paramref name="document" /> must be known to.
    /// </param>
    /// <param name="phaseEngine">The engine that normalizes and validates the phase fields.</param>
    /// <returns>The phase state of the feature, normalized against its profile.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document" />, <paramref name="workflowProfileCatalog" />, or <paramref name="phaseEngine" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="UnknownProfileException">
    /// The profile of <paramref name="document" /> is not known to <paramref name="workflowProfileCatalog" />; the
    /// exception names the <c>feature.json</c> file of the feature.
    /// </exception>
    /// <exception cref="InvalidPhaseStateException">
    /// The phase fields of <paramref name="document" /> do not fit its profile or contradict each other.
    /// </exception>
    public static FeaturePhaseState ReadState(FeatureDocument document, string featureId,
        IWorkflowProfileCatalog workflowProfileCatalog, IPhaseEngine phaseEngine)
    {
        Ensure.NotNull(document);
        Ensure.NotNull(workflowProfileCatalog);
        Ensure.NotNull(phaseEngine);

        // The profile comes from feature.json, so an unknown profile names the file instead of --profile.
        if (!workflowProfileCatalog.TryGetProfile(document.Profile, out var profile))
        {
            throw new UnknownProfileException(document.Profile,
                [.. workflowProfileCatalog.Profiles.Select(x => x.Name)], GetFeatureFilePath(featureId));
        }

        return phaseEngine.ReadState(profile, ToStoredState(document, featureId));
    }

    /// <summary>
    /// Gets the repository-relative path of the <c>feature.json</c> file of a feature.
    /// </summary>
    /// <param name="featureId">The id of the feature, which is the name of its folder.</param>
    /// <returns>The path, for example <c>.shadekit/features/gh-142-login-sso/feature.json</c>.</returns>
    public static string GetFeatureFilePath(string featureId)
    {
        return KitStorePaths.GetRepositoryPath(KitStorePaths.GetFeatureFilePath(featureId));
    }

    /// <summary>
    /// Writes the specified phase state into the phase fields and the status of the specified document.
    /// </summary>
    /// <param name="document">The document to change.</param>
    /// <param name="state">The phase state to write, for example the initial state of a new feature.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document" /> or <paramref name="state" /> is <see langword="null" />.
    /// </exception>
    public static void Apply(FeatureDocument document, FeaturePhaseState state)
    {
        Ensure.NotNull(document);
        Ensure.NotNull(state);

        document.Status = state.Status;
        document.CurrentPhase = state.CurrentPhase;
        document.Phases = new OrderedDictionary<string, PhaseState>(state.Phases);
        document.PendingTransition = state.PendingTransition;
        document.History ??= [];
    }

    /// <summary>
    /// Writes the new phase state of the specified change into the specified document and appends its
    /// <c>history</c> entry.
    /// </summary>
    /// <param name="document">The document to change.</param>
    /// <param name="change">The change decided by the <see cref="IPhaseEngine" />.</param>
    /// <param name="at">The point in time of the change.</param>
    /// <param name="by">The git user who caused the change.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document" />, <paramref name="change" />, or <paramref name="by" /> is <see langword="null" />.
    /// </exception>
    public static void ApplyChange(FeatureDocument document, PhaseStateChange change, DateTimeOffset at,
        PhaseHistoryActor by)
    {
        Ensure.NotNull(change);
        Ensure.NotNull(by);

        Apply(document, change.State);
        document.History!.Add(change.History.ToEntry(at, by));
    }
}
