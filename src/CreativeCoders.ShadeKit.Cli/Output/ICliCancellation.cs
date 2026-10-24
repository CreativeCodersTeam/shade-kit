namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Provides the token that signals that the user canceled the running CLI command.
/// </summary>
/// <remarks>
/// Commands pass <see cref="Token" /> to every core call; a run stopped by it ends with
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
