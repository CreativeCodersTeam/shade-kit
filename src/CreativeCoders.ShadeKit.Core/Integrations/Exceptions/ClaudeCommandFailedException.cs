using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a command of the Claude Code CLI <c>claude</c> runs but fails: it
/// exits with a non-zero exit code, reports an outcome other than <c>ok</c>, or prints no JSON output that SHADE kit
/// can read.
/// </summary>
/// <remarks>
/// A <c>claude</c> that cannot be found or started is reported as a <see cref="ClaudeUnavailableException" />
/// instead.
/// </remarks>
/// <param name="arguments">The arguments of the <c>claude</c> command that failed.</param>
/// <param name="exitCode">The exit code of the <c>claude</c> process.</param>
/// <param name="reason">
/// A sentence that describes how the command failed, including the message and the failure code that <c>claude</c>
/// reported, if any.
/// </param>
/// <param name="claudeMessage">
/// The <c>message</c> that <c>claude</c> reported in its JSON output, or <see langword="null" /> if it reported none.
/// </param>
/// <param name="failureCode">
/// The <c>failureCode</c> that <c>claude</c> reported in its JSON output, for example <c>not_found</c>, or
/// <see langword="null" /> if it reported none.
/// </param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public sealed class ClaudeCommandFailedException(
    IReadOnlyList<string> arguments,
    int exitCode,
    string reason,
    string? claudeMessage = null,
    string? failureCode = null,
    Exception? innerException = null)
    : ExternalToolException("claude", "failed", arguments, reason, innerException)
{
    /// <summary>
    /// Gets the exit code of the <c>claude</c> process.
    /// </summary>
    /// <value>
    /// The exit code; <c>0</c> if the process succeeded but its output reports a failure or cannot be read.
    /// </value>
    public int ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets the message that <c>claude</c> reported in its JSON output.
    /// </summary>
    /// <value>
    /// The value of the <c>message</c> field, for example <c>Plugin "x" not found</c>, or <see langword="null" /> if
    /// the output has no such string.
    /// </value>
    public string? ClaudeMessage { get; } = claudeMessage;

    /// <summary>
    /// Gets the failure code that <c>claude</c> reported in its JSON output.
    /// </summary>
    /// <value>
    /// The value of the <c>failureCode</c> field, for example <c>not_found</c>, or <see langword="null" /> if the
    /// output has no such string.
    /// </value>
    public string? FailureCode { get; } = failureCode;
}
