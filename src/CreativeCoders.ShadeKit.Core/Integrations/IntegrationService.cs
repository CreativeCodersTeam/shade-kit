using System.Diagnostics.CodeAnalysis;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Git;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Integrations.Warnings;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Installs, updates, and reads the SHADE kit integrations of the registered <see cref="IAgentIntegration" />s from
/// the marketplace source that fits the engine version of an <see cref="IEngineVersionProvider" />.
/// </summary>
/// <remarks>
/// <para>
/// For a release engine without the environment variable <see cref="MarketplaceSourceVariableName" />, the
/// <see cref="IGitRemoteTagChecker" /> checks that the release tag exists in <see cref="MarketplaceRepositoryUrl" />.
/// A missing tag gives <see cref="FallbackRef" /> with an <see cref="IntegrationReleaseTagMissingWarning" />; if the
/// check cannot be made (no <c>git</c>, no network), the tag is used unchecked without a warning, so an agent CLI that
/// cannot find it reports the failure.
/// </para>
/// <para>
/// The agents run one after the other. If an agent fails after other agents were already installed or updated, the
/// failure is reported as an <see cref="IntegrationRunFailedException" /> that keeps their results; a cancellation
/// and a failure of the first agent escape unchanged.
/// </para>
/// </remarks>
[SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded",
    Justification = "The marketplace repository of SHADE kit is fixed by design; the environment variable " +
                    "SHADE_KIT_MARKETPLACE_SOURCE replaces the source for developers.")]
public sealed class IntegrationService : IIntegrationService
{
    /// <summary>
    /// Represents the name of the environment variable that replaces the marketplace source, a developer option for
    /// example to install from the working tree of the SHADE kit repository.
    /// </summary>
    public const string MarketplaceSourceVariableName = "SHADE_KIT_MARKETPLACE_SOURCE";

    /// <summary>
    /// Represents the GitHub repository that is the plugin marketplace of SHADE kit.
    /// </summary>
    public const string MarketplaceRepo = "CreativeCodersTeam/shade-kit";

    /// <summary>
    /// Represents the <c>https://</c> URL of the git repository of <see cref="MarketplaceRepo" />, in which the release
    /// tags are checked.
    /// </summary>
    public const string MarketplaceRepositoryUrl = "https://github.com/" + MarketplaceRepo + ".git";

    /// <summary>
    /// Represents the branch that a prerelease engine, and a release engine whose release tag does not exist, installs
    /// from.
    /// </summary>
    public const string FallbackRef = "main";

    private readonly IReadOnlyList<IAgentIntegration> _agentIntegrations;

    private readonly IEngineVersionProvider _engineVersionProvider;

    private readonly IGitRemoteTagChecker _gitRemoteTagChecker;

    private readonly Func<string, string?> _getEnvironmentVariable;

