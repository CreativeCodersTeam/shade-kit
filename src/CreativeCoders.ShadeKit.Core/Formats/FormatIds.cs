namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Defines the logical ids of the versioned JSON file types that SHADE kit reads and writes.
/// </summary>
/// <remarks>
/// Each file type has its own format version and its own migration chain. The ids are part of the agent contract,
/// for example as keys of the <c>formats</c> object returned by <c>shade-kit version --json</c>.
/// </remarks>
public static class FormatIds
{
    /// <summary>
    /// Represents the format id of <c>.shadekit/config.json</c>.
    /// </summary>
    public const string Config = "config";

    /// <summary>
    /// Represents the format id of <c>.shadekit/gates.json</c>.
    /// </summary>
    public const string Gates = "gates";

    /// <summary>
    /// Represents the format id of <c>.shadekit/features/&lt;id&gt;/feature.json</c>.
    /// </summary>
    public const string Feature = "feature";

    /// <summary>
    /// Represents the format id of <c>.shadekit/features/current-feature.json</c>.
    /// </summary>
    public const string CurrentFeature = "current-feature";

    /// <summary>
    /// Represents the format id of <c>.shadekit/templates.lock.json</c>.
    /// </summary>
    public const string TemplatesLock = "templates-lock";

    /// <summary>
    /// Represents the format id of the user-level record of installed agent integrations.
    /// </summary>
    public const string Integrations = "integrations";
}
