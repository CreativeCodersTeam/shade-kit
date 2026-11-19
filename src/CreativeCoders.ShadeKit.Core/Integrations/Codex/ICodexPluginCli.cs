using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Runs the plugin and marketplace commands of the OpenAI Codex CLI <c>codex</c> and reads their output.
/// </summary>
/// <remarks>
/// <para>
/// Every command runs with <c>--json</c> and must print one JSON object of the expected shape; unknown fields are
/// ignored. Plugins and marketplaces of the Codex CLI are installed for the user (in <c>$CODEX_HOME</c>, by default
/// <c>~/.codex</c>), so they are available in every project. The commands run outside any repository, so nothing is
/// written into one.
/// </para>
/// <para>
/// A command fails with a <see cref="CodexCommandFailedException" /> if <c>codex</c> exits with a non-zero exit code
/// or prints output that is not one JSON object of the expected shape.
/// </para>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/plugin_cmd.rs</c> and
/// <c>codex-rs/cli/src/marketplace_cmd.rs</c>): the subcommands, their arguments, the <c>--json</c> option of each, and
/// the output shapes. There is no <c>codex plugin update</c> and no enable or disable subcommand. Unverified: none of it
/// has been run against a real <c>codex</c>, and whether the commands need a signed-in account.
/// </para>
/// </remarks>
public interface ICodexPluginCli
{
    /// <summary>
    /// Lists the installed plugins, with <c>codex plugin list --json</c>.
    /// </summary>
    /// <remarks>
    /// Only the <c>installed</c> list of the output is read; the <c>available</c> list is empty without
    /// <c>--available</c> and is not read.
    /// </remarks>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the installed plugins in the order
    /// <c>codex</c> reported them.
    /// </returns>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IReadOnlyList<CodexInstalledPlugin>> ListPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the plugin marketplaces that the Codex CLI knows, with <c>codex plugin marketplace list --json</c>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the marketplaces in the order
    /// <c>codex</c> reported them.
    /// </returns>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IReadOnlyList<CodexMarketplace>> ListMarketplacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a plugin marketplace, with <c>codex plugin marketplace add &lt;source&gt; [--ref &lt;ref&gt;] --json</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A GitHub source is passed as <c>owner/repo</c>, with <c>--ref &lt;ref&gt;</c> if it has a ref; a directory
    /// source is passed as its absolute path without <c>--ref</c>. The marketplace is registered under the
    /// <c>name</c> of its <c>marketplace.json</c>.
    /// </para>
    /// <para>
    /// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>,
    /// <c>codex-rs/core-plugins/src/marketplace_add.rs</c>): a marketplace with exactly this source is not added again
    /// (<see cref="CodexMarketplaceAddResult.AlreadyAdded" />), and a marketplace of the same name with another
    /// source is refused with an error and left unchanged. <c>codex</c> splits a source at its last <c>#</c> into
    /// source and ref, also a local path (<c>marketplace_add/source.rs</c>, <c>split_source_ref</c>), so a directory
    /// path that contains <c>#</c> is refused before <c>codex</c> starts.
    /// </para>
    /// </remarks>
    /// <param name="source">The source of the marketplace.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the added marketplace and whether
    /// it was already added with this source.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// The ref of <paramref name="source" /> starts with <c>-</c>, so <c>codex</c> would read it as an option.
    /// </exception>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">
    /// The command fails or its output cannot be read, for example because a marketplace of the same name has another
    /// source; or the directory path contains <c>#</c>, and nothing is started.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<CodexMarketplaceAddResult> AddMarketplaceAsync(MarketplaceSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a Git marketplace again from its configured source and ref, with
    /// <c>codex plugin marketplace upgrade &lt;name&gt; --json</c>.
    /// </summary>
    /// <remarks>
    /// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/core-plugins/src/manager.rs</c>,
    /// <c>upgrade_configured_marketplaces_for_config_with_mode</c>, and <c>codex-rs/cli/src/marketplace_cmd.rs</c>,
    /// <c>print_upgrade_outcome_json</c>): a named marketplace that is not a Git marketplace is an error, an upgrade
    /// error is written to standard error and fails the command, and an upgrade that changed the marketplace reinstalls
    /// its configured plugins.
    /// </remarks>
    /// <param name="name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>codex</c> would read it as an option.
    /// </exception>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">
    /// The command fails or its output cannot be read, for example because the marketplace is not a Git marketplace.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task UpgradeMarketplaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a plugin marketplace, with <c>codex plugin marketplace remove &lt;name&gt; --json</c>.
    /// </summary>
    /// <remarks>
    /// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>,
    /// <c>codex-rs/core-plugins/src/marketplace_remove.rs</c>): the command removes the entry in the user's
    /// <c>config.toml</c> and the marketplace directory, does not check for installed plugins, and refuses a
    /// marketplace that another configuration layer names.
    /// </remarks>
    /// <param name="name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>codex</c> would read it as an option.
    /// </exception>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">
    /// The command fails or its output cannot be read, for example because no marketplace has the name.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a plugin from a configured marketplace, with <c>codex plugin add &lt;id&gt; --json</c>.
    /// </summary>
    /// <remarks>
    /// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/core-plugins/src/store.rs</c> and
    /// <c>codex-rs/core-plugins/src/manager.rs</c>, <c>install_resolved_plugin</c>): an installed plugin is replaced
    /// by a fresh copy from the marketplace, and the plugin is enabled afterwards, also if it was disabled.
    /// </remarks>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>codex</c> would read it as an option.
    /// </exception>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">
    /// The command fails or its output cannot be read, for example because the marketplace has no such plugin.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task AddPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a plugin and removes its cached copy, with <c>codex plugin remove &lt;id&gt; --json</c>.
    /// </summary>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>codex</c> would read it as an option.
    /// </exception>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task RemovePluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
