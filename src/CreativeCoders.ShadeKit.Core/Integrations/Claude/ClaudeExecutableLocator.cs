using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Locates the Claude Code CLI executable <c>claude</c> in the absolute directories of the <c>PATH</c> environment
/// variable.
/// </summary>
/// <remarks>
/// The executable is <c>claude.exe</c> with the Windows rules and <c>claude</c> with the Unix rules. The search
/// follows the rules of <see cref="PathExecutableLocator" />, so on Windows the <c>claude.cmd</c> or
/// <c>claude.ps1</c> shim of an npm installation is not found.
/// </remarks>
public sealed class ClaudeExecutableLocator : PathExecutableLocator, IClaudeExecutableLocator
{
    private const string WindowsExecutableName = "claude.exe";

    private const string UnixExecutableName = "claude";

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public ClaudeExecutableLocator()
        : base(new PathExecutableSearch(WindowsExecutableName, UnixExecutableName))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeExecutableLocator" /> class with the specified sources for
    /// the <c>PATH</c> value, the operating system rules, the file check, and the execute check, as described for
    /// <see cref="PathExecutableSearch" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    internal ClaudeExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
        : base(new PathExecutableSearch(WindowsExecutableName, UnixExecutableName, getPathVariable, isWindows,
            fileExists, isExecutable))
    {
    }

    /// <inheritdoc />
    public string? FindClaudeExecutable()
    {
        return FindExecutable();
    }
}
