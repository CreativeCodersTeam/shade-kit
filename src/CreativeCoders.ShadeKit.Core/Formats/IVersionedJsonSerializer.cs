using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Converts versioned JSON documents between their text and their object form, checking and migrating the format
/// version on the way in and stamping it on the way out.
/// </summary>
public interface IVersionedJsonSerializer
{
    /// <summary>
    /// Parses the specified JSON text into a document of the specified file type.
    /// </summary>
    /// <remarks>
    /// An outdated document is migrated in memory if <paramref name="migrateOutdated" /> is <see langword="true" />.
    /// Unknown fields end up in <see cref="VersionedDocument.ExtensionData" />.
    /// </remarks>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="json">The JSON text to parse.</param>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <param name="filePath">The path of the file the text was read from, used in exceptions and warnings.</param>
    /// <param name="migrateOutdated">
    /// <see langword="true" /> to migrate a document in an older format version in memory and report a
    /// <see cref="FormatWarningKind.UpgradeNeeded" /> warning; otherwise, <see langword="false" />.
    /// </param>
    /// <returns>The document and the warnings that came up while reading it.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="json" />, <paramref name="formatId" />, or <paramref name="filePath" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">No file type with the id <paramref name="formatId" /> is registered.</exception>
    /// <exception cref="InvalidKitFileException">
    /// The text is not a valid document of the file type, or a migration step failed on its content.
    /// </exception>
    /// <exception cref="FormatTooNewException">The document uses a format version newer than the engine supports.</exception>
    /// <exception cref="UpgradeNeededException">
    /// The document uses an older format version and <paramref name="migrateOutdated" /> is <see langword="false" />.
    /// </exception>
    VersionedReadResult<T> Deserialize<T>(string json, string formatId, string filePath, bool migrateOutdated)
        where T : VersionedDocument;

    /// <summary>
    /// Converts the specified document of the specified file type into JSON text.
    /// </summary>
    /// <remarks>
    /// Writes the current <c>schemaVersion</c> and <c>minEngineVersion</c> first, whatever <paramref name="document" />
    /// holds, then the known and the unknown fields; indented with two spaces, with LF line endings and a final line
    /// break.
    /// </remarks>
    /// <typeparam name="T">The type of the document.</typeparam>
    /// <param name="document">The document to convert.</param>
    /// <param name="formatId">The id of the file type, for example <see cref="FormatIds.Config" />.</param>
    /// <returns>The JSON text of the document.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document" /> or <paramref name="formatId" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">No file type with the id <paramref name="formatId" /> is registered.</exception>
    string Serialize<T>(T document, string formatId)
        where T : VersionedDocument;
}
