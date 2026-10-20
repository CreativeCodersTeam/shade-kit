using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Plans the managed SHADE kit section of an agent instruction file such as <c>CLAUDE.md</c> or
/// <c>AGENTS.md</c>.
/// </summary>
/// <remarks>
/// The section starts with a line whose text, without leading and trailing whitespace, starts with
/// <see cref="ManagedInstructionSection.BeginMarkerPrefix" /> followed by whitespace or <c>--&gt;</c>, and ends with a
/// line whose text, without leading and trailing whitespace, is <see cref="ManagedInstructionSection.EndMarker" />.
/// Lines in a fenced code block (opened by at least three backticks or tildes, indented by at most three spaces) are
/// no markers, so the markers can be documented in a code example. A code block that is still open at the end of the
/// file makes the file invalid, because a section appended after it would be part of the block. The markers are
/// invalid if a file
/// contains more than one begin or end marker, a begin marker without an end marker, an end marker without a begin
/// marker, or an end marker before the begin marker.
/// </remarks>
public interface IInstructionSectionPlanner
{
    /// <summary>
    /// Computes the change that writes the managed section <see cref="ManagedInstructionSection.Text" /> into an
    /// instruction file, without writing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A missing file gets only the section, as UTF-8 without byte order mark and with <c>\n</c> line
    /// terminators. To a file without markers, the section is appended after one blank line, or directly if the file
    /// already ends with an empty line. In a file with one valid pair of markers, the lines from the begin marker to
    /// the end marker are replaced by the section.
    /// </para>
    /// <para>
    /// In an existing file, every byte outside the inserted or replaced part stays unchanged, a leading UTF-8 byte
    /// order mark is kept, a missing final line terminator is added before appending, and the section uses
    /// <c>\r\n</c> line terminators if the file contains one. A file with invalid markers or that is not UTF-8 is
    /// refused.
    /// </para>
    /// </remarks>
    /// <param name="filePath">The absolute path of the instruction file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the new content with
    /// <see cref="InitializedFileAction.Created" />, <see cref="InitializedFileAction.Appended" />, or
    /// <see cref="InitializedFileAction.Updated" />, or <see langword="null" /> if the file already contains the
    /// section exactly and needs no change.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark or contains a NUL byte.
    /// </exception>
    /// <exception cref="InvalidManagedSectionException">The markers in the file are invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<PlannedFileChange?> PlanAsync(string filePath, CancellationToken cancellationToken = default);
}
