using CreativeCoders.ShadeKit.Core.Files.Exceptions;
using CreativeCoders.ShadeKit.Core.Init.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Plans the managed SHADE kit section of an agent instruction file such as <c>CLAUDE.md</c> or
/// <c>AGENTS.md</c>.
/// </summary>
/// <remarks>
/// The section runs from a line starting with <see cref="ManagedInstructionSection.BeginMarkerPrefix" /> to the
/// line <see cref="ManagedInstructionSection.EndMarker" />, both trimmed. Lines in fenced code blocks are no
/// markers. A file with more than one pair, an unpaired or reversed marker, or an unclosed code block is invalid.
/// </remarks>
public interface IInstructionSectionPlanner
{
    /// <summary>
    /// Computes the change that writes the managed section <see cref="ManagedInstructionSection.Text" /> into an
    /// instruction file, without writing it.
    /// </summary>
    /// <remarks>
    /// A missing file gets only the section; otherwise the section is appended after one blank line or replaces the
    /// lines between the markers. Existing lines and a byte order mark are kept. A file whose section is already
    /// current needs no change, whatever its line endings.
    /// </remarks>
    /// <param name="filePath">The absolute path of the instruction file.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The task result contains the new content with
    /// <see cref="InitializedFileAction.Created" />, <see cref="InitializedFileAction.Appended" />, or
    /// <see cref="InitializedFileAction.Updated" />, or <see langword="null" /> if the file already contains the
    /// section with the same lines and needs no change.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath" /> is <see langword="null" />, empty, whitespace, or not an absolute path.
    /// </exception>
    /// <exception cref="PathNotAFileException"><paramref name="filePath" /> is an existing directory.</exception>
    /// <exception cref="UnsupportedFileEncodingException">
    /// The file starts with a UTF-16 or UTF-32 byte order mark, contains a NUL byte, or contains bytes that are not
    /// valid UTF-8.
    /// </exception>
    /// <exception cref="InvalidManagedSectionException">The markers in the file are invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    Task<PlannedFileChange?> PlanAsync(string filePath, CancellationToken cancellationToken = default);
}
