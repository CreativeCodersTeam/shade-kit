using System.Diagnostics;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Installs, updates, and reads the SHADE kit plugin of the OpenAI Codex CLI through the plugin commands of
/// <c>codex</c>.
/// </summary>
/// <remarks>
/// <para>
/// The SHADE kit repository is the plugin marketplace <see cref="ShadeKitPlugin.MarketplaceName" /> with the plugin
/// <see cref="ShadeKitPlugin.Id" />; the Codex CLI installs both for the user. The plugin counts as installed only if
/// the <c>installed</c> list of <c>codex plugin list --json</c> has an entry with the name
/// <see cref="ShadeKitPlugin.Name" />, the marketplace <see cref="ShadeKitPlugin.MarketplaceName" />, and
/// <c>installed</c> <see langword="true" />; a plugin of the same name from another marketplace does not count. An
/// install or update runs these steps:
/// </para>
/// <list type="number">
/// <item><description>
/// <c>codex plugin list --json</c> reads whether the plugin is installed; an update of a plugin that is not installed
/// stops here, changes nothing, and resolves no source.
/// </description></item>
/// <item><description>
/// <c>codex plugin marketplace list --json</c> reads the marketplace, and the
/// <see cref="MarketplaceChangeResolver" /> decides by the rules that are the same for every agent: a missing
/// marketplace is added, a directory marketplace with the directory of the source is kept, a GitHub marketplace with
/// the repository of the source is checked as described below, and every other one is a conflict that changes nothing.
/// </description></item>
/// <item><description>
/// Added: <c>codex plugin marketplace add</c>. Kept directory: nothing, because <c>codex</c> refuses to upgrade a
/// marketplace that is not a Git marketplace and <c>codex plugin add</c> copies the directory again anyway. GitHub
/// marketplace with the repository of the source: <c>codex plugin marketplace add</c> with the source checks the ref.
/// If <c>codex</c> reports the marketplace as already added, it has the ref and is kept, and
/// <c>codex plugin marketplace upgrade</c> fetches it again, because a marketplace that follows a branch keeps the
/// commit of its last fetch. If the add fails with a non-zero exit code, the marketplace has another source and is
/// switched: <c>codex plugin remove</c> (only if the plugin is installed), <c>codex plugin marketplace remove</c>,
/// then <c>codex plugin marketplace add</c>.
/// </description></item>
/// <item><description>
/// <c>codex plugin add</c> installs the plugin or replaces the installed copy, because <c>codex</c> has no
/// <c>plugin update</c>. Then the plugins are listed again to read whether the plugin is enabled; a plugin that is not
/// listed is a failure.
/// </description></item>
/// </list>
/// <para>
/// Why the ref is checked with an add: <c>codex</c> reports a marketplace source only as
/// <c>{ sourceType, source }</c>, without its ref, so the <see cref="MarketplaceChangeResolver" /> cannot tell a kept
/// ref from another one. <c>codex plugin marketplace add</c> answers that question: with exactly the configured source
/// and ref it returns <c>alreadyAdded</c> without fetching or changing anything, and for a marketplace of the same name
/// with another source or ref it fails without changing anything. A failure for another reason, for example the
/// network, is not told apart; the switch that follows then fails at its add, which is reported as an incomplete
/// integration that a new run repairs. FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>:
/// <c>codex-rs/cli/src/plugin_cmd.rs</c> <c>JsonMarketplaceSource</c>; <c>codex-rs/core-plugins/src/marketplace_add.rs</c>
/// and <c>marketplace_add/metadata.rs</c>; <c>codex-rs/core-plugins/src/manager.rs</c>
/// <c>upgrade_configured_marketplaces_for_config_with_mode</c>). Because the ref is not reported, the kit version
/// before a switch is unknown, and <see cref="GetInstalledAsync" /> reports neither ref nor kit version.
/// </para>
/// <para>
/// The outcome is <see cref="IntegrationOutcome.Installed" /> if the plugin was not installed before the run and
/// <see cref="IntegrationOutcome.Updated" /> otherwise, never <see cref="IntegrationOutcome.Current" />, because
/// <c>codex</c> reports no up-to-date state for a plugin. <c>codex plugin add</c> enables the plugin, also a disabled
/// one (FACT, same tag, <c>codex-rs/core-plugins/src/manager.rs</c> <c>install_resolved_plugin</c>); the result
/// reports the state that <c>codex</c> lists afterwards.
/// </para>
/// <para>
/// Nothing is rolled back. A failure after the plugin or the old marketplace was removed is reported as an
/// <see cref="IntegrationIncompleteException" />, and a cancellation from the first removal on as an
/// <see cref="IntegrationSwitchCanceledException" />, because running the install again repairs the integration.
/// </para>
/// <para>
/// Unverified: none of the commands has been run against a real <c>codex</c>; that a tag works as <c>--ref</c>; that
/// the commands work without a signed-in account; the exit code of a failed command.
/// </para>
/// </remarks>
public sealed class CodexCliIntegration : AgentIntegrationBase<CodexInstalledPlugin>
{
    /// <summary>
    /// Represents the agent id of the OpenAI Codex CLI.
    /// </summary>
    public const string CodexAgentId = "codex";

