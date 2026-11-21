using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the move of discussion notes from <c>.shadekit/discussions/</c> into the <c>discussions/</c> folder of
/// a new feature, which <c>shade-kit feature new --move-source</c> asks for.
/// </summary>
/// <remarks>
/// Only a file directly in <c>.shadekit/discussions/</c> can be moved: the notes that the discuss skill writes without
/// a current feature. A file in a subfolder, the notes of another feature, and every other file stay where they are,
/// so the move never takes a file of the repository away from its place.
/// </remarks>
internal sealed class DiscussionNotesMove
{
    private static readonly string DiscussionsPathPrefix =
        KitStorePaths.GetRepositoryPath($"{KitStorePaths.DiscussionsDirName}/");

    private DiscussionNotesMove(string fileName)
    {
        OriginalPath = DiscussionsPathPrefix + fileName;
        KitRelativeOriginalPath = $"{KitStorePaths.DiscussionsDirName}/{fileName}";

        // The notes keep their place below their parent: discussions/<file> below the kit root before the move, and
        // below the feature folder after it. The two paths are equal by that rule, not by chance.
        FeatureRelativePath = KitRelativeOriginalPath;
    }

    /// <summary>
    /// Gets the path of the notes before the move.
    /// </summary>
    /// <value>
    /// The path relative to the repository root, with <c>/</c> as separator, for example
    /// <c>.shadekit/discussions/20261020-1500-login-sso.md</c>.
    /// </value>
    public string OriginalPath { get; }

    /// <summary>
    /// Gets the path of the notes before the move, relative to the SHADE kit root directory.
    /// </summary>
    /// <value>The path with <c>/</c> as separator, for example <c>discussions/20261020-1500-login-sso.md</c>.</value>
    public string KitRelativeOriginalPath { get; }

    /// <summary>
    /// Gets the path of the moved notes relative to the folder of the new feature.
    /// </summary>
    /// <value>The path with <c>/</c> as separator, for example <c>discussions/20261020-1500-login-sso.md</c>.</value>
    public string FeatureRelativePath { get; }

    /// <summary>
    /// Refuses a source that cannot name a file at all, before its content is loaded.
    /// </summary>
    /// <remarks>
    /// Only a file can be moved, so a source that names no existing file (a URL or a text) is refused before
    /// <c>gh</c> runs or an HTTP request is sent. Whether the file is discussion notes directly in
    /// <c>.shadekit/discussions/</c> is checked by <see cref="Create" /> once the source is resolved.
    /// </remarks>
    /// <param name="sourceText">The source as it was passed, for example the value of <c>--source</c>.</param>
    /// <param name="workingDirectory">The directory against which a relative path in the source is resolved.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sourceText" /> or <paramref name="workingDirectory" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidFeatureSourceException">
    /// <paramref name="sourceText" /> names no existing file; its <see cref="InvalidFeatureSourceException.Problem" />
    /// is <see cref="FeatureSourceProblem.SourceNotMovable" />.
    /// </exception>
    public static void EnsureFileSource(string sourceText, string workingDirectory)
    {
        Ensure.NotNull(sourceText);
        Ensure.NotNull(workingDirectory);

        if (FeatureSourceFiles.FindFilePath(sourceText, workingDirectory) is null)
        {
            throw CreateNotMovableException(sourceText);
        }
    }

    /// <summary>
    /// Creates the move of a feature source, or refuses a source that is not discussion notes directly in
    /// <c>.shadekit/discussions/</c>.
    /// </summary>
    /// <param name="source">The source as it is recorded in <c>feature.json</c> before the move.</param>
    /// <param name="sourceText">The source as it was passed, for example the value of <c>--source</c>.</param>
    /// <returns>The move of the file that <paramref name="source" /> names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidFeatureSourceException">
    /// <paramref name="source" /> is not a <see cref="FeatureSourceKind.File" /> source directly in
    /// <c>.shadekit/discussions/</c>; its <see cref="InvalidFeatureSourceException.Problem" /> is
    /// <see cref="FeatureSourceProblem.SourceNotMovable" />.
    /// </exception>
    public static DiscussionNotesMove Create(FeatureSource source, string sourceText)
    {
        Ensure.NotNull(source);

        // A file source inside the repository is recorded relative to the repository root with '/' as separator and
        // without '.' or '..' segments, so a plain ordinal prefix check is enough; a file outside the repository is
        // recorded with its absolute path and never matches.
        var fileName = source is { Kind: FeatureSourceKind.File, Value: { } value }
                       && value.StartsWith(DiscussionsPathPrefix, StringComparison.Ordinal)
            ? value[DiscussionsPathPrefix.Length..]
            : null;

        if (string.IsNullOrEmpty(fileName) || fileName.Contains('/', StringComparison.Ordinal))
        {
            throw CreateNotMovableException(sourceText);
        }

        return new DiscussionNotesMove(fileName);
    }

    /// <summary>
    /// Creates the source of the new feature after the move.
    /// </summary>
    /// <param name="featureId">The id of the new feature.</param>
    /// <returns>
    /// A <see cref="FeatureSourceKind.File" /> source whose value is the path of the moved notes relative to the
    /// repository root, for example <c>.shadekit/features/20261020-1530-login-sso/discussions/login.md</c>.
    /// </returns>
    public FeatureSource CreateMovedSource(string featureId)
    {
        return new FeatureSource
        {
            Kind = FeatureSourceKind.File,
            Value = KitStorePaths.GetRepositoryFeatureDirPath(featureId) + FeatureRelativePath
        };
    }

    private static InvalidFeatureSourceException CreateNotMovableException(string sourceText)
    {
        return new InvalidFeatureSourceException(sourceText,
            $"--move-source moves only a file directly in '{DiscussionsPathPrefix}'.",
            problem: FeatureSourceProblem.SourceNotMovable);
    }
}
