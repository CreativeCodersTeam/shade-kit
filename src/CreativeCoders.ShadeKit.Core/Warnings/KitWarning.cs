using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Core.Warnings;

/// <summary>
/// Represents a warning of the SHADE kit core about a problem that did not stop the command.
/// </summary>
/// <remarks>
/// Each kind is a sealed record that carries only its data; the CLI turns it into a code and a message.
/// </remarks>
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "The common base of the warning records: results carry one list of it and the CLI switches on " +
                    "the derived record types, which hold the data.")]
public abstract record KitWarning;
