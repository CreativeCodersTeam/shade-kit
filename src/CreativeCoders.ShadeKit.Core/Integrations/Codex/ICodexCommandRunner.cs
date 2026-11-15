using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Runs the OpenAI Codex CLI executable <c>codex</c> and captures its output.
/// </summary>
public interface ICodexCommandRunner
{
    /// <summary>
    /// Runs <c>codex</c> with the specified arguments in the specified directory and waits until it exits.
    /// </summary>
    /// <remarks>
    /// The arguments are passed to the process one by one, without a shell, so they are neither split nor expanded;
    /// the only exception is a Windows <c>codex.cmd</c> shim, which runs through <c>cmd.exe</c> and is refused every
    /// argument that <c>cmd.exe</c> would interpret. <c>codex</c> runs with <c>NO_COLOR=1</c>. A non-zero exit code is
    /// not an error of this method; it is returned in the result for the caller to interpret.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which <c>codex</c> runs.</param>
    /// <param name="arguments">
    /// The arguments that are passed to <c>codex</c>, for example <c>plugin</c>, <c>list</c>, and <c>--json</c>.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous run. The task result contains the exit code and the captured standard
    /// output and standard error of the process.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
    /// <exception cref="CodexUnavailableException">
    /// The <c>codex</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CodexNotFoundException" />.
    /// </exception>
    /// <exception cref="CodexCommandFailedException">
    /// <c>codex</c> is a Windows <c>.cmd</c> shim, and its path or an argument contains a character that
    /// <c>cmd.exe</c> would interpret; nothing is started.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; the started process is killed.
    /// </exception>
    Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
