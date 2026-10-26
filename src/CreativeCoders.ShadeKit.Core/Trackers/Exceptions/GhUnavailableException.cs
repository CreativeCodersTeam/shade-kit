using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the GitHub CLI <c>gh</c> cannot be started, so a <c>gh</c> command
/// cannot be run.
/// </summary>
/// <remarks>
/// Callers tell a missing or unusable <c>gh</c> from a <c>gh</c> that runs and fails by this type; a <c>gh</c> that
/// exits with an error is reported by the caller that interprets its exit code.
/// </remarks>
/// <param name="arguments">The arguments of the <c>gh</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes why <c>gh</c> could not be started.</param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public class GhUnavailableException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : ExternalToolUnavailableException("gh", "cannot be run", arguments, reason, innerException);
