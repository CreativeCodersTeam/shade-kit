using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Gates.Warnings;

/// <summary>
/// Represents a warning that an entry of <c>.shadekit/gates.json</c> is ignored; the entry stays in the file.
/// </summary>
/// <param name="ProfileName">The name of the workflow profile under which the entry is stored.</param>
/// <param name="PhaseName">
/// The name of the target phase of the ignored gate, or <see langword="null" /> if the whole profile entry is ignored
/// because of <see cref="GateEntryIgnoredReason.UnknownProfile" />.
/// </param>
/// <param name="Reason">One of the enumeration values that specifies why the entry is ignored.</param>
public sealed record GateEntryIgnoredWarning(string ProfileName, string? PhaseName, GateEntryIgnoredReason Reason)
    : KitWarning;
