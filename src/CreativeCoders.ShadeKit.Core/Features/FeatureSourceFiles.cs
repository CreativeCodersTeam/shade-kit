namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Resolves the file that the source of a new feature names, by one rule for every caller.
/// </summary>
/// <remarks>
/// <see cref="FeatureIdFactory" /> decides with it whether a source is a <see cref="FeatureSourceKind.File" />, and
/// <see cref="DiscussionNotesMove.EnsureFileSource" /> refuses a source that names no file before its content is
/// loaded; one rule keeps the two decisions from drifting apart.
/// </remarks>
internal static class FeatureSourceFiles
{
    /// <summary>
    /// Finds the existing file that a source names.
    /// </summary>
    /// <remarks>
    /// <see cref="File.Exists" /> decides, so on Unix devices, FIFOs, sockets, and symbolic links whose target is
    /// missing count as files too (package 03, file sources).
    /// </remarks>
    /// <param name="source">The source as it was passed, for example the value of <c>--source</c>.</param>
    /// <param name="workingDirectory">The directory against which a relative path in the source is resolved.</param>
    /// <returns>
    /// The full path of the file, with <c>.</c> and <c>..</c> segments resolved; or <see langword="null" /> if the
    /// source names no existing file, for example a directory, a missing path, a URL, or text that is not a valid
    /// path.
    /// </returns>
    public static string? FindFilePath(string source, string workingDirectory)
    {
        try
        {
            var fullPath = Path.GetFullPath(source, Path.GetFullPath(workingDirectory));

            return File.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                              or PathTooLongException)
        {
            // Text that is not a valid path, for example because it contains a null character, is no file.
            return null;
        }
    }
}
