using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Core.Workflow.Engine;

/// <summary>
/// Represents the decision of <see cref="IPhaseEngine.Complete" />.
/// </summary>
/// <param name="Phase">The name of the last phase of the profile, which was completed.</param>
/// <param name="Change">
/// The change to persist; its state has the last phase approved and the status <see cref="FeatureStatus.Done" />.
/// </param>
public sealed record PhaseCompleteDecision(string Phase, PhaseStateChange Change);
