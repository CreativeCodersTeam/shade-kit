using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Runs the plugin and marketplace commands of the Claude Code CLI <c>claude</c> and reads their JSON output.
/// </summary>
/// <remarks>
/// <para>
/// Every command runs with <c>--json</c>; commands that change the configuration use the scope <c>user</c>, so the
/// plugins are available in every project. The commands run outside any repository, so nothing is written into one.
/// </para>
/// <para>
/// A command fails with a <see cref="ClaudeCommandFailedException" /> if <c>claude</c> exits with a non-zero exit
/// code, reports an outcome other than <c>ok</c>, or prints output that does not have the expected JSON shape;
/// unknown fields are ignored.
/// </para>
/// </remarks>
public interface IClaudePluginCli
{
    /// <summary>
    /// Lists the plugin marketplaces that Claude Code knows, with <c>claude plugin marketplace list --json</c>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the marketplaces in the order
    /// <c>claude</c> reported them.
    /// </returns>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IReadOnlyList<ClaudeMarketplace>> ListMarketplacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a plugin marketplace for the user, with <c>claude plugin marketplace add &lt;source&gt; --scope user
    /// --json</c>.
    /// </summary>
    /// <remarks>
    /// Adding a marketplace that already exists with the same source succeeds without a change.
    /// </remarks>
    /// <param name="source">
    /// The source of the marketplace, for example <c>owner/repo#v1.2.0</c> or the absolute path of a local directory.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>claude</c> would read it as an option.
    /// </exception>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task AddMarketplaceAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a plugin marketplace, with <c>claude plugin marketplace remove &lt;name&gt; --json</c>.
    /// </summary>
    /// <param name="name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>claude</c> would read it as an option.
    /// </exception>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">
    /// The command fails, for example because no marketplace has the name, or its output cannot be read.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes a plugin marketplace from its source, with <c>claude plugin marketplace update &lt;name&gt;
    /// --json</c>.
    /// </summary>
    /// <remarks>
    /// A marketplace that follows a branch is brought to the latest commit of that branch, so a following
    /// <see cref="UpdatePluginAsync" /> or <see cref="InstallPluginAsync" /> sees the new content. Without the refresh,
    /// <c>claude plugin update</c> may report the plugin as up to date with the content of an older commit.
    /// </remarks>
    /// <param name="name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>claude</c> would read it as an option.
    /// </exception>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">
    /// The command fails, for example because no marketplace has the name or its source cannot be fetched, or its
    /// output cannot be read.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task UpdateMarketplaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the plugins that are installed in Claude Code, with <c>claude plugin list --json</c>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the installed plugins of every
    /// scope in the order <c>claude</c> reported them.
    /// </returns>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IReadOnlyList<ClaudeInstalledPlugin>> ListPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a plugin for the user, with <c>claude plugin install &lt;id&gt; --scope user --json</c>.
    /// </summary>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the versions that <c>claude</c>
    /// reported.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>claude</c> would read it as an option.
    /// </exception>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<ClaudePluginInstallResult> InstallPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a plugin that is installed for the user, with <c>claude plugin update &lt;id&gt; --scope user
    /// --json</c>.
    /// </summary>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the update outcome and the
    /// versions that <c>claude</c> reported.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>claude</c> would read it as an option.
    /// </exception>
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="ClaudeCommandFailedException">
    /// The command fails, for example because the plugin is not installed, or its output cannot be read.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<ClaudePluginUpdateResult> UpdatePluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
