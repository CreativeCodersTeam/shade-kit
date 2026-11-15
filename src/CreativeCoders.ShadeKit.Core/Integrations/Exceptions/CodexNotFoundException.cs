namespace CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

/// <summary>
/// Represents the exception that is thrown when no OpenAI Codex CLI <c>codex</c> executable is found, so a
/// <c>codex</c> command cannot be run.
/// </summary>
/// <param name="arguments">The arguments of the <c>codex</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes where <c>codex</c> was looked for.</param>
public sealed class CodexNotFoundException(IReadOnlyList<string> arguments, string reason)
    : CodexUnavailableException(arguments, reason);
