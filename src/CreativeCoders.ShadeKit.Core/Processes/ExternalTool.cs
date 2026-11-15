using CreativeCoders.ShadeKit.Core.Processes.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Processes;

/// <summary>
/// Describes an external tool that the <see cref="ExternalToolRunner" /> starts.
/// </summary>
/// <param name="Name">The name of the tool in messages, for example <c>gh</c>.</param>
/// <param name="DisplayName">
/// The name with which the reason of a missing executable starts, for example <c>The GitHub CLI gh</c>.
/// </param>
/// <param name="EnvironmentVariables">
/// The environment variables that are set for every process of the tool in addition to the inherited ones, or
/// <see langword="null" /> to pass the inherited environment unchanged.
/// </param>
/// <param name="CreateNotFoundException">
/// The function that creates the exception for a missing executable from the arguments and the reason.
/// </param>
/// <param name="CreateUnavailableException">
/// The function that creates the exception for an executable that cannot be started from the arguments, the reason,
/// and the exception that caused it.
/// </param>
/// <param name="CreateCommandFailedException">
/// The function that creates the tool's command-failed exception from the arguments and the reason when a Windows
/// <c>.cmd</c> shim of the tool is refused an argument, or <see langword="null" /> if the tool does not run
/// <c>.cmd</c> shims. Setting it opts the tool in to running a found <c>.cmd</c> file through <c>cmd.exe</c>, as
/// described for <see cref="CmdShimCommandLine" />; a tool without it starts every found executable directly.
/// </param>
/// <remarks>
/// The <c>.cmd</c> opt-in exists for the GitHub Copilot CLI and the OpenAI Codex CLI, which npm installs as
/// <c>.cmd</c> shims on Windows. The tools that only look for an <c>.exe</c>, such as <c>git</c>, <c>gh</c>, and
/// <c>claude</c>, do not set it.
/// </remarks>
internal sealed record ExternalTool(
    string Name,
    string DisplayName,
    IReadOnlyDictionary<string, string>? EnvironmentVariables,
    Func<IReadOnlyList<string>, string, ExternalToolUnavailableException> CreateNotFoundException,
    Func<IReadOnlyList<string>, string, Exception?, ExternalToolUnavailableException> CreateUnavailableException,
    Func<IReadOnlyList<string>, string, ExternalToolUnavailableException>? CreateCommandFailedException = null)
{
    /// <summary>
    /// Gets a value that indicates whether the tool runs a found Windows <c>.cmd</c> shim through <c>cmd.exe</c>.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if <see cref="CreateCommandFailedException" /> is set; otherwise,
    /// <see langword="false" />.
    /// </value>
    public bool RunsCmdShims => CreateCommandFailedException is not null;

    /// <summary>
    /// Gets the reason that is passed to <see cref="CreateNotFoundException" /> when the executable of the tool is not
    /// found.
    /// </summary>
    /// <value>
    /// The sentence that the executable was not found in any absolute directory of the <c>PATH</c> environment
    /// variable, starting with <see cref="DisplayName" />.
    /// </value>
    public string NotFoundReason =>
        $"{DisplayName} was not found in any absolute directory of the PATH environment variable.";
}
