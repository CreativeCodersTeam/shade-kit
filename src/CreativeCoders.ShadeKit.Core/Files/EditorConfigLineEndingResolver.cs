using CreativeCoders.Core;
using EditorConfig.Core;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Determines the line ending that the <c>.editorconfig</c> files prescribe for a file with the EditorConfig core
/// library.
/// </summary>
/// <remarks>
/// Every call creates a new <see cref="EditorConfigParser" />, because a parser keeps the <c>.editorconfig</c> files
/// it found for a directory until it is discarded and would not notice files that are created or deleted later. The
/// parser reads each <c>.editorconfig</c> file through a factory of this class, so the path of a file that cannot be
/// read is known for the warning, and the files that were read can be evaluated one by one to find a file whose
/// content the parser cannot evaluate.
/// </remarks>
public sealed class EditorConfigLineEndingResolver : IEditorConfigLineEndingResolver
{
    private static readonly EditorConfigLineEndingResult NoLineEndingResult =
        new EditorConfigLineEndingResult(null, null);

    private static readonly EditorConfigLineEndingResult LfResult =
        new EditorConfigLineEndingResult(LineEnding.Lf, null);

    private static readonly EditorConfigLineEndingResult CrLfResult =
        new EditorConfigLineEndingResult(LineEnding.CrLf, null);

    private readonly Func<EditorConfigParser, string, FileConfiguration> _parse;

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
                var editorConfigFile = EditorConfigFile.Parse(editorConfigPath);
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
            return new EditorConfigLineEndingResult(null, unreadableEditorConfigPath is null
                ? new EditorConfigUnreadableWarning(filePath, exception.Message, IsEditorConfigFilePath: false)
                : new EditorConfigUnreadableWarning(unreadableEditorConfigPath, exception.Message));
        }
        catch (Exception exception) when (IsInvalidContentException(exception))
        {
            // As for an unreadable file, none of the .editorconfig files decides.
            var invalidEditorConfigPath = FindInvalidEditorConfigPath(filePath, readEditorConfigFiles);

            return new EditorConfigLineEndingResult(null, invalidEditorConfigPath is null
                ? new EditorConfigInvalidWarning(filePath, exception.Message, IsEditorConfigFilePath: false)
                : new EditorConfigInvalidWarning(invalidEditorConfigPath, exception.Message));
        }

        return configuration.EndOfLine switch
        {
            EndOfLine.LF => LfResult,
            EndOfLine.CRLF => CrLfResult,
            _ => NoLineEndingResult
        };
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
}
