namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Provides the token that signals that the user canceled the running CLI command.
/// </summary>
/// <remarks>
/// Commands pass <see cref="Token" /> to every core call that accepts a <see cref="CancellationToken" />, so that a
/// canceled run stops at the next check and the core can roll back what it has started, as
/// <see cref="CreativeCoders.ShadeKit.Core.Features.IFeatureStore.CreateAsync" /> does. A core call that stops
/// because of the token throws an <see cref="OperationCanceledException" />, which the run reports with
/// <see cref="ShadeKitCliExitCodes.Canceled" />.
/// </remarks>
public interface ICliCancellation
{
    /// <summary>
    /// Gets the token that is canceled when the user cancels the run.
    /// </summary>
    /// <value>The cancellation token of the run.</value>
    CancellationToken Token { get; }
}
