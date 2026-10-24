namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Provides the line ending of the operating system on which SHADE kit runs.
/// </summary>
internal static class OperatingSystemLineEnding
{
    /// <summary>
    /// Gets the line ending of the current operating system.
    /// </summary>
    /// <value>
    /// <see cref="LineEnding.CrLf" /> on Windows; otherwise, <see cref="LineEnding.Lf" />.
    /// </value>
    public static LineEnding Current => OperatingSystem.IsWindows() ? LineEnding.CrLf : LineEnding.Lf;
}
