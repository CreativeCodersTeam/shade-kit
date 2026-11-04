using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Core.Gates;

/// <summary>
/// Represents the gate configuration of a SHADE kit store as the engine uses it, together with the warnings of
/// reading it.
/// </summary>
/// <param name="Profiles">
/// A read-only view of the gates keyed by profile name and then by the name of the target phase, in file order. The
/// built-in defaults of <see cref="GatesConfig.CreateDefault" /> replace a missing file or a missing <c>profiles</c>
/// field. Ignored entries are kept; a gate whose checklist is ignored because of
/// <see cref="GateEntryIgnoredReason.SharedChecklist" /> has no checklist in the view.
/// </param>
/// <param name="Warnings">
/// The warnings of reading the configuration: the format warnings first, then the gate warnings; an empty list if
/// there were none.
/// </param>
public sealed record GatesConfigReadResult(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, TransitionGate>> Profiles,
    IReadOnlyList<KitWarning> Warnings);
