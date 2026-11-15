using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when a command of the OpenAI Codex CLI <c>codex</c> fails: it exits with a
/// non-zero exit code, prints no JSON output that SHADE kit can read, or is refused before it starts.
/// </summary>
/// <remarks>
/// <para>
/// A <c>codex</c> that cannot be found or started is reported as a <see cref="CodexUnavailableException" /> instead. A
/// command is refused before it starts when <c>codex</c> is a Windows <c>.cmd</c> shim and its path or an argument
/// contains a character that <c>cmd.exe</c> would interpret, or when an argument would be misread by <c>codex</c>.
/// </para>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/main.rs</c> and
/// <c>codex-rs/arg0/src/lib.rs</c>): a failing <c>codex plugin</c> command prints no JSON, also with <c>--json</c>;
/// its error leaves <c>main</c> as an error value, which the Rust runtime writes to standard error and ends the
/// process with a non-zero exit code. Unverified: the exit code itself (expected <c>1</c>), because <c>codex</c> has
/// not been run.
/// </para>
/// </remarks>
/// <param name="arguments">The arguments of the <c>codex</c> command that failed.</param>
/// <param name="exitCode">
/// The exit code of the <c>codex</c> process, or <see langword="null" /> if the command was refused before a process
/// was started.
/// </param>
/// <param name="reason">
/// A sentence that describes how the command failed, including the text that <c>codex</c> printed last, if any.
/// </param>
/// <param name="codexMessage">
/// The last non-empty line that <c>codex</c> printed on standard error, or on standard output if standard error is
/// empty, or <see langword="null" /> if it printed none or the failure is not a non-zero exit code.
/// </param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public sealed class CodexCommandFailedException(
    IReadOnlyList<string> arguments,
    int? exitCode,
    string reason,
    string? codexMessage = null,
    Exception? innerException = null)
    : ExternalToolUnavailableException("codex", "failed", arguments, reason, innerException)
{
    /// <summary>
    /// Gets the exit code of the <c>codex</c> process.
    /// </summary>
    /// <value>
    /// The exit code; <c>0</c> if the process succeeded but its output cannot be read or does not show the expected
    /// state; <see langword="null" /> if the command was refused before a process was started.
    /// </value>
    public int? ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets the text that <c>codex</c> printed last before it exited with a non-zero exit code.
    /// </summary>
    /// <value>
    /// The last non-empty line of standard error, or of standard output if standard error is empty, without leading
    /// and trailing whitespace, or <see langword="null" /> if there is no such line or the failure is of another kind.
    /// </value>
    public string? CodexMessage { get; } = codexMessage;
}
