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
    public static Task<ExternalProcessResult> RunAsync(ExternalTool tool, Func<string?> findExecutable,
        string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return RunAsync(tool, findExecutable, GetComSpec, workingDirectory, arguments, cancellationToken);
    }

    /// <summary>
    /// Locates the executable of the specified tool and runs it with the specified arguments in the specified
    /// directory, reading the <c>ComSpec</c> value for a <c>.cmd</c> shim from the specified function.
    /// </summary>
    /// <remarks>
    /// Behaves like the overload without <paramref name="getComSpec" />; the function is called only for a tool that
    /// runs <c>.cmd</c> shims and a found executable that is one.
    /// </remarks>
    /// <param name="tool">The tool to run.</param>
    /// <param name="findExecutable">
    /// The function that returns the absolute path of the executable, or <see langword="null" /> if it is not found.
    /// </param>
    /// <param name="getComSpec">
    /// The function that returns the value of the <c>ComSpec</c> environment variable, or <see langword="null" /> if
    /// it is not set.
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
    /// The executable is not found, its path is not absolute, it cannot be started, the <c>ComSpec</c> value of a
    /// <c>.cmd</c> shim is unusable, or the shim is refused an argument; the exception is the one that
    /// <paramref name="tool" /> creates.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken" /> was canceled; a started process is killed.
    /// </exception>
    internal static async Task<ExternalProcessResult> RunAsync(ExternalTool tool, Func<string?> findExecutable,
        Func<string?> getComSpec, string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        Ensure.IsNotNullOrWhitespace(workingDirectory);
        Ensure.NotNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var executablePath = findExecutable();

        if (string.IsNullOrEmpty(executablePath))
        {
            throw tool.CreateNotFoundException(arguments, tool.NotFoundReason);
        }

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw tool.CreateUnavailableException(arguments,
                $"The {tool.Name} executable path '{executablePath}' is not absolute, so it is not started.", null);
        }

        var start = CreateProcessStart(tool, executablePath, arguments, getComSpec);

        // The ComSpec value of a .cmd shim is checked with the Windows rules; on another host it is not absolute.
        if (!Path.IsPathFullyQualified(start.ExecutablePath))
        {
            throw tool.CreateUnavailableException(arguments,
                $"The command interpreter path '{start.ExecutablePath}' of the {tool.Name} .cmd shim " +
                $"'{executablePath}' is not absolute on this operating system, so it is not started.", null);
        }

        try
        {
            return await ExternalProcessRunner
                .RunAsync(start, workingDirectory, tool.EnvironmentVariables, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            var startedThrough = start.ExecutablePath == executablePath
                ? string.Empty
                : $" through '{start.ExecutablePath}'";

            throw tool.CreateUnavailableException(arguments,
                $"The {tool.Name} executable '{executablePath}' could not be started{startedThrough}: " +
                $"{Sentences.Terminate(exception.Message)}", exception);
        }
    }

    /// <summary>
    /// Decides how the found executable of the specified tool is started with the specified arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An executable is started directly with the arguments one by one, without a shell, unless the tool runs
    /// <c>.cmd</c> shims (<see cref="ExternalTool.CreateCommandFailedException" /> is set) and the executable ends
    /// with <c>.cmd</c>. Such a shim is started as <c>%ComSpec% /d /s /c ""&lt;shim&gt;" &lt;arguments&gt;"</c>, as
    /// built by <see cref="CmdShimCommandLine" />.
    /// </para>
    /// <para>
    /// Why: npm installs the GitHub Copilot CLI and the OpenAI Codex CLI on Windows as <c>.cmd</c> shims, which only
    /// <c>cmd.exe</c> can run (user decision of 2026-11-15 against the "no shell" rule). Because <c>cmd.exe</c> can
    /// expand or interpret arguments (BatBadBut, CVE-2024-24576), arguments with its special characters are refused,
    /// not escaped. The <c>ComSpec</c> value is checked first, then the shim path and the arguments; nothing is
    /// started when either check fails. Unverified: this has not been run on Windows.
    /// </para>
    /// </remarks>
    /// <param name="tool">The tool whose executable was found.</param>
    /// <param name="executablePath">The absolute path of the found executable.</param>
    /// <param name="arguments">The arguments of the tool command.</param>
    /// <param name="getComSpec">
    /// The function that returns the value of the <c>ComSpec</c> environment variable, or <see langword="null" /> if
    /// it is not set.
    /// </param>
    /// <returns>The start of the executable, or of <c>cmd.exe</c> for a <c>.cmd</c> shim.</returns>
    /// <exception cref="ExternalToolUnavailableException">
    /// The <c>ComSpec</c> value of a <c>.cmd</c> shim is not set, not fully qualified, or does not name
    /// <c>cmd.exe</c> (the exception that <see cref="ExternalTool.CreateUnavailableException" /> creates), or the shim
    /// path or an argument contains a refused character (the exception that
    /// <see cref="ExternalTool.CreateCommandFailedException" /> creates).
    /// </exception>
    internal static ExternalProcessStart CreateProcessStart(ExternalTool tool, string executablePath,
        IReadOnlyList<string> arguments, Func<string?> getComSpec)
    {
        Ensure.NotNull(tool);
        Ensure.NotNull(executablePath);
        Ensure.NotNull(arguments);
        Ensure.NotNull(getComSpec);

        if (tool.CreateCommandFailedException is not { } createCommandFailedException
            || !CmdShimCommandLine.IsCmdShim(executablePath))
        {
            return ExternalProcessStart.WithArguments(executablePath, arguments);
        }

        // Deliberate exception to the "no shell" rule (user decision 2026-11-15): npm installs Copilot and Codex on
        // Windows as .cmd shims, which only cmd.exe runs. cmd.exe expands and interprets arguments (BatBadBut,
        // CVE-2024-24576), so dangerous characters are refused below instead of escaped. Unverified on Windows.
        var comSpec = getComSpec();

        if (!CmdShimCommandLine.IsValidComSpec(comSpec, executablePath, out var comSpecProblem))
        {
            throw tool.CreateUnavailableException(arguments, comSpecProblem, null);
        }

        var refusal = CmdShimCommandLine.FindRefusal(executablePath, arguments);

        if (refusal is not null)
        {
            throw createCommandFailedException(arguments, refusal);
        }

        return ExternalProcessStart.WithVerbatimArguments(comSpec,
            CmdShimCommandLine.Create(executablePath, arguments));
    }

    private static string? GetComSpec()
    {
        return Environment.GetEnvironmentVariable(CmdShimCommandLine.ComSpecVariableName);
    }
}
