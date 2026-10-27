namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the possible next steps of a feature, the result of <see cref="IPhaseEngine.GetNext" />.
/// </summary>
/// <remarks>
/// For a done feature only <paramref name="State" /> is set. While the current phase is in progress,
/// <paramref name="Continue" /> and <paramref name="Reopen" /> are set, and <paramref name="Forward" /> unless the
/// current phase is the last phase. While it awaits approval, only <paramref name="Pending" /> is set besides
/// <paramref name="State" />.
/// </remarks>
/// <param name="State">The current phase state of the feature.</param>
/// <param name="Continue">The current phase, if it is in progress; otherwise, <see langword="null" />.</param>
/// <param name="Pending">
/// The target of the pending transition, if the current phase awaits approval; otherwise, <see langword="null" />.
/// </param>
/// <param name="Forward">
/// The phase after the current phase, if the current phase is in progress and not the last phase; otherwise,
/// <see langword="null" />.
/// </param>
/// <param name="Reopen">
/// The earlier phases that can be reopened, in profile order, possibly empty, if the current phase is in progress;
/// otherwise, <see langword="null" />.
/// </param>
public sealed record PhaseNextSteps(
    FeaturePhaseState State,
    PhaseStep? Continue,
    GatedPhaseStep? Pending,
    GatedPhaseStep? Forward,
    IReadOnlyList<PhaseStep>? Reopen);
