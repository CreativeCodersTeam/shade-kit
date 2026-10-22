using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Core;

/// <summary>
/// Represents a warning of the SHADE kit core about a problem that did not stop the command.
/// </summary>
/// <remarks>
/// Each kind of warning is a sealed record that derives from this type and carries exactly the data of its kind, for
/// example <see cref="Formats.FormatWarning" /> for the format of a versioned file and
/// <see cref="Features.FeatureIdMismatchWarning" /> for a feature. The core reports facts only; the CLI turns each
/// warning into its code and message.
/// </remarks>
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "The common base of the warning records: results carry one list of it and the CLI switches on " +
                    "the derived record types, which hold the data.")]
public abstract record KitWarning;
