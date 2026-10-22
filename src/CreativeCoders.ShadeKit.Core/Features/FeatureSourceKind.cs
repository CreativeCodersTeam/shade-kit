namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Specifies the kind of input from which a feature was created.
/// </summary>
public enum FeatureSourceKind
{
    /// <summary>
    /// The feature was created from free text. Stored as <c>text</c>.
    /// </summary>
    Text,

    /// <summary>
    /// The feature was created from an absolute <c>http</c> or <c>https</c> URL, for example an issue URL. Stored as
    /// <c>url</c>.
    /// </summary>
    Url,

    /// <summary>
    /// The feature was created from the content of a file. Stored as <c>file</c>.
    /// </summary>
    File
}
