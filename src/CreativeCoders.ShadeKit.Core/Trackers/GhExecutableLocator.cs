using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Locates the GitHub CLI executable <c>gh</c> in the absolute directories of the <c>PATH</c> environment variable.
/// </summary>
/// <remarks>
/// <para>
/// Empty and relative <c>PATH</c> entries are skipped, so a <c>gh</c> committed to a repository is never started.
/// The first existing file wins; with the Unix rules it also needs an execute bit.
/// </para>
/// <para>
/// On Windows the executable is <c>gh.exe</c>, double quotes are removed, and a quoted <c>;</c> does not separate
/// entries; <c>PATHEXT</c> is not evaluated. The first path found is cached.
/// </para>
/// </remarks>
public sealed class GhExecutableLocator : IGhExecutableLocator
{
    private const string WindowsExecutableName = "gh.exe";

    private const string UnixExecutableName = "gh";

    private readonly PathExecutableSearch _pathExecutableSearch;

    private string? _ghExecutablePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GhExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public GhExecutableLocator()
    {
        _pathExecutableSearch = new PathExecutableSearch(WindowsExecutableName, UnixExecutableName);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GhExecutableLocator" /> class with the specified sources for the
    /// <c>PATH</c> value, the operating system rules, the file check, and the execute check.
    /// </summary>
    /// <param name="getPathVariable">
    /// The function that returns the value of the <c>PATH</c> environment variable, or <see langword="null" /> if it
    /// is not set.
    /// </param>
    /// <param name="isWindows">
    /// <see langword="true" /> to use the Windows rules (separator <c>;</c>, executable <c>gh.exe</c>, drive and UNC
    /// paths); <see langword="false" /> to use the Unix rules (separator <c>:</c>, executable <c>gh</c>, paths that
    /// start with <c>/</c>).
    /// </param>
    /// <param name="fileExists">
    /// The function that returns <see langword="true" /> if the specified path is an existing file and not a
    /// directory.
    /// </param>
    /// <param name="isExecutable">
    /// The function that returns <see langword="true" /> if the existing file at the specified path has at least one
    /// execute bit. It is called only with the Unix rules and only for a path for which
    /// <paramref name="fileExists" /> returned <see langword="true" />.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    internal GhExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
    {
        _pathExecutableSearch = new PathExecutableSearch(WindowsExecutableName, UnixExecutableName, getPathVariable,
            isWindows, fileExists, isExecutable);
    }

    /// <inheritdoc />
    public string? FindGhExecutable()
    {
        return _ghExecutablePath ??= _pathExecutableSearch.Find();
    }
}
