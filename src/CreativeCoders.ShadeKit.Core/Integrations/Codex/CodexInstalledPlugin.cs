namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Represents a plugin in the <c>installed</c> list of <c>codex plugin list --json</c>.
/// </summary>
/// <remarks>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/plugin_cmd.rs</c>,
/// <c>JsonPluginListOutput</c> and <c>PluginListEntry</c>): the output is one JSON object
/// <c>{ installed: [...], available: [...] }</c>; <c>installed</c> holds only entries whose <c>installed</c> is
/// <see langword="true" />, and <c>available</c> is empty unless <c>--available</c> is passed. An entry is
/// <c>{ pluginId, name, marketplaceName, version, installed, enabled, source, marketplaceSource?, installPolicy,
/// authPolicy }</c>, where <c>pluginId</c> is <c>&lt;name&gt;@&lt;marketplace&gt;</c> and <c>version</c> may be
/// <see langword="null" />.
/// </para>
/// <para>
/// FACT (same tag, <c>codex-rs/core-plugin-common/src/installed.rs</c>, <c>codex-rs/core-plugins/src/store.rs</c>): a
/// plugin whose manifest has no <c>version</c>, such as SHADE kit, is installed as the version <c>local</c>. The
/// version is therefore never used as the kit version; the kit version comes from the marketplace ref.
/// </para>
/// </remarks>
/// <param name="PluginId">
/// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
/// <c>shade-kit@creativecoders-shade-kit</c>.
/// </param>
/// <param name="Name">The name of the plugin, for example <c>shade-kit</c>.</param>
/// <param name="MarketplaceName">
/// The name of the marketplace that the plugin comes from, for example <c>creativecoders-shade-kit</c>.
/// </param>
/// <param name="Version">
/// The installed version of the plugin, for example <c>local</c>, or <see langword="null" /> if <c>codex</c> reported
/// none.
/// </param>
/// <param name="Installed">
/// <see langword="true" /> if <c>codex</c> reports the plugin as installed; otherwise, <see langword="false" />.
/// </param>
/// <param name="Enabled"><see langword="true" /> if the plugin is enabled; otherwise, <see langword="false" />.</param>
public sealed record CodexInstalledPlugin(
    string PluginId,
    string Name,
    string MarketplaceName,
    string? Version,
    bool Installed,
    bool Enabled);
