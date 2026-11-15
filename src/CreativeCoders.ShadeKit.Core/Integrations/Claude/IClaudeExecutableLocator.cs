namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Locates the Claude Code CLI executable <c>claude</c> that SHADE kit starts.
/// </summary>
public interface IClaudeExecutableLocator
{
    /// <summary>
    /// Returns the absolute path of the <c>claude</c> executable.
    /// </summary>
    /// <remarks>
    /// The path is resolved from the absolute directories of the <c>PATH</c> environment variable only, so a
    /// <c>claude</c> executable in the current directory is never used. With the Windows rules only <c>claude.exe</c>
    /// is found, which the native installer of Claude Code provides.
    /// </remarks>
    /// <returns>
    /// The absolute path of the <c>claude</c> executable, or <see langword="null" /> if no absolute directory of the
    /// <c>PATH</c> environment variable contains it.
    /// </returns>
    string? FindClaudeExecutable();
}
