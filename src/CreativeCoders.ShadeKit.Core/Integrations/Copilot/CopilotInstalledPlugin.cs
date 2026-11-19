namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Represents a plugin that is installed in the GitHub Copilot CLI, as <c>copilot plugin list --json</c> reports it.
/// </summary>
/// <remarks>
/// Unverified: the shape rests on the plugin reference of the GitHub Docs (a flat array of
/// <c>{ name, marketplace?, version?, enabled, source, installedFrom? }</c>); it has not been read from a real
/// <c>copilot</c>. A plugin installed directly from a repository, URL, or path is assumed to report no
/// <c>marketplace</c>.
/// </remarks>
/// <param name="Name">The name of the plugin, for example <c>shade-kit</c>.</param>
/// <param name="Marketplace">
/// The name of the marketplace that the plugin was installed from, for example <c>creativecoders-shade-kit</c>, or
/// <see langword="null" /> if <c>copilot</c> reported none, as for a direct install.
/// </param>
/// <param name="Version">
/// The installed version of the plugin, or <see langword="null" /> if <c>copilot</c> reported none.
/// </param>
/// <param name="Enabled"><see langword="true" /> if the plugin is enabled; otherwise, <see langword="false" />.</param>
public sealed record CopilotInstalledPlugin(string Name, string? Marketplace, string? Version, bool Enabled);
