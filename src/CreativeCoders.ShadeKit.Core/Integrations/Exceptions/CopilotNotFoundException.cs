namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when no GitHub Copilot CLI <c>copilot</c> executable is found, so a
/// <c>copilot</c> command cannot be run.
/// </summary>
/// <param name="arguments">The arguments of the <c>copilot</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes where <c>copilot</c> was looked for.</param>
public sealed class CopilotNotFoundException(IReadOnlyList<string> arguments, string reason)
    : CopilotUnavailableException(arguments, reason);
