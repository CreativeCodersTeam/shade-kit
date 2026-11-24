using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Versions;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the CLI contract window of the engine in the <c>data</c> object that <c>shade-kit version --json</c>
/// returns, for example <c>{"current":1,"minSupported":1}</c>.
/// </summary>
/// <param name="Current">The newest CLI contract that the engine supports.</param>
/// <param name="MinSupported">The oldest CLI contract that the engine still supports.</param>
public sealed record VersionCommandContractData(int Current, int MinSupported)
{
    /// <summary>
    /// Creates the data of the specified CLI contract window.
    /// </summary>
    /// <param name="window">The CLI contract window of the engine.</param>
    /// <returns>The data with the bounds of <paramref name="window" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="window" /> is <see langword="null" />.</exception>
    public static VersionCommandContractData From(CliContractWindow window)
    {
        Ensure.NotNull(window);

        return new VersionCommandContractData(window.Current, window.MinSupported);
    }
}
