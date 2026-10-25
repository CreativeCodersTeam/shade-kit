using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Locates the GitHub CLI executable <c>gh</c> in the absolute directories of the <c>PATH</c> environment variable.
/// </summary>
/// <remarks>
/// The executable is <c>gh.exe</c> with the Windows rules and <c>gh</c> with the Unix rules. The search follows the
/// rules of <see cref="PathExecutableLocator" />.
/// </remarks>
public sealed class GhExecutableLocator : PathExecutableLocator, IGhExecutableLocator
{
    private const string WindowsExecutableName = "gh.exe";

    private const string UnixExecutableName = "gh";

    /// <summary>
    /// Initializes a new instance of the <see cref="GhExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public GhExecutableLocator()
        : base(new PathExecutableSearch(WindowsExecutableName, UnixExecutableName))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GhExecutableLocator" /> class with the specified sources for the
    /// <c>PATH</c> value, the operating system rules, the file check, and the execute check, as described for
    /// <see cref="PathExecutableSearch" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    internal GhExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
        : base(new PathExecutableSearch(WindowsExecutableName, UnixExecutableName, getPathVariable, isWindows,
            fileExists, isExecutable))
    {
    }

    /// <inheritdoc />
    public string? FindGhExecutable()
    {
        return FindExecutable();
    }
}
