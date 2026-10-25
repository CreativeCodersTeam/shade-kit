using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Locates the <c>git</c> executable in the absolute directories of the <c>PATH</c> environment variable.
/// </summary>
/// <remarks>
/// <para>
/// Empty and relative <c>PATH</c> entries are skipped, so a <c>git</c> committed to a repository is never started.
/// The first existing file wins; with the Unix rules it also needs an execute bit.
/// </para>
/// <para>
/// On Windows the executable is <c>git.exe</c>, double quotes are removed, and a quoted <c>;</c> does not separate
/// entries; <c>PATHEXT</c> is not evaluated, so <c>git.cmd</c> is not found. The first path found is cached.
/// </para>
/// </remarks>
public sealed class GitExecutableLocator : IGitExecutableLocator
{
    private const string WindowsExecutableName = "git.exe";

    private const string UnixExecutableName = "git";

    private readonly PathExecutableSearch _pathExecutableSearch;

    private string? _gitExecutablePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public GitExecutableLocator()
    {
        _pathExecutableSearch = new PathExecutableSearch(WindowsExecutableName, UnixExecutableName);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitExecutableLocator" /> class with the specified sources for the
    /// <c>PATH</c> value, the operating system rules, the file check, and the execute check.
    /// </summary>
    /// <param name="getPathVariable">
    /// The function that returns the value of the <c>PATH</c> environment variable, or <see langword="null" /> if it
    /// is not set.
    /// </param>
    /// <param name="isWindows">
    /// <see langword="true" /> to use the Windows rules (separator <c>;</c>, executable <c>git.exe</c>, drive and UNC
    /// paths); <see langword="false" /> to use the Unix rules (separator <c>:</c>, executable <c>git</c>, paths that
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
    internal GitExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
    {
        _pathExecutableSearch = new PathExecutableSearch(WindowsExecutableName, UnixExecutableName, getPathVariable,
            isWindows, fileExists, isExecutable);
    }

    /// <inheritdoc />
    public string? FindGitExecutable()
    {
        return _gitExecutablePath ??= _pathExecutableSearch.Find();
    }

    /// <summary>
    /// Determines whether the file at the specified path has at least one Unix execute bit.
    /// </summary>
    /// <remarks>
    /// A file whose mode cannot be read counts as not executable.
    /// </remarks>
    /// <param name="path">The absolute path of an existing file.</param>
    /// <returns>
    /// <see langword="true" /> if the owner, the group, or others may execute the file, or the current operating
    /// system is Windows; otherwise, <see langword="false" />.
    /// </returns>
    internal static bool HasUnixExecuteBit(string path)
    {
        return PathExecutableSearch.HasUnixExecuteBit(path);
    }
}
