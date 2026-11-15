namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Represents the result of a successful <c>claude plugin update --json</c> command.
/// </summary>
/// <param name="UpdateOutcome">
/// The <c>updateOutcome</c> that <c>claude</c> reported, for example <see cref="UpdatedOutcome" /> or
/// <see cref="UpToDateOutcome" />, or <see langword="null" /> if it reported none.
/// </param>
/// <param name="OldVersion">
/// The version that was installed before the command, or <see langword="null" /> if <c>claude</c> reported none.
/// </param>
/// <param name="NewVersion">
/// The version that is installed after the command, or <see langword="null" /> if <c>claude</c> reported none.
/// </param>
public sealed record ClaudePluginUpdateResult(string? UpdateOutcome, string? OldVersion, string? NewVersion)
{
    /// <summary>
    /// Represents the update outcome of a plugin that was updated to a new version.
    /// </summary>
    public const string UpdatedOutcome = "updated";

    /// <summary>
    /// Represents the update outcome of a plugin that already had the latest version.
    /// </summary>
    public const string UpToDateOutcome = "up_to_date";

    /// <summary>
    /// Gets a value that indicates whether the plugin already had the latest version, so nothing was changed.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if <see cref="UpdateOutcome" /> is <see cref="UpToDateOutcome" />; otherwise,
    /// <see langword="false" />.
    /// </value>
    public bool IsUpToDate => string.Equals(UpdateOutcome, UpToDateOutcome, StringComparison.Ordinal);
}
