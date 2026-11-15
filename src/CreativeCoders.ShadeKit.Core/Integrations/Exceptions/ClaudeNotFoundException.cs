namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when no Claude Code CLI <c>claude</c> executable is found, so a
/// <c>claude</c> command cannot be run.
/// </summary>
/// <param name="arguments">The arguments of the <c>claude</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes where <c>claude</c> was looked for.</param>
public sealed class ClaudeNotFoundException(IReadOnlyList<string> arguments, string reason)
    : ClaudeUnavailableException(arguments, reason);
