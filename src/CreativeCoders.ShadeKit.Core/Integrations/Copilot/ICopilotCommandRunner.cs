using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Runs the GitHub Copilot CLI executable <c>copilot</c> and captures its output.
/// </summary>
public interface ICopilotCommandRunner
{
    /// <summary>
    /// Runs <c>copilot</c> with the specified arguments in the specified directory and waits until it exits.
    /// </summary>
    /// <remarks>
    /// The arguments are passed to the process one by one, without a shell, so they are neither split nor expanded;
    /// the only exception is a Windows <c>copilot.cmd</c> shim, which runs through <c>cmd.exe</c> and is refused every
    /// argument that <c>cmd.exe</c> would interpret. <c>copilot</c> runs with <c>NO_COLOR=1</c>. A non-zero exit code
    /// is not an error of this method; it is returned in the result for the caller to interpret.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which <c>copilot</c> runs.</param>
    /// <param name="arguments">
    /// The arguments that are passed to <c>copilot</c>, for example <c>plugin</c>, <c>list</c>, and <c>--json</c>.
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
    /// <exception cref="CopilotUnavailableException">
    /// The <c>copilot</c> executable cannot be found or started; a missing executable is a
    /// <see cref="CopilotNotFoundException" />.
    /// </exception>
    /// <exception cref="CopilotCommandFailedException">
    /// <c>copilot</c> is a Windows <c>.cmd</c> shim, and its path or an argument contains a character that
    /// <c>cmd.exe</c> would interpret; nothing is started.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; the started process is killed.
    /// </exception>
    Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
