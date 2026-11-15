using System.Text;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Searches an executable by name in the absolute directories of the <c>PATH</c> environment variable.
/// </summary>
/// <remarks>
/// <para>
/// Empty and relative <c>PATH</c> entries are skipped, so an executable committed to a repository is never found.
/// The first existing file wins; with the Unix rules it also needs an execute bit.
/// </para>
/// <para>
/// With the Windows rules double quotes are removed, and a quoted <c>;</c> does not separate entries; <c>PATHEXT</c>
/// is not evaluated, so only the Windows executable names are looked for. Every call of <see cref="Find" /> reads the
/// <c>PATH</c> value again; the result is not cached.
/// </para>
/// <para>
/// A tool can declare several Windows executable names in order of preference, for example <c>copilot.exe</c>, then
/// <c>copilot.cmd</c>. The search goes directory by directory: each absolute <c>PATH</c> directory is checked for
/// every name in order before the next directory, so the first directory that contains any of the names wins, and
/// within that directory the earlier name wins. This keeps the <c>PATH</c> order the deciding rule, as with a shell.
/// </para>
/// </remarks>
internal sealed class PathExecutableSearch
{
    /// <summary>
    /// Represents the name of the environment variable that lists the directories to search.
    /// </summary>
    public const string PathVariableName = "PATH";

    private const char WindowsPathListSeparator = ';';

    private const char UnixPathListSeparator = ':';

    private const char WindowsDirectorySeparator = '\\';

    private const char WindowsAltDirectorySeparator = '/';

    private const char UnixDirectorySeparator = '/';

    private const char Quote = '"';

    private const UnixFileMode AnyExecute =
        UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    private readonly IReadOnlyList<string> _executableNames;

    private readonly char _directorySeparator;

    private readonly Func<string?> _getPathVariable;

    private readonly bool _isWindows;

    private readonly Func<string, bool> _fileExists;

    private readonly Func<string, bool> _isExecutable;

