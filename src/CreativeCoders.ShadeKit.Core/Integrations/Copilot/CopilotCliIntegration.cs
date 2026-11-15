using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Installs, updates, and reads the SHADE kit plugin of the GitHub Copilot CLI through the plugin commands of
/// <c>copilot</c>.
/// </summary>
/// <remarks>
/// <para>
/// The SHADE kit repository is the plugin marketplace <see cref="ShadeKitPlugin.MarketplaceName" /> with the plugin
/// <see cref="ShadeKitPlugin.Id" />; the Copilot CLI installs both for the user. The plugin counts as installed only
/// if <c>copilot plugin list --json</c> reports the name <see cref="ShadeKitPlugin.Name" /> with the marketplace
/// <see cref="ShadeKitPlugin.MarketplaceName" />; a plugin of the same name from another marketplace or from a direct
/// install does not count. An install or update runs these steps:
/// </para>
/// <list type="number">
/// <item><description>
/// <c>copilot plugin list --json</c> reads whether the plugin is installed; an update of a plugin that is not
/// installed stops here, changes nothing, and resolves no source.
/// </description></item>
/// <item><description>
/// <c>copilot plugin marketplace list --json</c> reads the marketplace, and the
/// <see cref="MarketplaceChangeResolver" /> decides by the rules that are the same for every agent: a missing
/// marketplace is added, one with the source is kept, one with the repository of the source but another ref is
/// switched, and every other one is a conflict that changes nothing.
/// </description></item>
/// <item><description>
/// Added: <c>copilot plugin marketplace add &lt;source&gt;</c>, then the plugins are listed again. Kept:
/// <c>copilot plugin marketplace update</c>, which fetches the catalog again, because a marketplace that follows a
/// branch keeps the commit of its last fetch. Switched: <c>copilot plugin uninstall</c> (only if the plugin is
/// installed), <c>copilot plugin marketplace remove</c> without <c>--force</c>, then
/// <c>copilot plugin marketplace add &lt;source&gt;</c>.
/// </description></item>
/// <item><description>
/// A plugin that is not installed (after a switch it never is) is installed with <c>copilot plugin install</c>;
/// otherwise it is updated with <c>copilot plugin update</c>. Then the plugins are listed again to read whether the
/// plugin is enabled; a plugin that is not listed is a failure.
/// </description></item>
/// </list>
/// <para>
/// The change commands of the Copilot CLI print no JSON, so a change succeeded if its exit code is <c>0</c>, and the
/// engine cannot tell an unchanged plugin from a refreshed one: the outcome is
/// <see cref="IntegrationOutcome.Installed" /> if the plugin was not installed before the run and
/// <see cref="IntegrationOutcome.Updated" /> otherwise, never <see cref="IntegrationOutcome.Current" />.
/// </para>
/// <para>
/// Nothing is rolled back. A failure after the plugin was uninstalled or the old marketplace was removed is reported
/// as an <see cref="IntegrationIncompleteException" />, and a cancellation from the first removal on as an
/// <see cref="IntegrationSwitchCanceledException" />, because running the install again repairs the integration. A
/// disabled plugin stays disabled.
/// </para>
/// <para>
/// Unverified: the commands, the uninstall before <c>copilot plugin marketplace remove</c> (which the GitHub Docs say
/// refuses a marketplace whose plugins are still installed), and the shapes of the list output rest on the GitHub
/// Docs plugin reference and the changelog of the Copilot CLI; none of it has been run against a real
/// <c>copilot</c>.
/// </para>
/// </remarks>
public sealed class CopilotCliIntegration : IAgentIntegration
{
    /// <summary>
    /// Represents the agent id of the GitHub Copilot CLI.
    /// </summary>
    public const string CopilotAgentId = "copilot";

    // Copilot removes no marketplace while plugins of it are installed, so a conflict names the uninstall first.
    private const string UninstallPluginCommand = "copilot plugin uninstall " + ShadeKitPlugin.Id;

    private const string RemoveMarketplaceCommand =
        "copilot plugin marketplace remove " + ShadeKitPlugin.MarketplaceName;

    private static readonly string[] ListPluginsArguments = ["plugin", "list", "--json"];

    private readonly ICopilotPluginCli _copilotPluginCli;

    private readonly ICopilotExecutableLocator _copilotExecutableLocator;

