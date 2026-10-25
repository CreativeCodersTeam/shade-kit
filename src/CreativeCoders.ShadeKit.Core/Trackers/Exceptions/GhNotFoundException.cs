using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Trackers.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the GitHub CLI <c>gh</c> is not found or cannot be started, so a
/// <c>gh</c> command cannot be run.
/// </summary>
/// <remarks>
/// Callers tell a missing or unusable <c>gh</c> from a <c>gh</c> that runs and fails by this type; a <c>gh</c> that
/// exits with an error is reported by the caller that interprets its exit code.
/// </remarks>
/// <param name="arguments">The arguments of the <c>gh</c> command that could not be run.</param>
/// <param name="reason">
/// A sentence that describes where <c>gh</c> was looked for or why it could not be started.
/// </param>
/// <param name="innerException">
/// The exception that caused this exception, or <see langword="null" /> if there is none.
/// </param>
public sealed class GhNotFoundException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : Exception($"The gh command '{FormatCommandText(arguments)}' cannot be run: {reason}", innerException)
{
    /// <summary>
    /// Gets the arguments of the <c>gh</c> command that could not be run.
    /// </summary>
    /// <value>A copy of the arguments as they were passed to <c>gh</c>, without the executable name.</value>
    public IReadOnlyList<string> Arguments { get; } = [.. arguments];

    /// <summary>
    /// Gets the command line of the <c>gh</c> command that could not be run.
    /// </summary>
    /// <value>
    /// The text <c>gh</c> followed by the arguments, separated by spaces, for example
    /// <c>gh issue view https://github.com/octo/repo/issues/1 --json title,body,comments</c>. An argument that is empty
    /// or contains whitespace is enclosed in double quotation marks.
    /// </value>
    public string CommandText { get; } = FormatCommandText(arguments);

    /// <summary>
    /// Gets the reason why the command could not be run.
    /// </summary>
    /// <value>
    /// A sentence that describes the problem, for example that <c>gh</c> was not found in any absolute directory of
    /// the <c>PATH</c> environment variable.
    /// </value>
    public string Reason { get; } = reason;

    private static string FormatCommandText(IReadOnlyList<string> arguments)
    {
        Ensure.NotNull(arguments);

        return string.Join(' ', ["gh", .. arguments.Select(FormatArgument)]);
    }

    private static string FormatArgument(string argument)
    {
        return argument.Length == 0 || argument.Any(char.IsWhiteSpace)
            ? $"\"{argument}\""
            : argument;
    }
}
