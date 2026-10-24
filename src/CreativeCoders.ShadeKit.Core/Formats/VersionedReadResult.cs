using CreativeCoders.ShadeKit.Core.Formats.Warnings;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents the result of reading a versioned JSON document.
/// </summary>
/// <typeparam name="T">The type of the document.</typeparam>
/// <param name="Document">The document, migrated in memory to the current format version if necessary.</param>
/// <param name="Warnings">The warnings that came up while reading the document; an empty list if there were none.</param>
public sealed record VersionedReadResult<T>(T Document, IReadOnlyList<FormatWarning> Warnings)
    where T : VersionedDocument;
