using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Runs the GitHub CLI executable <c>gh</c> and captures its output.
/// </summary>
public interface IGhCommandRunner
{
    /// <summary>
    /// Runs <c>gh</c> with the specified arguments in the specified directory and waits until it exits.
    /// </summary>
    /// <remarks>
    /// The arguments are passed to the process one by one, without a shell, so they are neither split nor expanded.
    /// <c>gh</c> runs without prompts, update notices, and colors. A non-zero exit code is not an error of this
    /// method; it is returned in the result for the caller to interpret.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which <c>gh</c> runs.</param>
    /// <param name="arguments">
    /// The arguments that are passed to <c>gh</c>, for example <c>issue</c> and <c>view</c>.
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
    /// <exception cref="GhNotFoundException">The <c>gh</c> executable cannot be found or started.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; the started process is killed.
    /// </exception>
    Task<GhCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
