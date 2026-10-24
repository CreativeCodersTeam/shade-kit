using CreativeCoders.ShadeKit.Core.Files;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

/// <summary>
/// Creates line ending resolvers and text file writers that always use one line ending, so that tests do not depend
/// on the operating system, the git configuration, or <c>.editorconfig</c> files.
/// </summary>
internal static class FixedLineEnding
{
    /// <summary>
    /// Creates a resolver that returns the specified line ending and warnings for every file.
    /// </summary>
    public static ILineEndingResolver CreateResolver(LineEnding lineEnding, params KitWarning[] warnings)
    {
        var resolver = A.Fake<ILineEndingResolver>();

        A.CallTo(() => resolver.GetLineEndingAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() => new LineEndingResolution(lineEnding, warnings));

        return resolver;
    }

    /// <summary>
    /// Creates a real <see cref="TextFileWriter" /> that writes every file with the specified line ending.
    /// </summary>
    public static TextFileWriter CreateWriter(LineEnding lineEnding, params KitWarning[] warnings)
    {
        return new TextFileWriter(CreateResolver(lineEnding, warnings));
    }

    /// <summary>
    /// Creates a real <see cref="TextFileWriter" /> that writes every file with the specified line ending and returns
    /// the specified warnings only for the first file it writes, so a test can tell the warnings of an earlier
    /// operation from those of a later one.
    /// </summary>
    public static TextFileWriter CreateWriterWithWarningsOnFirstWrite(LineEnding lineEnding,
        params KitWarning[] warnings)
    {
        var resolver = A.Fake<ILineEndingResolver>();
        var calls = 0;

        A.CallTo(() => resolver.GetLineEndingAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
                new LineEndingResolution(lineEnding, Interlocked.Increment(ref calls) == 1 ? warnings : []));

        return new TextFileWriter(resolver);
    }

    /// <summary>
    /// Creates a resolver that records the path of every file it is asked for and returns
    /// <see cref="LineEnding.CrLf" /> for the paths that start with <paramref name="crLfPathPrefix" /> and
    /// <see cref="LineEnding.Lf" /> for all other paths.
    /// </summary>
    /// <param name="crLfPathPrefix">The prefix, with forward slashes, of the paths that get CRLF.</param>
    /// <param name="requestedPaths">
    /// The list to which the requested paths are added, with forward slashes, in the order of the requests.
    /// </param>
    public static ILineEndingResolver CreateRecordingResolver(string crLfPathPrefix, List<string> requestedPaths)
    {
        var resolver = A.Fake<ILineEndingResolver>();

        A.CallTo(() => resolver.GetLineEndingAsync(A<string>._, A<string>._, A<CancellationToken>._))
            .ReturnsLazily((string _, string relativePath, CancellationToken _) =>
            {
                var path = relativePath.Replace('\\', '/');
                requestedPaths.Add(path);

                return new LineEndingResolution(
                    path.StartsWith(crLfPathPrefix, StringComparison.Ordinal) ? LineEnding.CrLf : LineEnding.Lf, []);
            });

        return resolver;
    }

    /// <summary>
    /// Writes content to an absolute file path with the specified line ending.
    /// </summary>
    public static async Task WriteAsync(string filePath, TextFileContent content, LineEnding lineEnding,
        CancellationToken cancellationToken)
    {
        await CreateWriter(lineEnding).WriteAsync(Path.GetDirectoryName(filePath)!, Path.GetFileName(filePath),
            content, cancellationToken: cancellationToken);
    }
}
