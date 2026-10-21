namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Locates the <c>git</c> executable that SHADE kit starts.
/// </summary>
public interface IGitExecutableLocator
{
    /// <summary>
    /// Returns the absolute path of the <c>git</c> executable.
    /// </summary>
    /// <remarks>
    /// The path is resolved from the absolute directories of the <c>PATH</c> environment variable only, so a
    /// <c>git</c> executable in the current directory (usually the repository root) is never used.
    /// </remarks>
    /// <returns>
    /// The absolute path of the <c>git</c> executable, or <see langword="null" /> if no absolute directory of the
    /// <c>PATH</c> environment variable contains it.
    /// </returns>
    string? FindGitExecutable();
}
