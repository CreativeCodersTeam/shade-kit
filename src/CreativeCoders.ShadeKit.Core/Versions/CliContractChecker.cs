using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Versions.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Checks requested CLI contracts against a fixed <see cref="CliContractWindow" />.
/// </summary>
/// <remarks>
/// The engine registers this checker with <see cref="CliContract.Window" />; any other window serves tests.
/// </remarks>
/// <param name="window">The window of CLI contracts that the engine supports.</param>
/// <exception cref="ArgumentNullException"><paramref name="window" /> is <see langword="null" />.</exception>
public sealed class CliContractChecker(CliContractWindow window) : ICliContractChecker
{
    /// <inheritdoc />
    public CliContractWindow Window { get; } = Ensure.NotNull(window);

    /// <inheritdoc />
    public void Check(int requestedContract)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestedContract);

        if (requestedContract > Window.Current)
        {
            throw new EngineContractOutdatedException(requestedContract, Window.MinSupported, Window.Current);
        }

        if (requestedContract < Window.MinSupported)
        {
            throw new SkillsContractOutdatedException(requestedContract, Window.MinSupported, Window.Current);
        }
    }
}
