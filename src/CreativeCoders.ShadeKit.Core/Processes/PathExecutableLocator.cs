namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Represents the base class of the locators that find the executable of an external tool in the absolute
/// directories of the <c>PATH</c> environment variable.
/// </summary>
/// <remarks>
/// <para>
/// Empty and relative <c>PATH</c> entries are skipped, so an executable committed to a repository is never started.
/// The first existing file wins; with the Unix rules it also needs an execute bit.
/// </para>
/// <para>
/// With the Windows rules double quotes are removed, and a quoted <c>;</c> does not separate entries; <c>PATHEXT</c>
/// is not evaluated, so only the Windows executable name, for example <c>git.exe</c>, is found. The first path found
/// is cached; a search that finds nothing is repeated by the next call.
/// </para>
/// </remarks>
public abstract class PathExecutableLocator
{
    private readonly PathExecutableSearch _pathExecutableSearch;

    private string? _executablePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathExecutableLocator" /> class that finds the executable with the
    /// specified search.
    /// </summary>
    /// <param name="pathExecutableSearch">The search that finds the executable.</param>
    private protected PathExecutableLocator(PathExecutableSearch pathExecutableSearch)
    {
        _pathExecutableSearch = pathExecutableSearch;
    }

    /// <summary>
    /// Returns the absolute path of the executable.
    /// </summary>
    /// <returns>
    /// The absolute path of the executable, or <see langword="null" /> if no absolute directory of the <c>PATH</c>
    /// environment variable contains it.
    /// </returns>
    protected string? FindExecutable()
    {
        return _executablePath ??= _pathExecutableSearch.Find();
    }
}