    private readonly MarketplaceChangeResolver _marketplaceChangeResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotCliIntegration" /> class that compares directories as the
    /// file system of the current operating system does.
    /// </summary>
    /// <param name="copilotPluginCli">The plugin commands of <c>copilot</c>.</param>
    /// <param name="copilotExecutableLocator">The locator that detects <c>copilot</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="copilotPluginCli" /> or <paramref name="copilotExecutableLocator" /> is
    /// <see langword="null" />.
    /// </exception>
    public CopilotCliIntegration(ICopilotPluginCli copilotPluginCli,
        ICopilotExecutableLocator copilotExecutableLocator)
        : this(copilotPluginCli, copilotExecutableLocator, DirectoryPathComparer.ForCurrentOperatingSystem())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotCliIntegration" /> class with the specified comparer of
    /// directory sources.
    /// </summary>
    /// <param name="copilotPluginCli">The plugin commands of <c>copilot</c>.</param>
    /// <param name="copilotExecutableLocator">The locator that detects <c>copilot</c>.</param>
    /// <param name="directoryPathComparer">
    /// The comparer that decides whether a directory marketplace has the directory of the source.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    internal CopilotCliIntegration(ICopilotPluginCli copilotPluginCli,
        ICopilotExecutableLocator copilotExecutableLocator, DirectoryPathComparer directoryPathComparer)
    {
        _copilotPluginCli = Ensure.NotNull(copilotPluginCli);
        _copilotExecutableLocator = Ensure.NotNull(copilotExecutableLocator);
        _marketplaceChangeResolver = new MarketplaceChangeResolver(CopilotAgentId,
            [UninstallPluginCommand, RemoveMarketplaceCommand], Ensure.NotNull(directoryPathComparer));
    }

    /// <inheritdoc />
    /// <value>The id <see cref="CopilotAgentId" />.</value>
    public string AgentId => CopilotAgentId;

    /// <inheritdoc />
    /// <remarks>
    /// The Copilot CLI is detected by <c>copilot</c> (on Windows <c>copilot.exe</c> or <c>copilot.cmd</c>) in an
    /// absolute directory of the <c>PATH</c>.
    /// </remarks>
    public bool IsAvailable()
    {
        return _copilotExecutableLocator.FindCopilotExecutable() is not null;
    }

    /// <inheritdoc />
    /// <exception cref="CopilotNotFoundException">
    /// <c>copilot</c> is not found; the exception names <c>copilot plugin list --json</c>, the first command of an
    /// install or update.
    /// </exception>
    public void EnsureAvailable()
    {
        if (!IsAvailable())
        {
            throw new CopilotNotFoundException(ListPluginsArguments, CopilotCommandRunner.Tool.NotFoundReason);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The ref and the kit version come from the marketplace <see cref="ShadeKitPlugin.MarketplaceName" /> if it is a
    /// GitHub marketplace.
    /// </remarks>
    public async Task<InstalledIntegration?> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var plugin = await FindPluginAsync(cancellationToken).ConfigureAwait(false);

        if (plugin is null)
        {
            return null;
        }

        var marketplace = FindMarketplace(
            await _copilotPluginCli.ListMarketplacesAsync(cancellationToken).ConfigureAwait(false));

        return new InstalledIntegration(AgentId, ShadeKitPlugin.Id, marketplace?.KitVersion, marketplace?.GitHubRef,
            plugin.Enabled);
    }

    /// <inheritdoc />
    public async Task<IntegrationResult> InstallAsync(MarketplaceSource source,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(source);

        var pluginBefore = await FindPluginAsync(cancellationToken).ConfigureAwait(false);

        return await ApplyAsync(source, pluginBefore, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IntegrationResult?> UpdateAsync(Func<CancellationToken, Task<MarketplaceSource>> getSource,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(getSource);

        var pluginBefore = await FindPluginAsync(cancellationToken).ConfigureAwait(false);

        if (pluginBefore is null)
        {
            return null;
        }

        var source = Ensure.NotNull(await getSource(cancellationToken).ConfigureAwait(false));

        return await ApplyAsync(source, pluginBefore, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IntegrationResult> ApplyAsync(MarketplaceSource source, CopilotInstalledPlugin? pluginBefore,
        CancellationToken cancellationToken)
    {
        var marketplace = FindMarketplace(
            await _copilotPluginCli.ListMarketplacesAsync(cancellationToken).ConfigureAwait(false));

        var change = _marketplaceChangeResolver.Resolve(marketplace, source);

        var previousKitVersion = pluginBefore is null ? null : marketplace?.KitVersion;

        var isRemoved = false;

        try
        {
            CopilotInstalledPlugin? plugin;

            switch (change)
            {
                case MarketplaceChange.Switch:
                    // copilot refuses to remove a marketplace whose plugins are installed, so the plugin goes first.
                    if (pluginBefore is not null)
                    {
                        await _copilotPluginCli.UninstallPluginAsync(ShadeKitPlugin.Id, cancellationToken)
                            .ConfigureAwait(false);

                        isRemoved = true;
                    }

                    await _copilotPluginCli.RemoveMarketplaceAsync(ShadeKitPlugin.MarketplaceName, cancellationToken)
                        .ConfigureAwait(false);

                    isRemoved = true;

                    await _copilotPluginCli.AddMarketplaceAsync(source.Text, cancellationToken).ConfigureAwait(false);

                    plugin = null;
                    break;

                case MarketplaceChange.Keep:
                    // A kept marketplace that follows a branch would otherwise stay at the commit of its last fetch.
                    await _copilotPluginCli.UpdateMarketplaceAsync(ShadeKitPlugin.MarketplaceName, cancellationToken)
                        .ConfigureAwait(false);

                    plugin = pluginBefore;
                    break;

                default:
                    await _copilotPluginCli.AddMarketplaceAsync(source.Text, cancellationToken).ConfigureAwait(false);

                    plugin = await FindPluginAsync(cancellationToken).ConfigureAwait(false);
                    break;
            }

            return await InstallOrUpdatePluginAsync(source, plugin is null, pluginBefore, previousKitVersion,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalToolUnavailableException exception) when (isRemoved)
        {
            throw new IntegrationIncompleteException(AgentId, exception);
        }
        catch (OperationCanceledException exception) when (change == MarketplaceChange.Switch &&
                                                           cancellationToken.IsCancellationRequested)
        {
            // A cancellation while the first removal runs counts too: the killed copilot may have removed it.
            throw new IntegrationSwitchCanceledException(AgentId, exception);
        }
    }

    private async Task<IntegrationResult> InstallOrUpdatePluginAsync(MarketplaceSource source, bool isInstall,
        CopilotInstalledPlugin? pluginBefore, string? previousKitVersion, CancellationToken cancellationToken)
    {
        if (isInstall)
        {
            await _copilotPluginCli.InstallPluginAsync(ShadeKitPlugin.Id, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _copilotPluginCli.UpdatePluginAsync(ShadeKitPlugin.Id, cancellationToken).ConfigureAwait(false);
        }

        // The change commands print no JSON, so the state is read again.
        var plugin = await FindPluginAsync(cancellationToken).ConfigureAwait(false)
                     ?? throw new CopilotCommandFailedException(ListPluginsArguments, 0,
                         $"copilot does not list the plugin '{ShadeKitPlugin.Name}' of the marketplace " +
                         $"'{ShadeKitPlugin.MarketplaceName}' after it was {(isInstall ? "installed" : "updated")}.");

        var outcome = pluginBefore is null ? IntegrationOutcome.Installed : IntegrationOutcome.Updated;

        return IntegrationResult.ForShadeKitPlugin(AgentId, source, outcome, previousKitVersion, plugin.Enabled);
    }

    private async Task<CopilotInstalledPlugin?> FindPluginAsync(CancellationToken cancellationToken)
    {
        return FindPlugin(await _copilotPluginCli.ListPluginsAsync(cancellationToken).ConfigureAwait(false));
    }

    private static CopilotInstalledPlugin? FindPlugin(IEnumerable<CopilotInstalledPlugin> plugins)
    {
        return plugins.FirstOrDefault(x =>
            string.Equals(x.Name, ShadeKitPlugin.Name, StringComparison.Ordinal)
            && string.Equals(x.Marketplace, ShadeKitPlugin.MarketplaceName, StringComparison.Ordinal));
    }

    private static AgentMarketplace? FindMarketplace(IEnumerable<CopilotMarketplace> marketplaces)
    {
        return marketplaces
            .FirstOrDefault(x => string.Equals(x.Name, ShadeKitPlugin.MarketplaceName, StringComparison.Ordinal))
            ?.ToAgentMarketplace();
    }
}
