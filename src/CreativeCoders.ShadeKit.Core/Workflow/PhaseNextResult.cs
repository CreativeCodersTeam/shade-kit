using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the result of <see cref="IPhaseWorkflow.GetNextAsync" />: the current phase of the feature and its
/// possible next steps.
/// </summary>
/// <remarks>
/// For a done feature only <paramref name="FeatureId" />, <paramref name="CurrentPhase" />, and
/// <paramref name="Status" /> are set. While the current phase is in progress, <paramref name="Continue" /> and
/// <paramref name="Reopen" /> are set, and <paramref name="Forward" /> unless the current phase is the last phase.
/// While it awaits approval, only <paramref name="Pending" /> is set besides the current data.
/// </remarks>
/// <param name="FeatureId">The id of the current feature, which is the name of its folder.</param>
/// <param name="CurrentPhase">
/// The phase that is in progress, awaiting approval, or, for a done feature, approved last, with its state.
/// </param>
/// <param name="Status">One of the enumeration values that specifies whether the feature is active or done.</param>
/// <param name="Continue">The current phase, if it is in progress; otherwise, <see langword="null" />.</param>
/// <param name="Pending">
/// The target of the pending transition with its gate, if the current phase awaits approval; otherwise,
/// <see langword="null" />.
/// </param>
/// <param name="Forward">
/// The phase after the current phase with its gate, if the current phase is in progress and not the last phase;
/// otherwise, <see langword="null" />.
/// </param>
/// <param name="Reopen">
/// The earlier phases that can be reopened, in profile order, possibly empty, if the current phase is in progress;
/// otherwise, <see langword="null" />.
/// </param>
/// <param name="Warnings">The format warnings first, then the other warnings, each only once.</param>
public sealed record PhaseNextResult(
    string FeatureId,
    PhaseSnapshot CurrentPhase,
    FeatureStatus Status,
    PhaseStep? Continue,
    GatedPhaseStep? Pending,
    GatedPhaseStep? Forward,
    IReadOnlyList<PhaseStep>? Reopen,
    IReadOnlyList<KitWarning> Warnings);
