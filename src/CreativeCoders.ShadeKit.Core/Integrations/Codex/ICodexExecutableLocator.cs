namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Locates the OpenAI Codex CLI executable <c>codex</c> that SHADE kit starts.
/// </summary>
public interface ICodexExecutableLocator
{
    /// <summary>
    /// Returns the absolute path of the <c>codex</c> executable.
    /// </summary>
    /// <remarks>
    /// The path is resolved from the absolute directories of the <c>PATH</c> environment variable only, so a
    /// <c>codex</c> executable in the current directory is never used. With the Windows rules <c>codex.exe</c> is
    /// preferred and <c>codex.cmd</c>, the shim of an npm installation, is found as well; <c>codex.ps1</c> is not.
    /// </remarks>
    /// <returns>
    /// The absolute path of the <c>codex</c> executable, or <see langword="null" /> if no absolute directory of the
    /// <c>PATH</c> environment variable contains it.
    /// </returns>
    string? FindCodexExecutable();
}
