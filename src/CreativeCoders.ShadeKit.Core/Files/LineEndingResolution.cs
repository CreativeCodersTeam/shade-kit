using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Represents the line ending with which SHADE kit writes a file, together with the warnings that occurred while it
/// was determined.
/// </summary>
/// <param name="LineEnding">One of the enumeration values that specifies the line ending for the file.</param>
/// <param name="Warnings">
/// The warnings that occurred while the line ending was determined, for example an
/// <see cref="EditorConfigUnreadableWarning" /> or an <see cref="EditorConfigInvalidWarning" />; empty if there were
/// none.
/// </param>
public sealed record LineEndingResolution(LineEnding LineEnding, IReadOnlyList<KitWarning> Warnings);
