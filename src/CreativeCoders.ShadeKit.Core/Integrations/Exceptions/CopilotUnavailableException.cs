using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the GitHub Copilot CLI <c>copilot</c> cannot be started, so a
/// <c>copilot</c> command cannot be run.
/// </summary>
/// <remarks>
/// Callers tell a missing or unusable <c>copilot</c> from a <c>copilot</c> that runs and fails by this type; a
/// <c>copilot</c> command that runs and fails is reported as a <see cref="CopilotCommandFailedException" />.
/// </remarks>
/// <param name="arguments">The arguments of the <c>copilot</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes why <c>copilot</c> could not be started.</param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public class CopilotUnavailableException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : ExternalToolException("copilot", "cannot be run", arguments, reason, innerException);
