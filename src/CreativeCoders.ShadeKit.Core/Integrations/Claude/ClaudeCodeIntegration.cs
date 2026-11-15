using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Installs, updates, and reads the SHADE kit plugin of Claude Code through the plugin commands of the Claude Code CLI
/// <c>claude</c>.
/// </summary>
/// <remarks>
/// <para>
/// The SHADE kit repository is the plugin marketplace <see cref="MarketplaceName" /> with the plugin
/// <see cref="PluginId" />; both are installed with the scope <c>user</c>. An install or update runs these steps:
/// </para>
/// <list type="number">
/// <item><description>
/// <c>claude plugin list</c> reads whether the plugin is installed with the scope <c>user</c>; an update of a plugin
/// that is not installed stops here, changes nothing, and resolves no source.
/// </description></item>
/// <item><description>
/// <c>claude plugin marketplace list</c> reads the marketplace. A missing marketplace is added. A marketplace with the
/// same repository (compared case-insensitively, as GitHub does) and the same ref, or with the same directory, is
/// kept; directories are compared with a <see cref="DirectoryPathComparer" />, after resolving their symbolic links
/// and case-insensitively on Windows and macOS. A marketplace with the same repository but another ref is removed
/// and added again (the switch). A marketplace with another repository, another kind of source, or another directory
/// is a conflict, and nothing is changed; these rules are the same for every agent. Claude Code stores the ref that was given, so a GitHub marketplace without
/// a ref (the default branch) has another ref than every source with a ref, and refs are compared case-sensitively,
/// as git does.
/// </description></item>
/// <item><description>
/// A kept marketplace is refreshed with <c>claude plugin marketplace update</c>, because a marketplace that follows a
/// branch keeps the commit of its last refresh, and <c>claude plugin update</c> does not refresh it reliably (it skips
/// the refresh when the marketplace was refreshed shortly before). After a change of the marketplace the plugins are
/// listed again, because removing a marketplace also removes its plugins. A plugin that is not installed is installed
/// (and listed once more to read whether it is enabled); otherwise it is updated. The outcome is
/// <see cref="IntegrationOutcome.Installed" /> if the plugin was not installed before the run,
/// <see cref="IntegrationOutcome.Current" /> if the marketplace was kept and the update found the plugin up to date,
/// and <see cref="IntegrationOutcome.Updated" /> otherwise.
/// </description></item>
/// </list>
/// <para>
/// Nothing is rolled back. A failure after the old marketplace was removed is reported as an
/// <see cref="IntegrationIncompleteException" />, and a cancellation from the removal on as an
/// <see cref="IntegrationSwitchCanceledException" />, because running the install again repairs the integration. A
/// disabled plugin stays disabled.
/// </para>
/// </remarks>
public sealed class ClaudeCodeIntegration : IAgentIntegration
{
    /// <summary>
    /// Represents the agent id of Claude Code.
    /// </summary>
    public const string ClaudeAgentId = "claude";

    /// <summary>
    /// Represents the name of the plugin marketplace of SHADE kit.
    /// </summary>
    /// <remarks>The name is <see cref="ShadeKitPlugin.MarketplaceName" />, the same for every agent.</remarks>
    public const string MarketplaceName = ShadeKitPlugin.MarketplaceName;

    /// <summary>
    /// Represents the id of the SHADE kit plugin in Claude Code.
    /// </summary>
    /// <remarks>The id is <see cref="ShadeKitPlugin.Id" />, the same for every agent.</remarks>
    public const string PluginId = ShadeKitPlugin.Id;

    private const string RemoveMarketplaceCommand = "claude plugin marketplace remove " + MarketplaceName;

    private static readonly string[] ListPluginsArguments = ["plugin", "list", "--json"];

    private readonly IClaudePluginCli _claudePluginCli;

    private readonly IClaudeExecutableLocator _claudeExecutableLocator;

