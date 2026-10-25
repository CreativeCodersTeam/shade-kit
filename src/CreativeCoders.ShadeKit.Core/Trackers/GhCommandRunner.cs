using System.ComponentModel;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Trackers;

/// <summary>
/// Runs the GitHub CLI executable <c>gh</c> as a child process and captures its output.
/// </summary>
/// <remarks>
/// Starts only the absolute path from the <see cref="IGhExecutableLocator" />, never a <c>gh</c> in the current
/// directory, and runs it without a shell; the output is read concurrently and decoded as UTF-8. The process inherits
/// the environment of the current process plus <see cref="EnvironmentVariables" />, so <c>gh</c> never waits for input,
/// checks for updates, or writes color codes.
/// </remarks>
/// <param name="ghExecutableLocator">The locator that resolves the absolute path of the <c>gh</c> executable.</param>
public sealed class GhCommandRunner(IGhExecutableLocator ghExecutableLocator) : IGhCommandRunner
{
    private readonly IGhExecutableLocator _ghExecutableLocator = Ensure.NotNull(ghExecutableLocator);

    /// <summary>
    /// Gets the environment variables that are set for every <c>gh</c> process in addition to the inherited ones.
    /// </summary>
    /// <value>
    /// <c>GH_PROMPT_DISABLED=1</c>, <c>GH_NO_UPDATE_NOTIFIER=1</c>, and <c>NO_COLOR=1</c>.
    /// </value>
    internal static IReadOnlyDictionary<string, string> EnvironmentVariables { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GH_PROMPT_DISABLED"] = "1",
            ["GH_NO_UPDATE_NOTIFIER"] = "1",
            ["NO_COLOR"] = "1"
        };

    /// <inheritdoc />
    public async Task<GhCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        Ensure.IsNotNullOrWhitespace(workingDirectory);
        Ensure.NotNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var ghExecutablePath = _ghExecutableLocator.FindGhExecutable();

        if (string.IsNullOrEmpty(ghExecutablePath))
        {
            throw new GhNotFoundException(arguments,
                "The GitHub CLI gh was not found in any absolute directory of the PATH environment variable.");
        }

        if (!Path.IsPathFullyQualified(ghExecutablePath))
        {
            throw new GhNotFoundException(arguments,
                $"The gh executable path '{ghExecutablePath}' is not absolute, so it is not started.");
        }

        ExternalProcessResult result;

        try
        {
            result = await ExternalProcessRunner
                .RunAsync(ghExecutablePath, workingDirectory, arguments, EnvironmentVariables, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Win32Exception exception)
        {
            throw new GhNotFoundException(arguments,
                $"The gh executable '{ghExecutablePath}' could not be started: {CreateSentence(exception.Message)}",
                exception);
        }

        return new GhCommandResult(result.ExitCode, result.StandardOutput, result.StandardError);
    }

    private static string CreateSentence(string text)
    {
        var trimmedText = text.Trim();

        return trimmedText.EndsWith('.') ? trimmedText : trimmedText + ".";
    }
}
