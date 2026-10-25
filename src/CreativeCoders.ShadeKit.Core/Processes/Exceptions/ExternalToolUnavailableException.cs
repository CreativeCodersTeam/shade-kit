using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Processes.Exceptions;

/// <summary>
/// Represents the base class of the exceptions that are thrown when a command of an external tool, such as
/// <c>git</c> or <c>gh</c>, cannot be run or fails in a way that SHADE kit does not expect.
/// </summary>
public abstract class ExternalToolUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalToolUnavailableException" /> class with the message
    /// <c>The &lt;tool&gt; command '&lt;command text&gt;' &lt;failure&gt;: &lt;reason&gt;</c>.
    /// </summary>
    /// <param name="toolName">The name of the tool, for example <c>git</c>.</param>
    /// <param name="failure">The words that describe the failure in the message, for example <c>failed</c>.</param>
    /// <param name="arguments">The arguments of the command that could not be run or failed.</param>
    /// <param name="reason">A sentence that describes why the command could not be run or how it failed.</param>
    /// <param name="innerException">
    /// The exception that caused this exception, or <see langword="null" /> if there is none.
    /// </param>
    protected ExternalToolUnavailableException(string toolName, string failure, IReadOnlyList<string> arguments,
        string reason, Exception? innerException)
        : base($"The {toolName} command '{FormatCommandText(toolName, arguments)}' {failure}: {reason}",
            innerException)
    {
        Arguments = [.. arguments];
        CommandText = FormatCommandText(toolName, arguments);
        Reason = reason;
    }

    /// <summary>
    /// Gets the arguments of the command that could not be run or failed.
    /// </summary>
    /// <value>A copy of the arguments as they were passed to the tool, without the executable name.</value>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the command line of the command that could not be run or failed.
    /// </summary>
    /// <value>
    /// The tool name followed by the arguments, separated by spaces, for example
    /// <c>git symbolic-ref --short -q HEAD</c>. An argument that is empty or contains whitespace is enclosed in double
    /// quotation marks.
    /// </value>
    public string CommandText { get; }

    /// <summary>
    /// Gets the reason why the command could not be run or how it failed.
    /// </summary>
    /// <value>
    /// A sentence that describes the problem, for example that the tool was not found, could not be started, or
    /// exited with an unexpected exit code and the text it wrote to standard error.
    /// </value>
    public string Reason { get; }

    private static string FormatCommandText(string toolName, IReadOnlyList<string> arguments)
    {
        Ensure.NotNull(arguments);

        return string.Join(' ', [toolName, .. arguments.Select(FormatArgument)]);
    }

    private static string FormatArgument(string argument)
    {
        return argument.Length == 0 || argument.Any(char.IsWhiteSpace)
            ? $"\"{argument}\""
            : argument;
    }
}
