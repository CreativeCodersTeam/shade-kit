using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Config;

/// <summary>
/// Represents the repository configuration stored in <c>.shadekit/config.json</c>.
/// </summary>
/// <remarks>
/// The team owns the values; the engine owns the schema. This model contains only the fields the engine reads so
/// far. All other fields, such as <c>kitVersion</c> or <c>trackers</c>, are kept in
/// <see cref="VersionedDocument.ExtensionData" /> and written back unchanged.
/// </remarks>
public sealed class KitConfig : VersionedDocument
{
    /// <summary>
    /// Gets or sets what read commands do with a versioned file in an older format version.
    /// </summary>
    /// <value>
    /// One of the enumeration values. The default is <see cref="OutdatedFormatOnReadPolicy.MigrateAndWarn" />, which
    /// also applies when the field is missing.
    /// </value>
    public OutdatedFormatOnReadPolicy OutdatedFormatOnRead { get; set; } = OutdatedFormatOnReadPolicy.MigrateAndWarn;
}
