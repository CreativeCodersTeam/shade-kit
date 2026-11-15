using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Runs the plugin and marketplace commands of the GitHub Copilot CLI <c>copilot</c> and reads their output.
/// </summary>
/// <remarks>
/// <para>
/// The list commands run with <c>--json</c>. The commands that change the configuration have no <c>--json</c>; they
/// succeed with the exit code <c>0</c>, and a caller that needs the new state lists it again. Plugins and
/// marketplaces of the Copilot CLI are installed for the user, so they are available in every project. The commands
/// run outside any repository, so nothing is written into one.
/// </para>
/// <para>
/// A command fails with a <see cref="CopilotCommandFailedException" /> if <c>copilot</c> exits with a non-zero exit
/// code or a list command prints output that is not one JSON array of the expected shape; unknown fields are ignored.
/// </para>
/// <para>
/// Unverified: the commands, their missing <c>--json</c>, and the shape of <c>copilot plugin list --json</c> rest on
/// the GitHub Docs plugin reference; the shape of <c>copilot plugin marketplace list --json</c> is not documented.
/// None of it has been run against a real <c>copilot</c>.
/// </para>
/// </remarks>
public interface ICopilotPluginCli
{
    /// <summary>
    /// Lists the plugin marketplaces that the Copilot CLI knows, with <c>copilot plugin marketplace list --json</c>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the marketplaces in the order
    /// <c>copilot</c> reported them.
    /// </returns>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IReadOnlyList<CopilotMarketplace>> ListMarketplacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a plugin marketplace, with <c>copilot plugin marketplace add &lt;source&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The marketplace is registered under the <c>name</c> of its <c>marketplace.json</c>.
    /// </remarks>
    /// <param name="source">
    /// The source of the marketplace, for example <c>owner/repo#v1.2.0</c>, <c>owner/repo</c>, or the absolute path
    /// of a local directory.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="source" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>copilot</c> would read it as an option.
    /// </exception>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">The command fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task AddMarketplaceAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a plugin marketplace, with <c>copilot plugin marketplace remove &lt;name&gt;</c>.
    /// </summary>
    /// <remarks>
    /// The command runs without <c>--force</c>. Unverified (GitHub Docs plugin reference): <c>copilot</c> refuses to
    /// remove a marketplace while plugins of it are installed, so they must be uninstalled first.
    /// </remarks>
    /// <param name="name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>copilot</c> would read it as an option.
    /// </exception>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">
    /// The command fails, for example because no marketplace has the name or plugins of it are still installed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task RemoveMarketplaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the plugin catalog of a marketplace again from its source, with
    /// <c>copilot plugin marketplace update &lt;name&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Unverified: that a following <see cref="UpdatePluginAsync" /> then installs the content of the latest commit of
    /// a marketplace that follows a branch.
    /// </remarks>
    /// <param name="name">The name of the marketplace, for example <c>creativecoders-shade-kit</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>copilot</c> would read it as an option.
    /// </exception>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">
    /// The command fails, for example because no marketplace has the name or its source cannot be fetched.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task UpdateMarketplaceAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the plugins that are installed in the Copilot CLI, with <c>copilot plugin list --json</c>.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the installed plugins in the order
    /// <c>copilot</c> reported them.
    /// </returns>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">The command fails or its output cannot be read.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task<IReadOnlyList<CopilotInstalledPlugin>> ListPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs a plugin from a marketplace, with <c>copilot plugin install &lt;id&gt;</c>.
    /// </summary>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>copilot</c> would read it as an option.
    /// </exception>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">The command fails.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task InstallPluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an installed plugin, with <c>copilot plugin update &lt;id&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Unverified: that <c>copilot plugin update</c> takes the id <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>; the
    /// GitHub Docs name its argument <c>NAME</c>.
    /// </remarks>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>copilot</c> would read it as an option.
    /// </exception>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">
    /// The command fails, for example because the plugin is not installed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task UpdatePluginAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls an installed plugin, with <c>copilot plugin uninstall &lt;id&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Unverified: that <c>copilot plugin uninstall</c> takes the id <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>; the
    /// GitHub Docs name its argument <c>NAME</c>.
    /// </remarks>
    /// <param name="pluginId">
    /// The id of the plugin in the form <c>&lt;plugin&gt;@&lt;marketplace&gt;</c>, for example
    /// <c>shade-kit@creativecoders-shade-kit</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="pluginId" /> is <see langword="null" />, empty, or whitespace, or starts with <c>-</c>, so
    /// <c>copilot</c> would read it as an option.
    /// </exception>
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">
    /// The command fails, for example because the plugin is not installed.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    Task UninstallPluginAsync(string pluginId, CancellationToken cancellationToken = default);
}
