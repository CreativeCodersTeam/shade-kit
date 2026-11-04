namespace CreativeCoders.ShadeKit.Core.Gates.Warnings;

/// <summary>
/// Specifies why an entry of <c>.shadekit/gates.json</c> is ignored.
/// </summary>
public enum GateEntryIgnoredReason
{
    /// <summary>
    /// The entry configures the gates of a workflow profile that the engine does not know.
    /// </summary>
    UnknownProfile,

    /// <summary>
    /// The entry configures the gate into a phase that the workflow profile does not have.
    /// </summary>
    UnknownPhase,

    /// <summary>
    /// The entry configures the gate into the first phase of the workflow profile, which no transition leads into.
    /// </summary>
    FirstPhase,

    /// <summary>
    /// The entry names the same checklist, compared without regard to case, as the gate into an earlier phase of the
    /// workflow profile; only the checklist of the entry is ignored, so the transition has no checklist, and its
    /// approval still applies.
    /// </summary>
    SharedChecklist
}
