namespace CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

/// <summary>
/// Represents the exception that is thrown when no GitHub CLI <c>gh</c> executable is found, so a <c>gh</c> command
/// cannot be run.
/// </summary>
/// <param name="arguments">The arguments of the <c>gh</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes where <c>gh</c> was looked for.</param>
public sealed class GhNotFoundException(IReadOnlyList<string> arguments, string reason)
    : GhUnavailableException(arguments, reason);