    /// <summary>
    /// Initializes a new instance of the <see cref="PathExecutableSearch" /> class that searches the specified
    /// executable names, reads the <c>PATH</c> environment variable of the current process, and uses the rules of the
    /// current operating system.
    /// </summary>
    /// <param name="windowsExecutableName">
    /// The file name that is looked for with the Windows rules, for example <c>git.exe</c>.
    /// </param>
    /// <param name="unixExecutableName">
    /// The file name that is looked for with the Unix rules, for example <c>git</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="windowsExecutableName" /> or <paramref name="unixExecutableName" /> is <see langword="null" />,
    /// empty, or whitespace.
    /// </exception>
    public PathExecutableSearch(string windowsExecutableName, string unixExecutableName)
        : this(ToNameList(windowsExecutableName), unixExecutableName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathExecutableSearch" /> class that searches the specified
    /// Windows executable names in order of preference and the specified Unix executable name, reads the <c>PATH</c>
    /// environment variable of the current process, and uses the rules of the current operating system.
    /// </summary>
    /// <param name="windowsExecutableNames">
    /// The file names that are looked for with the Windows rules, in order of preference, for example
    /// <c>copilot.exe</c>, then <c>copilot.cmd</c>.
    /// </param>
    /// <param name="unixExecutableName">
    /// The file name that is looked for with the Unix rules, for example <c>copilot</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="windowsExecutableNames" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="windowsExecutableNames" /> is empty or contains a name that is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="unixExecutableName" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    public PathExecutableSearch(IReadOnlyList<string> windowsExecutableNames, string unixExecutableName)
        : this(windowsExecutableNames, unixExecutableName,
            static () => Environment.GetEnvironmentVariable(PathVariableName), OperatingSystem.IsWindows(),
            File.Exists, HasUnixExecuteBit)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathExecutableSearch" /> class with the specified executable
    /// names and the specified sources for the <c>PATH</c> value, the operating system rules, the file check, and the
    /// execute check.
    /// </summary>
    /// <param name="windowsExecutableName">
    /// The file name that is looked for with the Windows rules, for example <c>git.exe</c>.
    /// </param>
    /// <param name="unixExecutableName">
    /// The file name that is looked for with the Unix rules, for example <c>git</c>.
    /// </param>
    /// <param name="getPathVariable">
    /// The function that returns the value of the <c>PATH</c> environment variable, or <see langword="null" /> if it
    /// is not set.
    /// </param>
    /// <param name="isWindows">
    /// <see langword="true" /> to use the Windows rules (separator <c>;</c>, <paramref name="windowsExecutableName" />,
    /// drive and UNC paths); <see langword="false" /> to use the Unix rules (separator <c>:</c>,
    /// <paramref name="unixExecutableName" />, paths that start with <c>/</c>).
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
    /// <exception cref="ArgumentException">
    /// <paramref name="windowsExecutableName" /> or <paramref name="unixExecutableName" /> is <see langword="null" />,
    /// empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="getPathVariable" />, <paramref name="fileExists" />, or <paramref name="isExecutable" /> is
    /// <see langword="null" />.
    /// </exception>
    public PathExecutableSearch(string windowsExecutableName, string unixExecutableName,
        Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists, Func<string, bool> isExecutable)
        : this(ToNameList(windowsExecutableName), unixExecutableName, getPathVariable, isWindows, fileExists,
            isExecutable)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathExecutableSearch" /> class with the specified Windows
    /// executable names in order of preference, the specified Unix executable name, and the specified sources for the
    /// <c>PATH</c> value, the operating system rules, the file check, and the execute check.
    /// </summary>
    /// <param name="windowsExecutableNames">
    /// The file names that are looked for with the Windows rules, in order of preference, for example
    /// <c>copilot.exe</c>, then <c>copilot.cmd</c>. Each absolute <c>PATH</c> directory is checked for every name
    /// before the next directory.
    /// </param>
    /// <param name="unixExecutableName">
    /// The file name that is looked for with the Unix rules, for example <c>copilot</c>.
    /// </param>
    /// <param name="getPathVariable">
    /// The function that returns the value of the <c>PATH</c> environment variable, or <see langword="null" /> if it
    /// is not set.
    /// </param>
    /// <param name="isWindows">
    /// <see langword="true" /> to use the Windows rules (separator <c>;</c>, <paramref name="windowsExecutableNames" />,
    /// drive and UNC paths); <see langword="false" /> to use the Unix rules (separator <c>:</c>,
    /// <paramref name="unixExecutableName" />, paths that start with <c>/</c>).
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
    /// <paramref name="windowsExecutableNames" />, <paramref name="getPathVariable" />, <paramref name="fileExists" />,
    /// or <paramref name="isExecutable" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="windowsExecutableNames" /> is empty or contains a name that is <see langword="null" />, empty,
    /// or whitespace, or <paramref name="unixExecutableName" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    public PathExecutableSearch(IReadOnlyList<string> windowsExecutableNames, string unixExecutableName,
        Func<string?> getPathVariable, bool isWindows, Func<string, bool> fileExists, Func<string, bool> isExecutable)
    {
        Ensure.NotNull(windowsExecutableNames);

        if (windowsExecutableNames.Count == 0 || windowsExecutableNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one Windows executable name is required, and no name may be null, empty, or whitespace.",
                nameof(windowsExecutableNames));
        }

        Ensure.IsNotNullOrWhitespace(unixExecutableName);