    private readonly MarketplaceChangeResolver _marketplaceChangeResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCodeIntegration" /> class that compares directories as the
    /// file system of the current operating system does.
    /// </summary>
    /// <param name="claudePluginCli">The plugin commands of <c>claude</c>.</param>
    /// <param name="claudeExecutableLocator">The locator that detects <c>claude</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="claudePluginCli" /> or <paramref name="claudeExecutableLocator" /> is
    /// <see langword="null" />.
    /// </exception>
    public ClaudeCodeIntegration(IClaudePluginCli claudePluginCli, IClaudeExecutableLocator claudeExecutableLocator)
        : this(claudePluginCli, claudeExecutableLocator, DirectoryPathComparer.ForCurrentOperatingSystem())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeCodeIntegration" /> class with the specified comparer of
    /// directory sources.
    /// </summary>
    /// <param name="claudePluginCli">The plugin commands of <c>claude</c>.</param>
    /// <param name="claudeExecutableLocator">The locator that detects <c>claude</c>.</param>
    /// <param name="directoryPathComparer">
    /// The comparer that decides whether a directory marketplace has the directory of the source.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    internal ClaudeCodeIntegration(IClaudePluginCli claudePluginCli, IClaudeExecutableLocator claudeExecutableLocator,
        DirectoryPathComparer directoryPathComparer)
    {
        _claudePluginCli = Ensure.NotNull(claudePluginCli);
        _claudeExecutableLocator = Ensure.NotNull(claudeExecutableLocator);
        _marketplaceChangeResolver = new MarketplaceChangeResolver(ClaudeAgentId, [RemoveMarketplaceCommand],
            Ensure.NotNull(directoryPathComparer));
    }

    /// <inheritdoc />
    /// <value>The id <see cref="ClaudeAgentId" />.</value>
    public string AgentId => ClaudeAgentId;

    /// <inheritdoc />
    /// <remarks>Claude Code is detected by <c>claude</c> in an absolute directory of the <c>PATH</c>.</remarks>
    public bool IsAvailable()
    {
        return _claudeExecutableLocator.FindClaudeExecutable() is not null;
    }

    /// <inheritdoc />
    /// <exception cref="ClaudeNotFoundException">
    /// <c>claude</c> is not found; the exception names <c>claude plugin list --json</c>, the first command of an
    /// install or update.
    /// </exception>
    public void EnsureAvailable()
    {
        if (!IsAvailable())
        {
            throw new ClaudeNotFoundException(ListPluginsArguments, ClaudeCommandRunner.Tool.NotFoundReason);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The ref and the kit version come from the marketplace <see cref="MarketplaceName" /> if it is a GitHub
    /// marketplace.
    /// </remarks>
    public async Task<InstalledIntegration?> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var plugin = await FindUserPluginAsync(cancellationToken).ConfigureAwait(false);

        if (plugin is null)
        {
            return null;
        }

        var marketplace = FindMarketplace(
            await _claudePluginCli.ListMarketplacesAsync(cancellationToken).ConfigureAwait(false));

        return new InstalledIntegration(AgentId, PluginId, marketplace?.KitVersion, marketplace?.GitHubRef,
            plugin.Enabled);
    }

    /// <inheritdoc />
    public async Task<IntegrationResult> InstallAsync(MarketplaceSource source,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(source);

        var pluginBefore = await FindUserPluginAsync(cancellationToken).ConfigureAwait(false);

        return await ApplyAsync(source, pluginBefore, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IntegrationResult?> UpdateAsync(Func<CancellationToken, Task<MarketplaceSource>> getSource,
        CancellationToken cancellationToken = default)
    {
        Ensure.NotNull(getSource);

        var pluginBefore = await FindUserPluginAsync(cancellationToken).ConfigureAwait(false);

        if (pluginBefore is null)
        {
            return null;
        }

        var source = Ensure.NotNull(await getSource(cancellationToken).ConfigureAwait(false));

        return await ApplyAsync(source, pluginBefore, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IntegrationResult> ApplyAsync(MarketplaceSource source, ClaudeInstalledPlugin? pluginBefore,
        CancellationToken cancellationToken)
    {
        var marketplace = FindMarketplace(
            await _claudePluginCli.ListMarketplacesAsync(cancellationToken).ConfigureAwait(false));

        var change = _marketplaceChangeResolver.Resolve(marketplace, source);

        var previousKitVersion = pluginBefore is null ? null : marketplace?.KitVersion;

        var isRemoved = false;

        try
        {
            if (change == MarketplaceChange.Switch)
            {
                await _claudePluginCli.RemoveMarketplaceAsync(MarketplaceName, cancellationToken)
                    .ConfigureAwait(false);

                isRemoved = true;
            }

            // A kept marketplace that follows a branch would otherwise stay at the commit of its last refresh.
            if (change == MarketplaceChange.Keep)
            {
                await _claudePluginCli.UpdateMarketplaceAsync(MarketplaceName, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _claudePluginCli.AddMarketplaceAsync(source.Text, cancellationToken).ConfigureAwait(false);
            }

            var plugin = change == MarketplaceChange.Keep
                ? pluginBefore
                : await FindUserPluginAsync(cancellationToken).ConfigureAwait(false);

            return plugin is null
                ? await InstallPluginAsync(source, pluginBefore, previousKitVersion, cancellationToken)
                    .ConfigureAwait(false)
                : await UpdatePluginAsync(source, plugin, pluginBefore, previousKitVersion,
                    change == MarketplaceChange.Keep, cancellationToken).ConfigureAwait(false);
        }
        catch (ExternalToolUnavailableException exception) when (isRemoved)
        {
            throw new IntegrationIncompleteException(AgentId, exception);
        }
        catch (OperationCanceledException exception) when (change == MarketplaceChange.Switch &&
                                                           cancellationToken.IsCancellationRequested)
        {
            // A cancellation while the removal runs counts too: the killed claude may have removed the marketplace.
            throw new IntegrationSwitchCanceledException(AgentId, exception);
        }
    }

    private async Task<IntegrationResult> InstallPluginAsync(MarketplaceSource source,
        ClaudeInstalledPlugin? pluginBefore, string? previousKitVersion, CancellationToken cancellationToken)
    {
        await _claudePluginCli.InstallPluginAsync(PluginId, cancellationToken).ConfigureAwait(false);

        var plugin = await FindUserPluginAsync(cancellationToken).ConfigureAwait(false)
                     ?? throw new ClaudeCommandFailedException(ListPluginsArguments, 0,
                         $"claude does not list the plugin '{PluginId}' with the scope " +
                         $"'{ClaudeInstalledPlugin.UserScope}' after it was installed.");

        var outcome = pluginBefore is null ? IntegrationOutcome.Installed : IntegrationOutcome.Updated;

        return IntegrationResult.ForShadeKitPlugin(AgentId, source, outcome, previousKitVersion, plugin.Enabled);
    }

    private async Task<IntegrationResult> UpdatePluginAsync(MarketplaceSource source, ClaudeInstalledPlugin plugin,
        ClaudeInstalledPlugin? pluginBefore, string? previousKitVersion, bool isMarketplaceKept,
        CancellationToken cancellationToken)
    {
        var update = await _claudePluginCli.UpdatePluginAsync(PluginId, cancellationToken).ConfigureAwait(false);

        var outcome = pluginBefore switch
        {
            null => IntegrationOutcome.Installed,
            _ when isMarketplaceKept && update.IsUpToDate => IntegrationOutcome.Current,
            _ => IntegrationOutcome.Updated
        };

        return IntegrationResult.ForShadeKitPlugin(AgentId, source, outcome, previousKitVersion, plugin.Enabled);
    }

    private async Task<ClaudeInstalledPlugin?> FindUserPluginAsync(CancellationToken cancellationToken)
    {
        return FindUserPlugin(await _claudePluginCli.ListPluginsAsync(cancellationToken).ConfigureAwait(false));
    }

    private static ClaudeInstalledPlugin? FindUserPlugin(IEnumerable<ClaudeInstalledPlugin> plugins)
    {
        return plugins.FirstOrDefault(x =>
            string.Equals(x.Id, PluginId, StringComparison.Ordinal)
            && string.Equals(x.Scope, ClaudeInstalledPlugin.UserScope, StringComparison.Ordinal));
    }

    private static AgentMarketplace? FindMarketplace(IEnumerable<ClaudeMarketplace> marketplaces)
    {
        return marketplaces.FirstOrDefault(x => string.Equals(x.Name, MarketplaceName, StringComparison.Ordinal))
            ?.ToAgentMarketplace();
    }
}
