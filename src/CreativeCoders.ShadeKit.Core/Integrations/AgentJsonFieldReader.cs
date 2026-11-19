using System.Globalization;
using System.Text.Json;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Text;

namespace CreativeCoders.ShadeKit.Core.Integrations;

/// <summary>
/// Reads the fields of the JSON output of a finished agent command, such as <c>claude</c>, <c>copilot</c>, or
/// <c>codex</c>, and reports every unexpected shape and every non-zero exit code as the agent's own exception.
/// </summary>
/// <remarks>
/// <para>
/// Messages name the object that holds a field by its subject: <see langword="null" /> for the output object
/// itself, which is described as <c>&lt;tool&gt; output</c>, otherwise for example <c>entry 2</c>, which is
/// described as <c>entry 2 of the &lt;tool&gt; output</c> (see <see cref="Describe" />).
/// </para>
/// <para>
/// The agent readers keep the output shapes of their own agent, for example the result object of <c>claude</c>, and
/// use this reader for the field reads that every agent shares.
/// </para>
/// </remarks>
/// <typeparam name="TException">The type of the exception that reports a failure of the agent command.</typeparam>
/// <param name="toolName">The name of the agent command in messages, for example <c>copilot</c>.</param>
/// <param name="result">The result of the finished agent process.</param>
/// <param name="createException">
/// The function that creates the agent's exception; it receives the reason sentence, the text that the agent printed
/// last (only for a non-zero exit code, otherwise <see langword="null" />), and the exception that caused the failure,
/// or <see langword="null" /> if there is none.
/// </param>
internal sealed class AgentJsonFieldReader<TException>(
    string toolName,
    ExternalProcessResult result,
    Func<string, string?, Exception?, TException> createException)
    where TException : Exception
{
    // Every character of this string ends a line: CRLF, LF, or a lone CR.
    private const string LineSeparators = "\r\n";

    /// <summary>
    /// Gets the start of a sentence for the non-zero exit code of the agent process.
    /// </summary>
    /// <value>
    /// The tool name and the exit code, without a final full stop, for example
    /// <c>copilot exited with the exit code 1</c>.
    /// </value>
    public string ExitCodeText =>
        toolName + " exited with the exit code " + result.ExitCode.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Describes the object that holds a field, for use in messages.
    /// </summary>
    /// <param name="subject">
    /// The name of the object, for example <c>entry 2</c>, or <see langword="null" /> for the output object.
    /// </param>
    /// <returns>
    /// <c>&lt;tool&gt; output</c> if <paramref name="subject" /> is <see langword="null" />, otherwise
    /// <c>&lt;subject&gt; of the &lt;tool&gt; output</c>, for example <c>entry 2 of the copilot output</c>.
    /// </returns>
    public string Describe(string? subject)
    {
        return subject is null ? $"{toolName} output" : $"{subject} of the {toolName} output";
    }

    /// <summary>
    /// Creates the agent's exception for a failure that is not a non-zero exit code.
    /// </summary>
    /// <param name="reason">A sentence that describes how the command failed.</param>
    /// <param name="innerException">
    /// The exception that caused the failure, or <see langword="null" /> if there is none.
    /// </param>
    /// <returns>The exception that the failure factory creates, without the text that the agent printed last.</returns>
    public TException Fail(string reason, Exception? innerException = null)
    {
        return createException(reason, null, innerException);
    }

    /// <summary>
    /// Creates the agent's exception for the non-zero exit code of the agent process.
    /// </summary>
    /// <returns>
    /// The exception that the failure factory creates, with the reason <see cref="ExitCodeText" />, followed by the
    /// last non-empty line of standard error, or of standard output if standard error has none, and the same line
    /// as the text that the agent printed last; or <see langword="null" /> as that text if neither has such a line.
    /// </returns>
    /// <remarks>
    /// Lines end at CRLF, LF, or a lone CR and are compared without leading and trailing whitespace. The agent writes
    /// its error last, so the last line names the cause; earlier lines may be progress or warnings, and progress
    /// text that a CR overwrites is not part of the message.
    /// </remarks>
    public TException CreateExitCodeException()
    {
        var exitCodeText = ExitCodeText;

        var message = FindLastNonEmptyLine(result.StandardError) ?? FindLastNonEmptyLine(result.StandardOutput);

        var reason = message is null ? $"{exitCodeText}." : $"{exitCodeText}: {Sentences.Terminate(message)}";

        return createException(reason, message, null);
    }

    /// <summary>
    /// Ensures that the specified element is a JSON object.
    /// </summary>
    /// <param name="element">The element to check.</param>
    /// <param name="subject">
    /// The name of the element in messages, for example <c>entry 2</c>, or <see langword="null" /> for the output
    /// object.
    /// </param>
    /// <exception cref="Exception">
    /// The exception of type <typeparamref name="TException" />: <paramref name="element" /> is not a JSON object.
    /// </exception>
    public void EnsureObject(JsonElement element, string? subject)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Fail($"The {Describe(subject)} is not a JSON object.");
        }
    }

    /// <summary>
    /// Reads a string field that must be present.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">
    /// The name of the object in messages, for example <c>entry 2</c>, or <see langword="null" /> for the output
    /// object.
    /// </param>
    /// <returns>The value of the field, which is neither empty nor whitespace.</returns>
    /// <exception cref="Exception">
    /// The exception of type <typeparamref name="TException" />: the field is missing, <see langword="null" />,
    /// empty, whitespace, or not a string.
    /// </exception>
    public string ReadRequiredString(JsonElement element, string propertyName, string? subject)
    {
        var value = ReadOptionalString(element, propertyName, subject);

        return string.IsNullOrWhiteSpace(value)
            ? throw Fail($"The {Describe(subject)} has no non-empty '{propertyName}' string.")
            : value;
    }

    /// <summary>
    /// Reads a string field that may be missing.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">
    /// The name of the object in messages, for example <c>entry 2</c>, or <see langword="null" /> for the output
    /// object.
    /// </param>
    /// <returns>
    /// The value of the field, or <see langword="null" /> if the field is missing or <see langword="null" />.
    /// </returns>
    /// <exception cref="Exception">
    /// The exception of type <typeparamref name="TException" />: the field has a value that is not a string.
    /// </exception>
    public string? ReadOptionalString(JsonElement element, string propertyName, string? subject)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw Fail($"The field '{propertyName}' of the {Describe(subject)} is not a string.");
    }

    /// <summary>
    /// Reads a Boolean field that must be present.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">
    /// The name of the object in messages, for example <c>entry 2</c>, or <see langword="null" /> for the output
    /// object.
    /// </param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="Exception">
    /// The exception of type <typeparamref name="TException" />: the field is missing, <see langword="null" />, or
    /// not a Boolean.
    /// </exception>
    public bool ReadRequiredBoolean(JsonElement element, string propertyName, string? subject)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        throw Fail($"The {Describe(subject)} has no '{propertyName}' Boolean.");
    }

    private static string? FindLastNonEmptyLine(string text)
    {
        return text.Split(LineSeparators.AsSpan())
            .Select(x => x.Trim())
            .LastOrDefault(x => x.Length > 0);
    }
}
