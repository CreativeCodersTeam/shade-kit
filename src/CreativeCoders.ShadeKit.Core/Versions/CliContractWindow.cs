namespace CreativeCoders.ShadeKit.Core.Versions;

/// <summary>
/// Represents the range of CLI contracts that an engine supports, from the oldest supported contract to the current
/// one.
/// </summary>
/// <remarks>
/// CLI contracts are numbered from 1, so a window always starts at 1 or higher, and a requested contract 0 lies below
/// every window. The window of the running engine is <see cref="CliContract.Window" />.
/// </remarks>
public sealed record CliContractWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CliContractWindow" /> class with the oldest supported and the
    /// current CLI contract.
    /// </summary>
    /// <param name="minSupported">The oldest CLI contract that the engine still supports.</param>
    /// <param name="current">The newest CLI contract that the engine supports.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minSupported" /> is less than 1, or <paramref name="current" /> is less than
    /// <paramref name="minSupported" />.
    /// </exception>
    public CliContractWindow(int minSupported, int current)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minSupported, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(current, minSupported);

        MinSupported = minSupported;
        Current = current;
    }

    /// <summary>
    /// Gets the oldest CLI contract that the engine still supports.
    /// </summary>
    /// <value>A contract number of 1 or higher.</value>
    public int MinSupported { get; }

    /// <summary>
    /// Gets the newest CLI contract that the engine supports.
    /// </summary>
    /// <value>A contract number equal to or greater than <see cref="MinSupported" />.</value>
    public int Current { get; }
}
