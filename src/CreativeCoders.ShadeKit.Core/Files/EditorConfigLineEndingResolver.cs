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
/// mode changed. A file with a section glob that the parser would expand into more than
/// <see cref="EditorConfigGlobExpansion.MaxPatternCount" /> patterns is reported as invalid before the parser
/// expands it.
/// </remarks>
public sealed class EditorConfigLineEndingResolver : IEditorConfigLineEndingResolver
{
    private readonly Func<EditorConfigParser, string, FileConfiguration> _parse;

    private readonly ConcurrentDictionary<string, ParsedFile> _parsedFiles =
        new ConcurrentDictionary<string, ParsedFile>(StringComparer.Ordinal);

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
        (string Path, string Reason)? tooLargeGlobError = null;
        var readEditorConfigFiles = new List<(string Path, EditorConfigFile File)>();

        var parser = new EditorConfigParser(editorConfigPath =>
        {
            try
            {
                var parsedFile = GetEditorConfigFile(editorConfigPath);

                // Thrown before the parser expands the glob, so the expansion never takes the memory or the time.
                // The parser gets an empty file instead, so it never expands the glob, but still reads the other
                // files; a file that cannot be read is then reported first, as for any other invalid content.
                if (parsedFile.TooLargeGlob is { } tooLargeGlob)
                {
                    tooLargeGlobError ??= (editorConfigPath,
                        $"The section glob '{tooLargeGlob}' expands into more than " +
                        $"{EditorConfigGlobExpansion.MaxPatternCount} patterns.");

                    return CreateEmptyEditorConfigFile(editorConfigPath);
                }

                readEditorConfigFiles.Add((editorConfigPath, parsedFile.File));

                return parsedFile.File;
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
            // As for an unreadable file, none of the .editorconfig files decides; a glob that is too large was found
            // first, while the files were read.
            if (tooLargeGlobError is { } error)
            {
                return new EditorConfigLineEndingResult(null, new EditorConfigInvalidWarning(error.Path, error.Reason));
            }

            var invalidEditorConfigPath = FindInvalidEditorConfigPath(filePath, readEditorConfigFiles);

            return new EditorConfigLineEndingResult(null,
                new EditorConfigInvalidWarning(invalidEditorConfigPath ?? filePath, exception.Message,
                    IsEditorConfigFilePath: invalidEditorConfigPath is not null));
        }

        // The other files were evaluated without the file with the glob that is too large, so none of them decides.
        if (tooLargeGlobError is { } tooLargeError)
        {
            return new EditorConfigLineEndingResult(null,
                new EditorConfigInvalidWarning(tooLargeError.Path, tooLargeError.Reason));
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
    private ParsedFile GetEditorConfigFile(string editorConfigPath)
    {
        var stamp = FileStamp.Read(editorConfigPath);

        if (_parsedFiles.TryGetValue(editorConfigPath, out var parsedFile) && parsedFile.Stamp == stamp)
        {
            return parsedFile;
        }

        var editorConfigFile = EditorConfigFile.Parse(editorConfigPath);
        var tooLargeGlob = editorConfigFile.Sections
            .Select(section => section.Glob)
            .FirstOrDefault(EditorConfigGlobExpansion.IsTooLarge);

        parsedFile = new ParsedFile(stamp, editorConfigFile, tooLargeGlob);
        _parsedFiles[editorConfigPath] = parsedFile;

        return parsedFile;
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
    // of a file leniently and ignores lines it does not understand, but it expands the braces of a section header
    // ({a,b}, {1..3}) when it matches the header against a path: a number that does not fit into an int throws
    // OverflowException, and a negative list capacity after an overflowing range throws ArgumentOutOfRangeException.
    // A file with a header that expands into too many patterns never reaches the parser, so an OutOfMemoryException is a
    // real lack of memory and is not caught. OperationCanceledException is never caught either.
    private static bool IsInvalidContentException(Exception exception)
    {
        return exception is OverflowException or ArgumentOutOfRangeException;
    }

    // An .editorconfig file without sections or properties in the directory of the specified file, which the parser
    // gets instead of a file with a glob that is too large.
    private static EditorConfigFile CreateEmptyEditorConfigFile(string editorConfigPath)
    {
        using var reader = new StringReader(string.Empty);

        return EditorConfigFile.Parse(reader, Path.GetDirectoryName(editorConfigPath),
            Path.GetFileName(editorConfigPath));
    }

    // A parsed .editorconfig file with the stamp it had when it was parsed and its first section glob that is too
    // large, or null if there is none.
    private sealed record ParsedFile(FileStamp Stamp, EditorConfigFile File, string? TooLargeGlob);

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