    private readonly Func<string> _getCurrentDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationService" /> class that reads the environment variables
    /// and the current directory of the current process.
    /// </summary>
    /// <param name="agentIntegrations">The integrations of the supported agents.</param>
    /// <param name="engineVersionProvider">The provider of the running engine version.</param>
    /// <param name="gitRemoteTagChecker">The checker that finds out whether the release tag exists.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="agentIntegrations" />, <paramref name="engineVersionProvider" />, or
    /// <paramref name="gitRemoteTagChecker" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Two of <paramref name="agentIntegrations" /> have the same agent id, compared case-insensitively.
    /// </exception>
    public IntegrationService(IEnumerable<IAgentIntegration> agentIntegrations,
        IEngineVersionProvider engineVersionProvider, IGitRemoteTagChecker gitRemoteTagChecker)
        : this(agentIntegrations, engineVersionProvider, gitRemoteTagChecker, Environment.GetEnvironmentVariable,
            Directory.GetCurrentDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationService" /> class with the specified sources for the
    /// environment variables and the current directory.
    /// </summary>
    /// <param name="agentIntegrations">The integrations of the supported agents.</param>
    /// <param name="engineVersionProvider">The provider of the running engine version.</param>
    /// <param name="gitRemoteTagChecker">The checker that finds out whether the release tag exists.</param>
    /// <param name="getEnvironmentVariable">
    /// The function that returns the value of an environment variable, or <see langword="null" /> if it is not set.
    /// </param>
    /// <param name="getCurrentDirectory">The function that returns the absolute current directory.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    /// Two of <paramref name="agentIntegrations" /> have the same agent id, compared case-insensitively.
    /// </exception>
    internal IntegrationService(IEnumerable<IAgentIntegration> agentIntegrations,
        IEngineVersionProvider engineVersionProvider, IGitRemoteTagChecker gitRemoteTagChecker,
        Func<string, string?> getEnvironmentVariable, Func<string> getCurrentDirectory)
    {
        _agentIntegrations = [.. Ensure.NotNull(agentIntegrations)];
        _engineVersionProvider = Ensure.NotNull(engineVersionProvider);
        _gitRemoteTagChecker = Ensure.NotNull(gitRemoteTagChecker);
        _getEnvironmentVariable = Ensure.NotNull(getEnvironmentVariable);
        _getCurrentDirectory = Ensure.NotNull(getCurrentDirectory);

        AgentIds = [.. _agentIntegrations.Select(x => x.AgentId)];

        if (AgentIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != AgentIds.Count)
        {
            throw new ArgumentException("Two agent integrations have the same agent id.", nameof(agentIntegrations));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> AgentIds { get; }

    /// <inheritdoc />
    public async Task<IntegrationRunResult> InstallAsync(IReadOnlyCollection<string>? agentIds,
        CancellationToken cancellationToken = default)
    {
        var selectedAgents = agentIds is null or { Count: 0 } ? null : SelectAgents(agentIds);

        // The local part of the source is resolved before the detection, so an invalid override is reported first;
        // the release tag is checked only when there is an agent to install for.
        var uncheckedSource = ResolveUncheckedSource();

        var agents = selectedAgents ?? DetectAgents();

        // A named agent that is missing fails before the network call of the tag check.
        foreach (var agent in agents)
        {
            agent.EnsureAvailable();
        }

        var (source, warnings) = await CheckReleaseTagAsync(uncheckedSource, cancellationToken).ConfigureAwait(false);

        var results = new List<IntegrationResult>(agents.Count);

        foreach (var agent in agents)
        {
            try
            {
                results.Add(await agent.InstallAsync(source, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (results.Count > 0 && IsAgentFailure(exception))
            {
                throw new IntegrationRunFailedException(agent.AgentId, source, results, exception);
            }
        }

        return new IntegrationRunResult(source, results, warnings);
    }

    /// <inheritdoc />
    public async Task<IntegrationRunResult> UpdateAsync(CancellationToken cancellationToken = default)
    {
        var agents = DetectAgents();

        // The source is resolved once, by the first agent that has the integration installed, before that agent
        // changes anything; without an installed integration neither the tag check nor its warnings happen.
        (MarketplaceSource Source, IReadOnlyList<KitWarning> Warnings)? resolved = null;

        async Task<MarketplaceSource> GetSourceAsync(CancellationToken token)
        {
            resolved ??= await ResolveSourceAsync(token).ConfigureAwait(false);

            return resolved.Value.Source;
        }

        var results = new List<IntegrationResult>(agents.Count);

        foreach (var agent in agents)
        {
            try
            {
                if (await agent.UpdateAsync(GetSourceAsync, cancellationToken).ConfigureAwait(false) is { } result)
                {
                    results.Add(result);
                }
            }
            catch (Exception exception) when (results.Count > 0 && resolved is not null && IsAgentFailure(exception))
            {
                throw new IntegrationRunFailedException(agent.AgentId, resolved.Value.Source, results, exception);
            }
        }

        return resolved is { } run
            ? new IntegrationRunResult(run.Source, results, run.Warnings)
            : new IntegrationRunResult(null, [], []);
    }

    /// <inheritdoc />
    public async Task<InstalledIntegrationsResult> GetInstalledAsync(CancellationToken cancellationToken = default)
    {
        var integrations = new List<InstalledIntegration>();
        var warnings = new List<KitWarning>();

        foreach (var agent in _agentIntegrations.Where(x => x.IsAvailable()))
        {
            try
            {
                if (await agent.GetInstalledAsync(cancellationToken).ConfigureAwait(false) is { } integration)
                {
                    integrations.Add(integration);
                }
            }
            catch (ExternalToolUnavailableException exception)
            {
                warnings.Add(new AgentUnavailableWarning(agent.AgentId, exception.Message));
            }
        }

        return new InstalledIntegrationsResult(integrations, warnings);
    }

    /// <summary>
    /// Returns the marketplace source that the running engine targets.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the source and its warnings: the
    /// value of <see cref="MarketplaceSourceVariableName" /> if it is set; otherwise <see cref="MarketplaceRepo" />
    /// with the tag <c>v&lt;engine version&gt;</c> for a release engine whose tag exists or cannot be checked, with
    /// <see cref="FallbackRef" /> and an <see cref="IntegrationReleaseTagMissingWarning" /> for a release engine whose
    /// tag does not exist, or with <see cref="FallbackRef" /> and an <see cref="IntegrationPrereleaseRefWarning" /> for
    /// a prerelease engine. Only a release engine without the environment variable checks the tag.
    /// </returns>
    /// <exception cref="InvalidMarketplaceSourceException">The environment variable cannot be used.</exception>
    /// <exception cref="InvalidOperationException">The engine version is not a valid SemVer 2.0.0 version.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    internal async Task<(MarketplaceSource Source, IReadOnlyList<KitWarning> Warnings)> ResolveSourceAsync(
        CancellationToken cancellationToken)
    {
        return await CheckReleaseTagAsync(ResolveUncheckedSource(), cancellationToken).ConfigureAwait(false);
    }

    // Everything that needs neither git nor the network: the override, or the source that the engine version names.
    private UncheckedSource ResolveUncheckedSource()
    {
        if (_getEnvironmentVariable(MarketplaceSourceVariableName) is { } overrideValue)
        {
            return new UncheckedSource(MarketplaceSource.Parse(overrideValue, _getCurrentDirectory()), [], null);
        }

        var engineVersionText = _engineVersionProvider.Version;

        if (!SemanticVersion.TryParse(engineVersionText, out var engineVersion))
        {
            throw new InvalidOperationException(
                $"The engine version '{engineVersionText}' is not a valid SemVer 2.0.0 version, so the marketplace " +
                "source cannot be chosen. The engine build is defective.");
        }

        if (engineVersion.IsPrerelease)
        {
            return new UncheckedSource(MarketplaceSource.ForGitHub(MarketplaceRepo, FallbackRef),
                [new IntegrationPrereleaseRefWarning(engineVersionText, FallbackRef)], null);
        }

        return new UncheckedSource(MarketplaceSource.ForGitHub(MarketplaceRepo, $"v{engineVersion}"), [],
            engineVersionText);
    }

    private async Task<(MarketplaceSource Source, IReadOnlyList<KitWarning> Warnings)> CheckReleaseTagAsync(
        UncheckedSource uncheckedSource, CancellationToken cancellationToken)
    {
        if (uncheckedSource is not { ReleaseEngineVersion: { } engineVersion, Source.Ref: { } releaseTag })
        {
            return (uncheckedSource.Source, uncheckedSource.Warnings);
        }

        var tagState = await _gitRemoteTagChecker
            .CheckTagAsync(MarketplaceRepositoryUrl, releaseTag, cancellationToken)
            .ConfigureAwait(false);

        // A tag that cannot be checked (no git, no network) is used unchecked; the agent CLI reports a missing one.
        return tagState == GitRemoteTagState.Missing
            ? (MarketplaceSource.ForGitHub(MarketplaceRepo, FallbackRef),
                [new IntegrationReleaseTagMissingWarning(engineVersion, releaseTag, FallbackRef)])
            : (uncheckedSource.Source, uncheckedSource.Warnings);
    }

    // The documented failures of an agent; anything else is a defect, and a cancellation stays a cancellation.
    private static bool IsAgentFailure(Exception exception)
    {
        return exception is ExternalToolUnavailableException or IntegrationConflictException
            or IntegrationIncompleteException;
    }

    private List<IAgentIntegration> SelectAgents(IReadOnlyCollection<string> agentIds)
    {
        var agents = new List<IAgentIntegration>();

        foreach (var agentId in agentIds)
        {
            var agent = _agentIntegrations.FirstOrDefault(x =>
                            string.Equals(x.AgentId, agentId, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ArgumentException(
                            $"The agent '{agentId}' is not supported. Supported agents: {string.Join(", ", AgentIds)}.",
                            nameof(agentIds));

            if (!agents.Contains(agent))
            {
                agents.Add(agent);
            }
        }

        return agents;
    }

    private List<IAgentIntegration> DetectAgents()
    {
        var agents = _agentIntegrations.Where(x => x.IsAvailable()).ToList();

        return agents.Count == 0
            ? throw new NoAgentFoundException(AgentIds)
            : agents;
    }

    // The source before the release tag check; ReleaseEngineVersion is set only for the release tag of a release
    // engine, the one source that is checked.
    private sealed record UncheckedSource(
        MarketplaceSource Source,
        IReadOnlyList<KitWarning> Warnings,
        string? ReleaseEngineVersion);
}
