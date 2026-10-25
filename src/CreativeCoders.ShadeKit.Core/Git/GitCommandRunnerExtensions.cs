using CreativeCoders.ShadeKit.Core.Git.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Git;

/// <summary>
/// Provides the <c>git</c> calls that several readers of git information share.
/// </summary>
internal static class GitCommandRunnerExtensions
{
    private const int SuccessExitCode = 0;

    private const int NotFoundExitCode = 1;

    /// <summary>
    /// Runs <c>git</c> for a value that may be missing, such as a configuration value or the current branch.
    /// </summary>
    /// <param name="gitCommandRunner">The runner that runs <c>git</c>.</param>
    /// <param name="workingDirectory">The directory in which <c>git</c> runs.</param>
    /// <param name="arguments">The arguments that are passed to <c>git</c>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous run. The task result contains the trimmed standard output if
    /// <c>git</c> exits with 0, or <see langword="null" /> if it exits with 1 or prints nothing.
    /// </returns>
    /// <exception cref="GitUnavailableException">
    /// The <c>git</c> executable cannot be found or started, or <c>git</c> exits with another exit code.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> was canceled.</exception>
    public static async Task<string?> RunForOptionalValueAsync(this IGitCommandRunner gitCommandRunner,
        string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var result = await gitCommandRunner.RunAsync(workingDirectory, arguments, cancellationToken)
            .ConfigureAwait(false);

        switch (result.ExitCode)
        {
            case SuccessExitCode:
                var value = result.StandardOutput.Trim();

                return value.Length == 0 ? null : value;

            case NotFoundExitCode:
                return null;

            default:
                throw new GitUnavailableException(arguments, result.CreateUnexpectedExitCodeReason("git"));
        }
    }
}
