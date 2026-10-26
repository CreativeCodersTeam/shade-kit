namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Locates the GitHub CLI executable <c>gh</c> that SHADE kit starts.
/// </summary>
public interface IGhExecutableLocator
{
    /// <summary>
    /// Returns the absolute path of the <c>gh</c> executable.
    /// </summary>
    /// <remarks>
    /// The path is resolved from the absolute directories of the <c>PATH</c> environment variable only, so a
    /// <c>gh</c> executable in the current directory (usually the repository root) is never used.
    /// </remarks>
    /// <returns>
    /// The absolute path of the <c>gh</c> executable, or <see langword="null" /> if no absolute directory of the
    /// <c>PATH</c> environment variable contains it.
    /// </returns>
    string? FindGhExecutable();
}
