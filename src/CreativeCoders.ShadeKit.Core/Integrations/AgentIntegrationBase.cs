using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Provides the parts of an <see cref="IAgentIntegration" /> that are the same for every agent: the detection, the
/// contract of <see cref="InstallAsync" /> and <see cref="UpdateAsync" />, and the translation of a failure while the
/// plugin marketplace is switched.
/// </summary>
/// <remarks>
/// A derived integration supplies the agent-specific parts: how the CLI of the agent is found, the exception for a
/// missing CLI, how the installed plugin is found, and the steps of an install or update
/// (<see cref="ApplyAsync" />), which run inside <see cref="RunStepsAsync" />. Only integrations of this assembly
/// derive from it.
/// </remarks>
/// <typeparam name="TPlugin">The type of the installed SHADE kit plugin, as the CLI of the agent lists it.</typeparam>
public abstract class AgentIntegrationBase<TPlugin> : IAgentIntegration
    where TPlugin : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgentIntegrationBase{TPlugin}" /> class.
    /// </summary>
    private protected AgentIntegrationBase()
    {
    }

    /// <inheritdoc />
    public abstract string AgentId { get; }

    /// <inheritdoc />
    /// <remarks>The agent is detected if <see cref="FindExecutable" /> returns a path.</remarks>
    public bool IsAvailable()
    {
        return FindExecutable() is not null;
    }

    /// <inheritdoc />
    /// <exception cref="ExternalToolException">
    /// The CLI of the agent is not found; the exception is the one that <see cref="CreateNotFoundException" />
    /// returns.
    /// </exception>
    public void EnsureAvailable()
    {
        if (!IsAvailable())
        {
            throw CreateNotFoundException();
        }
    }

    /// <inheritdoc />
    public abstract Task<InstalledIntegration?> GetInstalledAsync(CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Returns the path of the CLI of the agent.
    /// </summary>
    /// <returns>The absolute path of the CLI, or <see langword="null" /> if it is not found.</returns>
    private protected abstract string? FindExecutable();

    /// <summary>
    /// Creates the exception that <see cref="EnsureAvailable" /> throws for a missing CLI of the agent.
    /// </summary>
    /// <returns>The not-found exception of the agent.</returns>
    private protected abstract ExternalToolException CreateNotFoundException();

    /// <summary>
    /// Reads the installed SHADE kit plugin of the agent.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the installed plugin, or
    /// <see langword="null" /> if it is not installed for the user.
    /// </returns>
    private protected abstract Task<TPlugin?> FindPluginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Installs the SHADE kit plugin from the specified source, or updates it if it is installed.
    /// </summary>
    /// <remarks>
    /// An implementation runs the steps that change the agent inside <see cref="RunStepsAsync" />, so a failure is
    /// reported by what was already changed.
    /// </remarks>
    /// <param name="source">The marketplace source to install from.</param>
    /// <param name="pluginBefore">
    /// The plugin that was installed before the run, or <see langword="null" /> if none was installed.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains what was done.</returns>
    private protected abstract Task<IntegrationResult> ApplyAsync(MarketplaceSource source, TPlugin? pluginBefore,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs the specified steps of an install or update and translates a failure by what the steps had changed.
    /// </summary>
    /// <remarks>
    /// The steps record their progress in the <see cref="IntegrationStepState" /> they receive, and the translation
    /// reads it at the time of the failure. Nothing is rolled back, because running the install again repairs the
    /// integration. Every other exception escapes unchanged.
    /// </remarks>
    /// <param name="steps">The steps; they receive the state in which they record their progress.</param>
    /// <param name="cancellationToken">The token of the run, which the steps monitor.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the result of the steps.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="steps" /> is <see langword="null" />.</exception>
    /// <exception cref="IntegrationIncompleteException">
    /// An <see cref="ExternalToolException" /> was thrown after a step had removed the plugin or the marketplace
    /// (<see cref="IntegrationStepState.IsRemoved" />).
    /// </exception>
    /// <exception cref="IntegrationSwitchCanceledException">
    /// An <see cref="OperationCanceledException" /> was thrown while the marketplace was switched
    /// (<see cref="IntegrationStepState.IsSwitching" />) and <paramref name="cancellationToken" /> is canceled.
    /// </exception>
    private protected async Task<IntegrationResult> RunStepsAsync(
        Func<IntegrationStepState, Task<IntegrationResult>> steps, CancellationToken cancellationToken)
    {
        Ensure.NotNull(steps);

        var state = new IntegrationStepState();

        try
        {
            return await steps(state).ConfigureAwait(false);
        }
        // ExternalToolException also covers a command that ran and failed, not only a CLI that is missing.
        catch (ExternalToolException exception) when (state.IsRemoved)
        {
            throw new IntegrationIncompleteException(AgentId, exception);
        }
        catch (OperationCanceledException exception) when (state.IsSwitching &&
                                                           cancellationToken.IsCancellationRequested)
        {
            // A cancellation while the first removal runs counts too: the killed CLI may have removed it.
            throw new IntegrationSwitchCanceledException(AgentId, exception);
        }
    }
}
