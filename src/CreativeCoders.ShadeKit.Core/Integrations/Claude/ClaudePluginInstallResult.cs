namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Represents the result of a successful <c>claude plugin install --json</c> command.
/// </summary>
/// <param name="InstalledVersion">
/// The version that was already installed, which <c>claude</c> reports when the plugin was installed before the
/// command, or <see langword="null" /> if it reported none.
/// </param>
/// <param name="AvailableVersion">
/// The version that the marketplace offers, which <c>claude</c> reports when the plugin was installed before the
/// command, or <see langword="null" /> if it reported none.
/// </param>
public sealed record ClaudePluginInstallResult(string? InstalledVersion, string? AvailableVersion);
