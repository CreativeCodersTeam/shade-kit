namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Locates the GitHub Copilot CLI executable <c>copilot</c> that SHADE kit starts.
/// </summary>
public interface ICopilotExecutableLocator
{
    /// <summary>
    /// Returns the absolute path of the <c>copilot</c> executable.
    /// </summary>
    /// <remarks>
    /// The path is resolved from the absolute directories of the <c>PATH</c> environment variable only, so a
    /// <c>copilot</c> executable in the current directory is never used. With the Windows rules <c>copilot.exe</c> is
    /// preferred and <c>copilot.cmd</c>, the shim of an npm installation, is found as well; <c>copilot.ps1</c> is not.
    /// </remarks>
    /// <returns>
    /// The absolute path of the <c>copilot</c> executable, or <see langword="null" /> if no absolute directory of the
    /// <c>PATH</c> environment variable contains it.
    /// </returns>
    string? FindCopilotExecutable();
}
