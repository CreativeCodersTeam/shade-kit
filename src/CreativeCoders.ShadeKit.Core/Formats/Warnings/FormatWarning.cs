using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Formats.Warnings;

/// <summary>
/// Represents a warning about the format of a versioned file that did not stop the command.
/// </summary>
/// <param name="Kind">One of the enumeration values that specifies the kind of the warning.</param>
/// <param name="FilePath">The path of the file the warning is about.</param>
/// <param name="FileVersion">The format version of the file on disk.</param>
/// <param name="CurrentVersion">The current format version of the file type.</param>
public sealed record FormatWarning(FormatWarningKind Kind, string FilePath, int FileVersion, int CurrentVersion)
    : KitWarning;
