using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Locates the <c>git</c> executable in the absolute directories of the <c>PATH</c> environment variable.
/// </summary>
/// <remarks>
/// The executable is <c>git.exe</c> with the Windows rules and <c>git</c> with the Unix rules; <c>git.cmd</c> is not
/// found. The search follows the rules of <see cref="PathExecutableLocator" />.
/// </remarks>
public sealed class GitExecutableLocator : PathExecutableLocator, IGitExecutableLocator
{
    private const string WindowsExecutableName = "git.exe";

    private const string UnixExecutableName = "git";

    /// <summary>
    /// Initializes a new instance of the <see cref="GitExecutableLocator" /> class that reads the <c>PATH</c>
    /// environment variable of the current process and uses the rules of the current operating system.
    /// </summary>
    public GitExecutableLocator()
        : base(new PathExecutableSearch(WindowsExecutableName, UnixExecutableName))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GitExecutableLocator" /> class with the specified sources for the
    /// <c>PATH</c> value, the operating system rules, the file check, and the execute check, as described for
    /// <see cref="PathExecutableSearch" />.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    internal GitExecutableLocator(Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists,
        Func<string, bool> isExecutable)
        : base(new PathExecutableSearch(WindowsExecutableName, UnixExecutableName, getPathVariable, isWindows,
            fileExists, isExecutable))
    {
    }

    /// <inheritdoc />
    public string? FindGitExecutable()
    {
        return FindExecutable();
    }
}
