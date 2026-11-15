namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Represents a plugin that is installed in Claude Code, as <c>claude plugin list --json</c> reports it.
/// </summary>
/// <param name="Id">
/// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
/// <c>shade-kit@creativecoders-shade-kit</c>.
/// </param>
/// <param name="Version">
/// The installed version of the plugin, or <see langword="null" /> if <c>claude</c> reported none. Without a
/// <c>version</c> in the plugin manifest, Claude Code uses the first 12 characters of the commit SHA.
/// </param>
/// <param name="Scope">
/// The scope in which the plugin is installed, for example <see cref="UserScope" />, or <see langword="null" /> if
/// <c>claude</c> reported none.
/// </param>
/// <param name="Enabled"><see langword="true" /> if the plugin is enabled; otherwise, <see langword="false" />.</param>
public sealed record ClaudeInstalledPlugin(string Id, string? Version, string? Scope, bool Enabled)
{
    /// <summary>
    /// Represents the scope of a plugin that is installed for the user, in every project.
    /// </summary>
    public const string UserScope = "user";
}
