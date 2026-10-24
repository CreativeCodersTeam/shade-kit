using System.Collections.Concurrent;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files.Warnings;
using EditorConfig.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Determines the line ending that the <c>.editorconfig</c> files prescribe for a file with the EditorConfig core
/// library.
/// </summary>
/// <remarks>
/// Each call uses a new parser, so created or deleted <c>.editorconfig</c> files are noticed. Parsed files are kept
/// for the lifetime of the instance (one command run) and parsed again only if their write time, size, or Unix
/// mode changed.
/// </remarks>
public sealed class EditorConfigLineEndingResolver : IEditorConfigLineEndingResolver
{
    private readonly Func<EditorConfigParser, string, FileConfiguration> _parse;

    private readonly ConcurrentDictionary<string, (FileStamp Stamp, EditorConfigFile File)> _parsedFiles =
        new ConcurrentDictionary<string, (FileStamp Stamp, EditorConfigFile File)>(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorConfigLineEndingResolver" /> class.
    /// </summary>
    public EditorConfigLineEndingResolver()
        : this(static (parser, filePath) => parser.Parse(filePath, editorConfigFiles: null))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EditorConfigLineEndingResolver" /> class that evaluates the
    /// <c>.editorconfig</c> files with the specified function.
    /// </summary>
    /// <remarks>
    /// Tests use this constructor to simulate a parser failure that cannot be traced to an <c>.editorconfig</c> file.
    /// </remarks>
    /// <param name="parse">
    /// A function that evaluates the <c>.editorconfig</c> files for a file path with the parser it gets, which reads
    /// each <c>.editorconfig</c> file through the factory of this class.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="parse" /> is <see langword="null" />.</exception>
    internal EditorConfigLineEndingResolver(Func<EditorConfigParser, string, FileConfiguration> parse)
    {
        _parse = Ensure.NotNull(parse);
    }

    /// <inheritdoc />
    public EditorConfigLineEndingResult GetLineEnding(string filePath)
    {
        Ensure.IsNotNullOrWhitespace(filePath);

        if (!Path.IsPathFullyQualified(filePath))
        {
            throw new ArgumentException($"The path '{filePath}' must be absolute.", nameof(filePath));
        }

        string? unreadableEditorConfigPath = null;
        var readEditorConfigFiles = new List<(string Path, EditorConfigFile File)>();

        var parser = new EditorConfigParser(editorConfigPath =>
        {
            try
            {
                var editorConfigFile = GetEditorConfigFile(editorConfigPath);
                readEditorConfigFiles.Add((editorConfigPath, editorConfigFile));

                return editorConfigFile;
            }
            catch (Exception exception) when (IsReadException(exception))
            {
                unreadableEditorConfigPath = editorConfigPath;

                throw;
            }
        });

        FileConfiguration configuration;

        try
        {
            configuration = _parse(parser, filePath);
        }
        catch (Exception exception) when (IsReadException(exception))
        {
            // The remaining .editorconfig files alone could prescribe a wrong line ending, for example if the
            // unreadable file is a nested one with root = true; therefore none of them decides.
            return new EditorConfigLineEndingResult(null,
                new EditorConfigUnreadableWarning(unreadableEditorConfigPath ?? filePath, exception.Message,
                    IsEditorConfigFilePath: unreadableEditorConfigPath is not null));
        }
        catch (Exception exception) when (IsInvalidContentException(exception))
        {
            // As for an unreadable file, none of the .editorconfig files decides.
            var invalidEditorConfigPath = FindInvalidEditorConfigPath(filePath, readEditorConfigFiles);

            return new EditorConfigLineEndingResult(null,
                new EditorConfigInvalidWarning(invalidEditorConfigPath ?? filePath, exception.Message,
                    IsEditorConfigFilePath: invalidEditorConfigPath is not null));
        }

        var lineEnding = configuration.EndOfLine switch
        {
            EndOfLine.LF => LineEnding.Lf,
            EndOfLine.CRLF => LineEnding.CrLf,
            _ => (LineEnding?)null
        };

        return new EditorConfigLineEndingResult(lineEnding, null);
    }

    // Returns the kept file while its stamp is unchanged; a file that cannot be read is not kept, so it is read again
    // by the next call.
    private EditorConfigFile GetEditorConfigFile(string editorConfigPath)
    {
        var stamp = FileStamp.Read(editorConfigPath);

        if (_parsedFiles.TryGetValue(editorConfigPath, out var parsedFile) && parsedFile.Stamp == stamp)
        {
            return parsedFile.File;
        }

        var editorConfigFile = EditorConfigFile.Parse(editorConfigPath);
        _parsedFiles[editorConfigPath] = (stamp, editorConfigFile);

        return editorConfigFile;
    }

    // Evaluates the sections of each .editorconfig file that was read on its own and returns the path of the first
    // file whose content the parser cannot evaluate, or null if none fails on its own.
    private static string? FindInvalidEditorConfigPath(string filePath,
        IEnumerable<(string Path, EditorConfigFile File)> readEditorConfigFiles)
    {
        foreach (var (editorConfigPath, editorConfigFile) in readEditorConfigFiles)
        {
            try
            {
                new EditorConfigParser().Parse(filePath, [editorConfigFile]);
            }
            catch (Exception exception) when (IsInvalidContentException(exception))
            {
                return editorConfigPath;
            }
        }

        return null;
    }

    // The errors with which reading an .editorconfig file fails: UnauthorizedAccessException for missing permissions,
    // IOException and its subclasses (FileNotFoundException, DirectoryNotFoundException, ...) for I/O errors.
    private static bool IsReadException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }

    // The errors with which the parser fails on content that it reads but cannot evaluate. The parser reads the lines
    // of a file leniently and ignores lines it does not understand, but it expands the numeric ranges of a section
    // header ({1..3}) when it matches the header against a path: a number that does not fit into an int or a range
    // whose length overflows throws OverflowException, a negative list capacity after an overflowing multiplication
    // of several ranges throws ArgumentOutOfRangeException, and a list capacity above the maximum array length throws
    // OutOfMemoryException ("Array dimensions exceeded supported range"), for example for [x{1..2147483647}].
    // OutOfMemoryException is caught only because of this known parser behaviour: the parser asks for the oversized
    // array before it allocates anything, so the process is not short of memory. OperationCanceledException is never
    // caught.
    private static bool IsInvalidContentException(Exception exception)
    {
        return exception is OverflowException or ArgumentOutOfRangeException or OutOfMemoryException;
    }

    // What tells a changed .editorconfig file from the kept one: a new content changes the time of last write or the
    // size, and a change of permissions, which can make the file unreadable, changes the Unix file mode.
    private readonly record struct FileStamp(DateTime LastWriteTimeUtc, long Length, UnixFileMode UnixFileMode)
    {
        // Throws FileNotFoundException for a file that was deleted after the parser found it, which the factory
        // reports like any other file that cannot be read.
        public static FileStamp Read(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            return new FileStamp(fileInfo.LastWriteTimeUtc, fileInfo.Length,
                OperatingSystem.IsWindows() ? default : fileInfo.UnixFileMode);
        }
    }
}
