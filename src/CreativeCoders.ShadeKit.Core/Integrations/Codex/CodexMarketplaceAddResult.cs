namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Represents the result of <c>codex plugin marketplace add --json</c>.
/// </summary>
/// <remarks>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/marketplace_cmd.rs</c>,
/// <c>JsonMarketplaceAddOutput</c>): the output is one JSON object <c>{ marketplaceName, installedRoot, alreadyAdded }</c>.
/// </remarks>
/// <param name="MarketplaceName">
/// The name of the added marketplace, taken from its <c>marketplace.json</c>, for example
/// <c>creativecoders-shade-kit</c>.
/// </param>
/// <param name="InstalledRoot">The directory that holds the marketplace.</param>
/// <param name="AlreadyAdded">
/// <see langword="true" /> if <c>codex</c> already had a marketplace with exactly this source; otherwise,
/// <see langword="false" />. FACT (same tag, <c>codex-rs/core-plugins/src/marketplace_add.rs</c> and
/// <c>marketplace_add/metadata.rs</c>, <c>matches_config</c>): exactly this source means the same source type, the same
/// source, the same ref, and the same sparse paths in the user's <c>config.toml</c>; in that case <c>codex</c> neither
/// fetches nor changes the marketplace.
/// </param>
public sealed record CodexMarketplaceAddResult(string MarketplaceName, string InstalledRoot, bool AlreadyAdded);
