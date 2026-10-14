namespace CreativeCoders.ShadeKit.Core.Formats;

/// <summary>
/// Specifies the kind of a <see cref="FormatWarning" />.
/// </summary>
public enum FormatWarningKind
{
    /// <summary>
    /// The file uses an older format version and was migrated in memory; <c>shade-kit upgrade</c> is needed to
    /// migrate it on disk.
    /// </summary>
    UpgradeNeeded
}
