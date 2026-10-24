namespace CreativeCoders.ShadeKit.Core.Git.Exceptions;

/// <summary>
/// Represents the exception that is thrown when no <c>git</c> executable is found, so a <c>git</c> command cannot be
/// run.
/// </summary>
/// <remarks>
/// Callers for which <c>git</c> is optional tell a missing <c>git</c> from a <c>git</c> that fails by this type.
/// </remarks>
/// <param name="arguments">The arguments of the <c>git</c> command that could not be run.</param>
/// <param name="reason">A sentence that describes where <c>git</c> was looked for.</param>
public sealed class GitNotFoundException(IReadOnlyList<string> arguments, string reason)
    : GitUnavailableException(arguments, reason);
