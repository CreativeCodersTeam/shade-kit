using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Locates the GitHub Copilot CLI executable <c>copilot</c> in the absolute directories of the <c>PATH</c>
/// environment variable.
/// </summary>
/// <remarks>
/// <para>
/// The executable is <c>copilot</c> with the Unix rules. With the Windows rules it is <c>copilot.exe</c> or
/// <c>copilot.cmd</c>: each absolute <c>PATH</c> directory is checked for <c>copilot.exe</c>, then for
/// <c>copilot.cmd</c>, before the next directory, as described for <see cref="PathExecutableSearch" />. A
/// <c>copilot.cmd</c> is the shim of an npm installation; the <see cref="CopilotCommandRunner" /> runs it through
/// <c>cmd.exe</c>. <c>copilot.ps1</c> is not supported.
/// </para>
/// <para>
/// Unverified: that an npm installation on Windows puts <c>copilot.cmd</c> on the <c>PATH</c> (standard npm
/// behaviour) and that the Windows release archives contain <c>copilot.exe</c>.
/// </para>
/// </remarks>
public sealed class CopilotExecutableLocator : PathExecutableLocator, ICopilotExecutableLocator
{
    private const string UnixExecutableName = "copilot";

    private static readonly string[] WindowsExecutableNames = ["copilot.exe", "copilot.cmd"];

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public CopilotExecutableLocator()
        : base(new PathExecutableSearch(WindowsExecutableNames, UnixExecutableName))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotExecutableLocator" /> class with the specified sources for
    /// the <c>PATH</c> value, the operating system rules, the file check, and the execute check, as described for
    /// <see cref="PathExecutableSearch" />.
    /// </summary>
    /// <param name="getPathVariable">
    /// The function that returns the value of the <c>PATH</c> environment variable, or <see langword="null" /> if it
    /// is not set.
    /// </param>
    /// <param name="isWindows">
    /// <see langword="true" /> to use the Windows rules; <see langword="false" /> to use the Unix rules.
    /// </param>
    /// <param name="fileExists">
    /// The function that returns <see langword="true" /> if the specified path is an existing file.
    /// </param>
    /// <param name="isExecutable">
    /// The function that returns <see langword="true" /> if the existing file at the specified path has at least one
    /// execute bit; it is called only with the Unix rules.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    internal CopilotExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
        : base(new PathExecutableSearch(WindowsExecutableNames, UnixExecutableName, getPathVariable, isWindows,
            fileExists, isExecutable))
    {
    }

    /// <inheritdoc />
    public string? FindCopilotExecutable()
    {
        return FindExecutable();
    }
}
