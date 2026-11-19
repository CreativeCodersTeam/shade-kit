using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the OpenAI Codex CLI <c>codex</c> cannot be started, so a
/// <c>codex</c> command cannot be run.
/// </summary>
/// <remarks>
/// Callers tell a missing or unusable <c>codex</c> from a <c>codex</c> that runs and fails by this type; a
/// <c>codex</c> command that runs and fails is reported as a <see cref="CodexCommandFailedException" />.
/// </remarks>
/// <param name="arguments">The arguments of the <c>codex</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes why <c>codex</c> could not be started.</param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public class CodexUnavailableException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : ExternalToolException("codex", "cannot be run", arguments, reason, innerException);
