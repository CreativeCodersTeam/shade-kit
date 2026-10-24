using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Git.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the <c>git</c> executable cannot be started or a <c>git</c> command
/// fails in a way that SHADE kit does not expect.
/// </summary>
/// <param name="arguments">The arguments of the <c>git</c> command that could not be run or failed.</param>
/// <param name="reason">A sentence that describes why the command could not be run or how it failed.</param>
/// <param name="innerException">The exception that caused this exception, or <see langword="null" /> if there is none.</param>
public class GitUnavailableException(
    IReadOnlyList<string> arguments,
    string reason,
    Exception? innerException = null)
    : Exception($"The git command '{FormatCommandText(arguments)}' failed: {reason}", innerException)
{
    /// <summary>
    /// Gets the arguments of the <c>git</c> command that could not be run or failed.
    /// </summary>
    /// <value>A copy of the arguments as they were passed to <c>git</c>, without the executable name.</value>
    public IReadOnlyList<string> Arguments { get; } = [.. arguments];

    /// <summary>
    /// Gets the command line of the <c>git</c> command that could not be run or failed.
    /// </summary>
    /// <value>
    /// The text <c>git</c> followed by the arguments, separated by spaces, for example
    /// <c>git symbolic-ref --short -q HEAD</c>. An argument that is empty or contains whitespace is enclosed in double
    /// quotation marks.
    /// </value>
    public string CommandText { get; } = FormatCommandText(arguments);

    /// <summary>
    /// Gets the reason why the command could not be run or how it failed.
    /// </summary>
    /// <value>
    /// A sentence that describes the problem, for example that <c>git</c> could not be started or exited with an
    /// unexpected exit code and the text it wrote to standard error.
    /// </value>
    public string Reason { get; } = reason;

    private static string FormatCommandText(IReadOnlyList<string> arguments)
    {
        Ensure.NotNull(arguments);

        return string.Join(' ', ["git", .. arguments.Select(FormatArgument)]);
    }

    private static string FormatArgument(string argument)
    {
        return argument.Length == 0 || argument.Any(char.IsWhiteSpace)
            ? $"\"{argument}\""
            : argument;
    }
}