    private const string RemoveMarketplaceCommand =
        "codex plugin marketplace remove " + ShadeKitPlugin.MarketplaceName;

    private static readonly string[] ListPluginsArguments = ["plugin", "list", "--json"];

    private readonly ICodexPluginCli _codexPluginCli;

    private readonly ICodexExecutableLocator _codexExecutableLocator;

    private readonly MarketplaceChangeResolver _marketplaceChangeResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexCliIntegration" /> class that compares directories as the
    /// file system of the current operating system does.
    /// </summary>
    /// <param name="codexPluginCli">The plugin commands of <c>codex</c>.</param>
    /// <param name="codexExecutableLocator">The locator that detects <c>codex</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="codexPluginCli" /> or <paramref name="codexExecutableLocator" /> is <see langword="null" />.
    /// </exception>
    public CodexCliIntegration(ICodexPluginCli codexPluginCli, ICodexExecutableLocator codexExecutableLocator)
        : this(codexPluginCli, codexExecutableLocator, DirectoryPathComparer.ForCurrentOperatingSystem())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CodexCliIntegration" /> class with the specified comparer of
    /// directory sources.
    /// </summary>
    /// <param name="codexPluginCli">The plugin commands of <c>codex</c>.</param>
    /// <param name="codexExecutableLocator">The locator that detects <c>codex</c>.</param>
    /// <param name="directoryPathComparer">
    /// The comparer that decides whether a directory marketplace has the directory of the source.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    internal CodexCliIntegration(ICodexPluginCli codexPluginCli, ICodexExecutableLocator codexExecutableLocator,
        DirectoryPathComparer directoryPathComparer)
    {
        _codexPluginCli = Ensure.NotNull(codexPluginCli);
        _codexExecutableLocator = Ensure.NotNull(codexExecutableLocator);
        _marketplaceChangeResolver = new MarketplaceChangeResolver(CodexAgentId, [RemoveMarketplaceCommand],
            Ensure.NotNull(directoryPathComparer));
    }

    /// <inheritdoc />
    /// <value>The id <see cref="CodexAgentId" />.</value>
    public override string AgentId => CodexAgentId;

    /// <inheritdoc />
    /// <remarks>
    /// The ref and the kit version are always <see langword="null" />, because <c>codex</c> does not report the ref of
    /// a marketplace; so the marketplaces are not listed.
    /// </remarks>
    public override async Task<InstalledIntegration?> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var plugin = await FindPluginAsync(cancellationToken).ConfigureAwait(false);

        return plugin is null
            ? null
            : new InstalledIntegration(AgentId, ShadeKitPlugin.Id, null, null, plugin.Enabled);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The Codex CLI is detected by <c>codex</c> (on Windows <c>codex.exe</c> or <c>codex.cmd</c>) in an absolute
    /// directory of the <c>PATH</c>.
    /// </remarks>
    private protected override string? FindExecutable()
    {
        return _codexExecutableLocator.FindCodexExecutable();
    }

    /// <inheritdoc />
    /// <returns>
    /// A <see cref="CodexNotFoundException" /> that names <c>codex plugin list --json</c>, the first command of an
    /// install or update.
    /// </returns>
    private protected override ExternalToolException CreateNotFoundException()
    {
        return new CodexNotFoundException(ListPluginsArguments, CodexCommandRunner.Tool.NotFoundReason);
    }

    /// <inheritdoc />
    private protected override async Task<IntegrationResult> ApplyAsync(MarketplaceSource source,
        CodexInstalledPlugin? pluginBefore, CancellationToken cancellationToken)
    {
        var marketplace = FindMarketplace(
            await _codexPluginCli.ListMarketplacesAsync(cancellationToken).ConfigureAwait(false));

        var change = _marketplaceChangeResolver.Resolve(marketplace, source);

        return await RunStepsAsync(
            steps => ApplyChangeAsync(source, change, pluginBefore, steps, cancellationToken),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IntegrationResult> ApplyChangeAsync(MarketplaceSource source, MarketplaceChange change,
        CodexInstalledPlugin? pluginBefore, IntegrationStepState steps, CancellationToken cancellationToken)
    {
        string? previousKitVersion = null;

        switch (change)
        {
            case MarketplaceChange.Add:
                await _codexPluginCli.AddMarketplaceAsync(source, cancellationToken).ConfigureAwait(false);
                break;

            case MarketplaceChange.Verify:
                // codex does not report the ref, so an add with the source tells whether the marketplace has it.
                if (await TryKeepGitMarketplaceAsync(source, cancellationToken).ConfigureAwait(false) is
                    { } isKept)
                {
                    previousKitVersion = isKept && pluginBefore is not null ? source.KitVersion : null;
                    break;
                }

                steps.StartSwitch();

                if (pluginBefore is not null)
                {
                    await _codexPluginCli.RemovePluginAsync(ShadeKitPlugin.Id, cancellationToken)
                        .ConfigureAwait(false);

                    steps.MarkRemoved();
                }

                await _codexPluginCli.RemoveMarketplaceAsync(ShadeKitPlugin.MarketplaceName, cancellationToken)
                    .ConfigureAwait(false);

                steps.MarkRemoved();

                await _codexPluginCli.AddMarketplaceAsync(source, cancellationToken).ConfigureAwait(false);
                break;

            case MarketplaceChange.Keep:
                // Only a directory marketplace is kept, because codex reports no ref for a GitHub one. It needs
                // no step: codex refuses to upgrade it, and plugin add copies it again.
                break;

            default:
                throw new UnreachableException(
                    $"The marketplace change '{change}' is not expected for {AgentId}, which reports no ref.");
        }

        return await AddPluginAsync(source, pluginBefore, previousKitVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    // Returns true if the marketplace has the source and was fetched again, false if codex added it just now, and
    // null if codex refused the add, which means that the marketplace has another source or ref.
    private async Task<bool?> TryKeepGitMarketplaceAsync(MarketplaceSource source,
        CancellationToken cancellationToken)
    {
        CodexMarketplaceAddResult addResult;

        try
        {
            addResult = await _codexPluginCli.AddMarketplaceAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (CodexCommandFailedException exception) when (exception.ExitCode is not (null or 0))
        {
            return null;
        }

        if (!addResult.AlreadyAdded)
        {
            return false;
        }

        // A kept marketplace that follows a branch would otherwise stay at the commit of its last fetch.
        await _codexPluginCli.UpgradeMarketplaceAsync(ShadeKitPlugin.MarketplaceName, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }

    private async Task<IntegrationResult> AddPluginAsync(MarketplaceSource source, CodexInstalledPlugin? pluginBefore,
        string? previousKitVersion, CancellationToken cancellationToken)
    {
        await _codexPluginCli.AddPluginAsync(ShadeKitPlugin.Id, cancellationToken).ConfigureAwait(false);

        // The add output does not say whether the plugin is enabled, so the state is read again.
        var plugin = await FindPluginAsync(cancellationToken).ConfigureAwait(false)
                     ?? throw new CodexCommandFailedException(ListPluginsArguments, 0,
                         $"codex does not list the plugin '{ShadeKitPlugin.Name}' of the marketplace " +
                         $"'{ShadeKitPlugin.MarketplaceName}' as installed after it was added.");

        var outcome = pluginBefore is null ? IntegrationOutcome.Installed : IntegrationOutcome.Updated;

        return IntegrationResult.ForShadeKitPlugin(AgentId, source, outcome, previousKitVersion, plugin.Enabled);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The plugin counts only with the name <see cref="ShadeKitPlugin.Name" />, the marketplace
    /// <see cref="ShadeKitPlugin.MarketplaceName" />, and <c>installed</c> <see langword="true" />.
    /// </remarks>
    private protected override async Task<CodexInstalledPlugin?> FindPluginAsync(
        CancellationToken cancellationToken)
    {
        return FindPlugin(await _codexPluginCli.ListPluginsAsync(cancellationToken).ConfigureAwait(false));
    }

    private static CodexInstalledPlugin? FindPlugin(IEnumerable<CodexInstalledPlugin> plugins)
    {
        return plugins.FirstOrDefault(x =>
            x.Installed
            && string.Equals(x.Name, ShadeKitPlugin.Name, StringComparison.Ordinal)
            && string.Equals(x.MarketplaceName, ShadeKitPlugin.MarketplaceName, StringComparison.Ordinal));
    }

    private static AgentMarketplace? FindMarketplace(IEnumerable<CodexMarketplace> marketplaces)
    {
        return marketplaces
            .FirstOrDefault(x => string.Equals(x.Name, ShadeKitPlugin.MarketplaceName, StringComparison.Ordinal))
            ?.ToAgentMarketplace();
    }
}
