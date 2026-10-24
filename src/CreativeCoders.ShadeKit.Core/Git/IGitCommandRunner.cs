using CreativeCoders.ShadeKit.Core.Git.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Runs the <c>git</c> executable and captures its output.
/// </summary>
public interface IGitCommandRunner
{
    /// <summary>
    /// Runs <c>git</c> with the specified arguments in the specified directory and waits until it exits.
    /// </summary>
    /// <remarks>
    /// The arguments are passed to the process one by one, without a shell, so they are neither split nor expanded.
    /// A non-zero exit code is not an error of this method; it is returned in the result for the caller to interpret.
    /// </remarks>
    /// <param name="workingDirectory">The directory in which <c>git</c> runs.</param>
    /// <param name="arguments">The arguments that are passed to <c>git</c>, for example <c>config</c> and <c>user.name</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous run. The task result contains the exit code and the captured standard
    /// output and standard error of the process.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
    /// <exception cref="GitUnavailableException">The <c>git</c> executable cannot be found or started.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; the started process is killed.
    /// </exception>
    Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
