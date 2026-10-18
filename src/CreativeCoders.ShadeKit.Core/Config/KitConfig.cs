using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Config;

/// <summary>
/// Represents the repository configuration stored in <c>.shadekit/config.json</c>.
/// </summary>
/// <remarks>
/// The team owns the values; the engine owns the schema. This model holds the fields of format version <c>1</c>.
/// Fields the engine does not know are kept in <see cref="VersionedDocument.ExtensionData" /> and written back
/// unchanged. The fields are written in the order in which the properties are declared, after
/// <see cref="VersionedDocument.SchemaVersion" /> and <see cref="VersionedDocument.MinEngineVersion" />.
/// </remarks>
public sealed class KitConfig : VersionedDocument, IJsonOnDeserialized
{
    /// <summary>
    /// Gets or sets the kit version that initialised or last upgraded the repository.
    /// </summary>
    /// <value>
    /// A semantic version string, or <see langword="null" /> if the field is missing. The value is informational only;
    /// format checks use <see cref="VersionedDocument.SchemaVersion" />. A <see langword="null" /> value is not written.
    /// </value>
    public string? KitVersion { get; set; }

    /// <summary>
    /// Gets or sets the workflow profile that <c>shade-kit feature new</c> uses when none is given.
    /// </summary>
    /// <value>
    /// The name of a workflow profile, for example <c>feature</c>, <c>bugfix</c>, or <c>quick</c>. The default is
    /// <c>feature</c>, which also applies when the field is missing.
    /// </value>
    public string DefaultProfile { get; set; } = "feature";

    /// <summary>
    /// Gets or sets what read commands do with a versioned file in an older format version.
    /// </summary>
    /// <value>
    /// One of the enumeration values. The default is <see cref="OutdatedFormatOnReadPolicy.MigrateAndWarn" />, which
    /// also applies when the field is missing.
    /// </value>
    public OutdatedFormatOnReadPolicy OutdatedFormatOnRead { get; set; } = OutdatedFormatOnReadPolicy.MigrateAndWarn;

    /// <summary>
    /// Gets or sets the mapping from issue URL host to the id prefix of feature ids created from such an issue.
    /// </summary>
    /// <value>
    /// The id prefixes keyed by host name, for example <c>gh</c> for <c>github.com</c>, or <see langword="null" /> if
    /// the field is missing. Host names are written exactly as given. A <see langword="null" /> value is not written; a
    /// host with a <see langword="null" /> id prefix makes the file invalid.
    /// </value>
    public Dictionary<string, string>? Trackers { get; set; }

    /// <summary>
    /// Gets or sets whether the <c>finish</c> phase writes a <c>report.md</c> for a feature.
    /// </summary>
    /// <value>
    /// One of the enumeration values. The default is <see cref="ReportPolicy.Ask" />, which also applies when the
    /// field is missing.
    /// </value>
    public ReportPolicy Report { get; set; } = ReportPolicy.Ask;

    /// <summary>
    /// Gets or sets the minimum coverage that the <c>verify</c> phase requires.
    /// </summary>
    /// <value>
    /// The minimum coverage, or <see langword="null" /> if the field is missing, which means that
    /// <c>verify</c> only measures and records the coverage. A <see langword="null" /> value is not written; a number
    /// beyond the range of <see cref="double" /> makes the file invalid.
    /// </value>
    public double? CoverageThreshold { get; set; }

    void IJsonOnDeserialized.OnDeserialized()
    {
        // RespectNullableAnnotations does not cover dictionary values, so a null id prefix is rejected here.
        if (Trackers?.Values.Any(prefix => prefix is null) == true)
        {
            throw new JsonException("The 'trackers' field contains a null id prefix.");
        }

        // A number beyond the double range is read as infinity, which cannot be written back.
        if (CoverageThreshold is { } threshold && !double.IsFinite(threshold))
        {
            throw new JsonException("The 'coverageThreshold' field is out of range.");
        }
    }
}
