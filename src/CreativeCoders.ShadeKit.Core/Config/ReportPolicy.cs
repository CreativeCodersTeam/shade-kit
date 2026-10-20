namespace CreativeCoders.ShadeKit.Core.Config;

/// <summary>
/// Specifies whether the <c>finish</c> phase writes a <c>report.md</c> for a feature.
/// </summary>
public enum ReportPolicy
{
    /// <summary>
    /// The <c>finish</c> phase asks the human whether to write the report. Stored as <c>ask</c>.
    /// </summary>
    Ask,

    /// <summary>
    /// The <c>finish</c> phase always writes the report. Stored as <c>always</c>.
    /// </summary>
    Always,

    /// <summary>
    /// The <c>finish</c> phase never writes the report. Stored as <c>never</c>.
    /// </summary>
    Never
}
