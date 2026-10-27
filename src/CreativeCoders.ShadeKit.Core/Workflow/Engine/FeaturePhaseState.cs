using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the validated phase state of a feature against its workflow profile.
/// </summary>
/// <remarks>
/// Instances are immutable and come only from <see cref="IPhaseEngine" />: from
/// <see cref="IPhaseEngine.CreateInitialState" />, <see cref="IPhaseEngine.ReadState" />, or as the new state of a
/// <see cref="PhaseStateChange" />. Every phase of <see cref="Profile" /> has an entry in <see cref="Phases" />.
/// </remarks>
public sealed class FeaturePhaseState
{
    private readonly KeyValuePair<string, PhaseState>[] _phases;

    internal FeaturePhaseState(WorkflowProfile profile, string featureId, string featureFilePath, FeatureStatus status,
        string currentPhase, IEnumerable<KeyValuePair<string, PhaseState>> phases, string? pendingTransition)
    {
        Profile = profile;
        FeatureId = featureId;
        FeatureFilePath = featureFilePath;
        Status = status;
        CurrentPhase = currentPhase;
        _phases = [.. phases];
        Phases = _phases.AsReadOnly();
        PendingTransition = pendingTransition;
    }

    /// <summary>
    /// Gets the workflow profile the state was validated against.
    /// </summary>
    /// <value>The profile of the feature, whose phases decide the order of every transition.</value>
    public WorkflowProfile Profile { get; }

    /// <summary>
    /// Gets the id of the feature.
    /// </summary>
    /// <value>The feature id, for example <c>gh-142-login-sso</c>.</value>
    public string FeatureId { get; }

    /// <summary>
    /// Gets the path of the <c>feature.json</c> file of the feature, which messages name.
    /// </summary>
    /// <value>The repository-relative path, for example <c>.shadekit/features/gh-142-login-sso/feature.json</c>.</value>
    public string FeatureFilePath { get; }

    /// <summary>
    /// Gets whether work on the feature is still in progress.
    /// </summary>
    /// <value>One of the enumeration values.</value>
    public FeatureStatus Status { get; }

    /// <summary>
    /// Gets the phase that is in progress, awaiting approval, or, for a done feature, approved last.
    /// </summary>
    /// <value>The name of a phase of <see cref="Profile" />.</value>
    public string CurrentPhase { get; }

    /// <summary>
    /// Gets the state of the current phase.
    /// </summary>
    /// <value>The entry of <see cref="CurrentPhase" /> in <see cref="Phases" />.</value>
    public PhaseState CurrentPhaseState => GetPhaseState(CurrentPhase) ?? PhaseState.Pending;

    /// <summary>
    /// Gets the state of each phase.
    /// </summary>
    /// <value>
    /// The phases of <see cref="Profile" /> in profile order, followed by the preserved entries of phases the profile
    /// does not have, in the order they were read. The list can be passed to the constructor of
    /// <see cref="OrderedDictionary{TKey, TValue}" /> to write <see cref="FeatureDocument.Phases" />.
    /// </value>
    public IReadOnlyList<KeyValuePair<string, PhaseState>> Phases { get; }

    /// <summary>
    /// Gets the target phase of the transition that waits for an approval.
    /// </summary>
    /// <value>
    /// The name of a phase of <see cref="Profile" /> while <see cref="CurrentPhaseState" /> is
    /// <see cref="PhaseState.AwaitingApproval" />; otherwise, <see langword="null" />.
    /// </value>
    public string? PendingTransition { get; }

    /// <summary>
    /// Gets the transition that waits for an approval.
    /// </summary>
    /// <value>
    /// The transition from <see cref="CurrentPhase" /> to <see cref="PendingTransition" />, a
    /// <see cref="PhaseTransitionKind.Reopen" /> if the target comes before the current phase; or
    /// <see langword="null" /> if no transition is pending.
    /// </value>
    public PhaseTransition? Pending => PendingTransition is null
        ? null
        : new PhaseTransition(GetKind(CurrentPhase, PendingTransition), CurrentPhase, PendingTransition);

    /// <summary>
    /// Gets the state of the specified phase.
    /// </summary>
    /// <param name="phase">The name of the phase.</param>
    /// <returns>
    /// The state of <paramref name="phase" />, or <see langword="null" /> if <see cref="Phases" /> has no entry for it.
    /// </returns>
    public PhaseState? GetPhaseState(string? phase)
    {
        foreach (var entry in _phases)
        {
            if (string.Equals(entry.Key, phase, StringComparison.Ordinal))
            {
                return entry.Value;
            }
        }

        return null;
    }

    internal PhaseTransitionKind GetKind(string from, string to)
    {
        return Profile.IndexOf(to) < Profile.IndexOf(from) ? PhaseTransitionKind.Reopen : PhaseTransitionKind.Forward;
    }

    internal FeaturePhaseState With(string currentPhase, string? pendingTransition,
        IReadOnlyDictionary<string, PhaseState> changedPhases, FeatureStatus? status = null)
    {
        var phases = _phases.Select(x => changedPhases.TryGetValue(x.Key, out var state)
            ? KeyValuePair.Create(x.Key, state)
            : x);

        return new FeaturePhaseState(Profile, FeatureId, FeatureFilePath, status ?? Status, currentPhase, phases,
            pendingTransition);
    }
}
