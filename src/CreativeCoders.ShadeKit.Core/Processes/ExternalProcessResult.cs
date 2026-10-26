using System.Globalization;
using CreativeCoders.ShadeKit.Core.Text;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Represents the outcome of a finished external process, such as <c>git</c> or <c>gh</c>.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="StandardOutput">
/// The complete text that the process wrote to its standard output, decoded as UTF-8.
/// </param>
/// <param name="StandardError">
/// The complete text that the process wrote to its standard error, decoded as UTF-8.
/// </param>
public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Creates a sentence for a process that exited with an exit code its caller does not expect.
    /// </summary>
    /// <param name="toolName">The name of the tool that the process ran, for example <c>git</c>.</param>
    /// <returns>
    /// A sentence with the tool name and the exit code, followed by the trimmed standard error of the process if it
    /// wrote any, for example <c>git exited with the unexpected exit code 128: fatal: not a git repository.</c>
    /// </returns>
    public string CreateUnexpectedExitCodeReason(string toolName)
    {
        var exitCode = ExitCode.ToString(CultureInfo.InvariantCulture);
        var standardError = StandardError.Trim();

        return standardError.Length == 0
            ? $"{toolName} exited with the unexpected exit code {exitCode}."
            : $"{toolName} exited with the unexpected exit code {exitCode}: {Sentences.Terminate(standardError)}";
    }
}
