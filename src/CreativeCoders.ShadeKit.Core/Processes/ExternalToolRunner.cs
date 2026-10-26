using System.ComponentModel;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Processes.Exceptions;
using CreativeCoders.ShadeKit.Core.Text;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Runs a command of an external tool whose executable a locator resolves, and reports a tool that cannot be run with
/// the exceptions of the tool.
/// </summary>
internal static class ExternalToolRunner
{
    /// <summary>
    /// Locates the executable of the specified tool and runs it with the specified arguments in the specified
    /// directory.
    /// </summary>
    /// <remarks>
    /// The executable is located only after the arguments were checked and the token was not canceled. Only an
    /// absolute executable path is started; the process runs as described for <see cref="ExternalProcessRunner" />.
    /// </remarks>
    /// <param name="tool">The tool to run.</param>
    /// <param name="findExecutable">
    /// The function that returns the absolute path of the executable, or <see langword="null" /> if it is not found.
    /// </param>
    /// <param name="workingDirectory">The directory in which the tool runs.</param>
    /// <param name="arguments">The arguments that are passed to the tool.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous run. The task result contains the exit code and the captured standard
    /// output and standard error of the process.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="workingDirectory" /> is <see langword="null" />, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
    /// <exception cref="ExternalToolUnavailableException">
    /// The executable is not found, its path is not absolute, or it cannot be started; the exception is the one that
    /// <paramref name="tool" /> creates.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    public static async Task<ExternalProcessResult> RunAsync(ExternalTool tool, Func<string?> findExecutable,
        string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        Ensure.IsNotNullOrWhitespace(workingDirectory);
        Ensure.NotNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var executablePath = findExecutable();

        if (string.IsNullOrEmpty(executablePath))
        {
            throw tool.CreateNotFoundException(arguments,
                $"{tool.DisplayName} was not found in any absolute directory of the PATH environment variable.");
        }

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw tool.CreateUnavailableException(arguments,
                $"The {tool.Name} executable path '{executablePath}' is not absolute, so it is not started.", null);
        }

        try
        {
            return await ExternalProcessRunner
                .RunAsync(executablePath, workingDirectory, arguments, tool.EnvironmentVariables, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            throw tool.CreateUnavailableException(arguments,
                $"The {tool.Name} executable '{executablePath}' could not be started: " +
                $"{Sentences.Terminate(exception.Message)}", exception);
        }
    }
}
