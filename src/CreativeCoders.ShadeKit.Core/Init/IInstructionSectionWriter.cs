namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Writes the managed SHADE kit section into an agent instruction file such as <c>CLAUDE.md</c> or
/// <c>AGENTS.md</c>.
/// </summary>
/// <remarks>
/// The section starts with a line whose text, without leading and trailing whitespace, starts with
/// <see cref="ManagedInstructionSection.BeginMarkerPrefix" />, and ends with a line whose text, without leading and
/// trailing whitespace, is <see cref="ManagedInstructionSection.EndMarker" />. The markers are invalid if a file
/// contains more than one begin or end marker, a begin marker without an end marker, an end marker without a begin
/// marker, or an end marker before the begin marker.
/// </remarks>
public interface IInstructionSectionWriter
{
    /// <summary>
    /// Checks that an instruction file can be changed by <see cref="ApplyAsync" />, in particular that the markers of
    /// the managed section are valid, without changing it.
    /// </summary>
    /// <param name="filePath">The absolute path of the instruction file. A missing file is valid.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous check.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark.
    /// </exception>
    /// <exception cref="InvalidManagedSectionException">The markers in the file are invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task ValidateAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the managed section <see cref="ManagedInstructionSection.Text" /> into an instruction file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A missing file is created with only the section, as UTF-8 without byte order mark and with <c>\n</c> line
    /// terminators. To a file without markers, the section is appended after one blank line, or directly if the file
    /// already ends with an empty line. In a file with one valid pair of markers, the lines from the begin marker to
    /// the end marker are replaced by the section.
    /// </para>
    /// <para>
    /// In an existing file, every byte outside the inserted or replaced part stays unchanged, a leading UTF-8 byte
    /// order mark is kept, a missing final line terminator is added before appending, and the section uses
    /// <c>\r\n</c> line terminators if the file contains one. A file with invalid markers or that is not UTF-8 is not
    /// changed.
    /// </para>
    /// </remarks>
    /// <param name="filePath">The absolute path of the instruction file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous write operation. The task result contains
    /// <see cref="InitializedFileAction.Created" />, <see cref="InitializedFileAction.Appended" />, or
    /// <see cref="InitializedFileAction.Updated" />, or <see langword="null" /> if the file already contained the
    /// section exactly and was not changed.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark.
    /// </exception>
    /// <exception cref="InvalidManagedSectionException">The markers in the file are invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<InitializedFileAction?> ApplyAsync(string filePath, CancellationToken cancellationToken = default);
}
