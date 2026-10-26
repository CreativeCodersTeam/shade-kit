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
internal sealed record ExternalTool(
    string Name,
    string DisplayName,
    IReadOnlyDictionary<string, string>? EnvironmentVariables,
    Func<IReadOnlyList<string>, string, ExternalToolUnavailableException> CreateNotFoundException,
    Func<IReadOnlyList<string>, string, Exception?, ExternalToolUnavailableException> CreateUnavailableException);
