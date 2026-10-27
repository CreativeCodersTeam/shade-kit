using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Next;

/// <summary>
/// Represents the <c>current</c> object in the <c>data</c> of <c>shade-kit phase next --json</c>.
/// </summary>
/// <param name="FeatureId">The id of the current feature.</param>
/// <param name="Phase">The current phase of the feature.</param>
/// <param name="State">
/// The state of the current phase, written as <c>pending</c>, <c>in-progress</c>, <c>awaiting-approval</c>, or
/// <c>approved</c>.
/// </param>
/// <param name="Status">The status of the feature, written as <c>active</c> or <c>done</c>.</param>
public sealed record PhaseNextCommandCurrentData(string FeatureId, string Phase, PhaseState State, FeatureStatus Status);
