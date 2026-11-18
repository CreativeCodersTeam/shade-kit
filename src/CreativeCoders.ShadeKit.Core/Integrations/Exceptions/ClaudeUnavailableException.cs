using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the Claude Code CLI <c>claude</c> cannot be started, so a
/// <c>claude</c> command cannot be run.
/// </summary>
/// <remarks>
/// Callers tell a missing or unusable <c>claude</c> from a <c>claude</c> that runs and fails by this type; a
/// <c>claude</c> command that runs and fails is reported as a <see cref="ClaudeCommandFailedException" />.
/// </remarks>
/// <param name="arguments">The arguments of the <c>claude</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes why <c>claude</c> could not be started.</param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public class ClaudeUnavailableException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : ExternalToolException("claude", "cannot be run", arguments, reason, innerException);
