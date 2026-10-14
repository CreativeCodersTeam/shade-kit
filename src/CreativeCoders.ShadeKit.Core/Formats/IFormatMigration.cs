using System.Text.Json.Nodes;

namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Represents one step of a migration chain that converts a JSON document of a file type from one format version to
/// the next.
/// </summary>
/// <remarks>
/// A step always migrates from <see cref="FromVersion" /> to <see cref="FromVersion" /> + 1. It changes only the
/// content of the document; <c>schemaVersion</c> and <c>minEngineVersion</c> are updated by the caller.
/// </remarks>
public interface IFormatMigration
{
    /// <summary>
    /// Gets the id of the file type this step migrates.
    /// </summary>
    /// <value>One of the ids defined in <see cref="FormatIds" />, or the id of a custom file type.</value>
    string FormatId { get; }

    /// <summary>
    /// Gets the format version this step migrates from.
    /// </summary>
    /// <value>The source format version. The step produces version <see cref="FromVersion" /> + 1.</value>
    int FromVersion { get; }

    /// <summary>
    /// Migrates the specified document in place from <see cref="FromVersion" /> to the next format version.
    /// </summary>
    /// <param name="document">The root object of the document to migrate.</param>
    void Migrate(JsonObject document);
}
