using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Runs the Claude Code CLI executable <c>claude</c> and captures its output.
/// </summary>
public interface IClaudeCommandRunner
{
    /// <summary>
    /// Runs <c>claude</c> with the specified arguments in the specified directory and waits until it exits.
    /// </summary>
    /// <remarks>
    /// The arguments are passed to the process one by one, without a shell, so they are neither split nor expanded.
    /// <c>claude</c> runs without colors. A non-zero exit code is not an error of this method; it is returned in the
    /// result for the caller to interpret.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which <c>claude</c> runs.</param>
    /// <param name="arguments">
    /// The arguments that are passed to <c>claude</c>, for example <c>plugin</c>, <c>list</c>, and <c>--json</c>.
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
    /// <exception cref="ClaudeUnavailableException">
    /// The <c>claude</c> executable cannot be found or started; a missing executable is a
    /// <see cref="ClaudeNotFoundException" />.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; the started process is killed.
    /// </exception>
    Task<ExternalProcessResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
