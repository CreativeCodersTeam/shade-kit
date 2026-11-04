using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Workflow;

/// <summary>
/// Represents the gates of the transitions into every phase of a workflow profile, together with the warnings of
/// reading the gate configuration.
/// </summary>
/// <param name="Gates">
/// The gate of the transition into each phase of the profile, keyed by the name of the target phase (compared
/// ordinally); every phase of the profile has an entry.
/// </param>
/// <param name="Warnings">The warnings of reading the gate configuration, in the order the reader reports them.</param>
public sealed record WorkflowProfileGates(
    IReadOnlyDictionary<string, TransitionGate> Gates,
    IReadOnlyList<KitWarning> Warnings);
