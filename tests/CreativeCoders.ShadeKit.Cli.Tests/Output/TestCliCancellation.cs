using CreativeCoders.ShadeKit.Cli.Output;

namespace CreativeCoders.ShadeKit.Cli.Tests.Output;

/// <summary>
/// Provides a fixed cancellation token to the commands under test.
/// </summary>
/// <param name="token">The token that <see cref="Token" /> returns.</param>
internal sealed class TestCliCancellation(CancellationToken token) : ICliCancellation
{
    /// <summary>
    /// Gets an instance whose token can never be canceled.
    /// </summary>
    public static TestCliCancellation None { get; } = new(CancellationToken.None);

    /// <summary>
    /// Gets an instance whose token is canceled already.
    /// </summary>
    public static TestCliCancellation Canceled { get; } = new(new CancellationToken(true));

    /// <inheritdoc />
    public CancellationToken Token => token;
}
