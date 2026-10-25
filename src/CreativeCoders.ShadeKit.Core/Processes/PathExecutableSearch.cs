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
/// is not evaluated, so only the Windows executable name is looked for. Every call of <see cref="Find" /> reads the
/// <c>PATH</c> value again; the result is not cached.
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

    private readonly string _executableName;

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
        : this(windowsExecutableName, unixExecutableName,
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
    {
        Ensure.IsNotNullOrWhitespace(windowsExecutableName);
        Ensure.IsNotNullOrWhitespace(unixExecutableName);

        _getPathVariable = Ensure.NotNull(getPathVariable);
        _isWindows = isWindows;
        _executableName = isWindows ? windowsExecutableName : unixExecutableName;
        _directorySeparator = isWindows ? WindowsDirectorySeparator : UnixDirectorySeparator;
        _fileExists = Ensure.NotNull(fileExists);
        _isExecutable = Ensure.NotNull(isExecutable);
    }

    /// <summary>
    /// Searches the absolute directories of the <c>PATH</c> value in order for the executable.
    /// </summary>
    /// <returns>
    /// The path of the executable in the first absolute <c>PATH</c> directory that contains it, or
    /// <see langword="null" /> if the <c>PATH</c> value is not set, empty, or no absolute directory contains it.
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

        return entries
            .Where(IsFullyQualified)
            .Select(CreateCandidatePath)
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

    // Mirrors Path.IsPathFullyQualified on Windows, independent of the host operating system: a drive path such as
    // "C:\Git\cmd" or a path that starts with two separators (UNC "\\server\share", device "\\?\C:\"). "C:Git" and
    // "\Git" are relative to the current drive or directory.
    private static bool IsWindowsFullyQualified(string directory)
    {
        if (directory.Length < 2)
        {
            return false;
        }

        if (IsWindowsDirectorySeparator(directory[0]))
        {
            return directory[1] == '?' || IsWindowsDirectorySeparator(directory[1]);
        }

        return directory.Length >= 3
               && char.IsAsciiLetter(directory[0])
               && directory[1] == ':'
               && IsWindowsDirectorySeparator(directory[2]);
    }

    private static bool IsWindowsDirectorySeparator(char character)
    {
        return character is WindowsDirectorySeparator or WindowsAltDirectorySeparator;
    }

    private string CreateCandidatePath(string directory)
    {
        var lastCharacter = directory[^1];

        var endsWithSeparator = _isWindows
            ? IsWindowsDirectorySeparator(lastCharacter)
            : lastCharacter == UnixDirectorySeparator;

        return endsWithSeparator
            ? directory + _executableName
            : $"{directory}{_directorySeparator}{_executableName}";
    }
}