        _getPathVariable = Ensure.NotNull(getPathVariable);
        _isWindows = isWindows;
        _executableNames = isWindows ? [.. windowsExecutableNames] : [unixExecutableName];
        _directorySeparator = isWindows ? WindowsDirectorySeparator : UnixDirectorySeparator;
        _fileExists = Ensure.NotNull(fileExists);
        _isExecutable = Ensure.NotNull(isExecutable);
    }

    /// <summary>
    /// Searches the absolute directories of the <c>PATH</c> value in order for the executable.
    /// </summary>
    /// <remarks>
    /// Each directory is checked for every executable name in order of preference before the next directory.
    /// </remarks>
    /// <returns>
    /// The path of the executable in the first absolute <c>PATH</c> directory that contains one of the names, with
    /// the earliest name that the directory contains, or <see langword="null" /> if the <c>PATH</c> value is not set,
    /// empty, or no absolute directory contains any of the names.
    /// </returns>
    public string? Find()
    {
        var pathVariable = _getPathVariable();

        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        var entries = _isWindows
            ? SplitWindowsPathVariable(pathVariable)
            : pathVariable.Split(UnixPathListSeparator, StringSplitOptions.RemoveEmptyEntries);

        // Directory by directory: every name is tried in a directory before the next directory is checked.
        return entries
            .Where(IsFullyQualified)
            .SelectMany(directory => _executableNames.Select(name => CreateCandidatePath(directory, name)))
            .FirstOrDefault(IsCandidate);
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
    public static bool HasUnixExecuteBit(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            return (File.GetUnixFileMode(path) & AnyExecute) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string[] ToNameList(string windowsExecutableName)
    {
        Ensure.IsNotNullOrWhitespace(windowsExecutableName);

        return [windowsExecutableName];
    }

    // Like the PATH search of a shell: on Unix, a file without any execute bit is skipped.
    private bool IsCandidate(string path)
    {
        return _fileExists(path) && (_isWindows || _isExecutable(path));
    }

    // Splits at ';' outside double quotation marks and removes the quotation marks, like cmd.exe does. Quotation
    // marks cannot be part of a Windows file name, so removing them never changes a valid directory.
    private static IEnumerable<string> SplitWindowsPathVariable(string pathVariable)
    {
        var entry = new StringBuilder();
        var isQuoted = false;

        foreach (var character in pathVariable)
        {
            if (character == Quote)
            {
                isQuoted = !isQuoted;
            }
            else if (character == WindowsPathListSeparator && !isQuoted)
            {
                if (entry.Length > 0)
                {
                    yield return entry.ToString();
                }

                entry.Clear();
            }
            else
            {
                entry.Append(character);
            }
        }

        if (entry.Length > 0)
        {
            yield return entry.ToString();
        }
    }

    private bool IsFullyQualified(string directory)
    {
        return _isWindows
            ? IsWindowsFullyQualified(directory)
            : directory.StartsWith(UnixDirectorySeparator);
    }

    /// <summary>
    /// Determines whether the specified path is fully qualified with the Windows rules, independent of the host
    /// operating system.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Path.IsPathFullyQualified(string)" /> on Windows. <c>C:Git</c> and <c>\Git</c> are relative
    /// to the current drive or directory, so they are not fully qualified.
    /// </remarks>
    /// <param name="path">The path to check.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="path" /> is a drive path such as <c>C:\Git\cmd</c> or starts with
    /// two separators (UNC <c>\\server\share</c>, device <c>\\?\C:\</c>); otherwise, <see langword="false" />.
    /// </returns>
    internal static bool IsWindowsFullyQualified(string path)
    {
        if (path.Length < 2)
        {
            return false;
        }

        if (IsWindowsDirectorySeparator(path[0]))
        {
            return path[1] == '?' || IsWindowsDirectorySeparator(path[1]);
        }

        return path.Length >= 3
               && char.IsAsciiLetter(path[0])
               && path[1] == ':'
               && IsWindowsDirectorySeparator(path[2]);
    }

    /// <summary>
    /// Determines whether the specified character separates directories with the Windows rules.
    /// </summary>
    /// <param name="character">The character to check.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="character" /> is <c>\</c> or <c>/</c>; otherwise,
    /// <see langword="false" />.
    /// </returns>
    internal static bool IsWindowsDirectorySeparator(char character)
    {
        return character is WindowsDirectorySeparator or WindowsAltDirectorySeparator;
    }

    private string CreateCandidatePath(string directory, string executableName)
    {
        var lastCharacter = directory[^1];

        var endsWithSeparator = _isWindows
            ? IsWindowsDirectorySeparator(lastCharacter)
            : lastCharacter == UnixDirectorySeparator;

        return endsWithSeparator
            ? directory + executableName
            : $"{directory}{_directorySeparator}{executableName}";
    }
}
