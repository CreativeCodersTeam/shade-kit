using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a command of the GitHub Copilot CLI <c>copilot</c> fails: it exits
/// with a non-zero exit code, prints no JSON output that SHADE kit can read, or is refused before it starts.
/// </summary>
/// <remarks>
/// A <c>copilot</c> that cannot be found or started is reported as a <see cref="CopilotUnavailableException" />
/// instead. A command is refused before it starts when <c>copilot</c> is a Windows <c>.cmd</c> shim and its path or
/// an argument contains a character that <c>cmd.exe</c> would interpret.
/// </remarks>
/// <param name="arguments">The arguments of the <c>copilot</c> command that failed.</param>
/// <param name="exitCode">
/// The exit code of the <c>copilot</c> process, or <see langword="null" /> if the command was refused before a
/// process was started.
/// </param>
/// <param name="reason">
/// A sentence that describes how the command failed, including the text that <c>copilot</c> printed last, if any.
/// </param>
/// <param name="copilotMessage">
/// The last non-empty line that <c>copilot</c> printed on standard error, or on standard output if standard error is
/// empty, or <see langword="null" /> if it printed none or the failure is not a non-zero exit code.
/// </param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public sealed class CopilotCommandFailedException(
    IReadOnlyList<string> arguments,
    int? exitCode,
    string reason,
    string? copilotMessage = null,
    Exception? innerException = null)
    : ExternalToolException("copilot", "failed", arguments, reason, innerException)
{
    /// <summary>
    /// Gets the exit code of the <c>copilot</c> process.
    /// </summary>
    /// <value>
    /// The exit code; <c>0</c> if the process succeeded but its output cannot be read or does not show the expected
    /// state; <see langword="null" /> if the command was refused before a process was started.
    /// </value>
    public int? ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets the text that <c>copilot</c> printed last before it exited with a non-zero exit code.
    /// </summary>
    /// <value>
    /// The last non-empty line of standard error, or of standard output if standard error is empty, without leading
    /// and trailing whitespace, or <see langword="null" /> if there is no such line or the failure is of another kind.
    /// </value>
    public string? CopilotMessage { get; } = copilotMessage;
}
