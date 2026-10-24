using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Represents the record of the shipped templates installed in a repository, stored in
/// <c>.shadekit/templates.lock.json</c>.
/// </summary>
/// <remarks>
/// <c>shade-kit upgrade</c> uses the entries to tell changed templates from unchanged ones. Unknown fields are
/// kept in <see cref="VersionedDocument.ExtensionData" />.
/// </remarks>
public sealed class TemplatesLock : VersionedDocument, IJsonOnDeserialized
{
    /// <summary>
    /// Gets or sets the installed templates.
    /// </summary>
    /// <value>
    /// One entry per installed template. The default is an empty list, which also applies when the field is missing.
    /// A <see langword="null" /> entry makes the file invalid.
    /// </value>
    public List<TemplatesLockEntry> Templates { get; set; } = [];

    void IJsonOnDeserialized.OnDeserialized()
    {
        // RespectNullableAnnotations does not cover list elements, so a null entry is rejected here.
        if (Templates.Any(entry => entry is null))
        {
            throw new JsonException("The 'templates' field contains a null entry.");
        }
    }
}
