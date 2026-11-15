using System.Globalization;
using System.Text.Json;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Text;

namespace CreativeCoders.ShadeKit.Core.Integrations.Copilot;

/// <summary>
/// Reads the output of a finished <c>copilot</c> command and reports every failure of the command as a
/// <see cref="CopilotCommandFailedException" />.
/// </summary>
/// <remarks>
/// <para>
/// A command that changes the configuration prints no JSON; it succeeded if it exited with the exit code <c>0</c>. A
/// list command with <c>--json</c> prints one JSON array, and the whole standard output must be that array, so a
/// stray line before or after it is a failure. Unknown fields are ignored.
/// </para>
/// <para>
/// Unverified: that the change commands have no <c>--json</c> and report a failure only through the exit code and
/// standard error rests on the GitHub Docs plugin reference and the changelog of the Copilot CLI (1.0.85 added
/// <c>--json</c> to the list commands only).
/// </para>
/// </remarks>
/// <param name="arguments">The arguments of the <c>copilot</c> command that produced the output.</param>
/// <param name="result">The result of the finished <c>copilot</c> process.</param>
internal sealed class CopilotJsonOutputReader(IReadOnlyList<string> arguments, ExternalProcessResult result)
{
    private static readonly char[] LineSeparators = ['\r', '\n'];

    /// <summary>
    /// Ensures that a command that changes the configuration succeeded.
    /// </summary>
    /// <exception cref="CopilotCommandFailedException">
    /// <c>copilot</c> exited with a non-zero exit code; the exception names the last non-empty line of standard
    /// error, or of standard output if standard error has none. Lines end at CRLF, LF, or a lone CR.
    /// </exception>
    public void EnsureSuccess()
    {
        if (result.ExitCode != 0)
        {
            throw CreateExitCodeException();
        }
    }

    /// <summary>
    /// Reads the JSON array that a list command prints and converts each of its entries.
    /// </summary>
    /// <typeparam name="T">The type of the converted entries.</typeparam>
    /// <param name="readEntry">
    /// The function that converts an entry; it receives the entry and its name in messages, for example
    /// <c>entry 2</c>.
    /// </param>
    /// <returns>The converted entries in the order of the array.</returns>
    /// <exception cref="CopilotCommandFailedException">
    /// <c>copilot</c> exited with a non-zero exit code, or its whole standard output is not one JSON array.
    /// </exception>
    public IReadOnlyList<T> ReadArray<T>(Func<JsonElement, string, T> readEntry)
    {
        EnsureSuccess();

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException exception)
        {
            throw Fail($"The output of copilot is not valid JSON: {Sentences.Terminate(exception.Message)}",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw Fail("The output of copilot is not a JSON array.");
            }

            return
            [
                .. root.EnumerateArray().Select((entry, index) =>
                    readEntry(entry, $"entry {(index + 1).ToString(CultureInfo.InvariantCulture)}"))
            ];
        }
    }

    /// <summary>
    /// Ensures that the specified entry of a list is a JSON object.
    /// </summary>
    /// <param name="entry">The entry to check.</param>
    /// <param name="entryName">The name of the entry in messages, for example <c>entry 2</c>.</param>
    /// <exception cref="CopilotCommandFailedException"><paramref name="entry" /> is not a JSON object.</exception>
    public void EnsureObject(JsonElement entry, string entryName)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            throw Fail($"The {entryName} of the copilot output is not a JSON object.");
        }
    }

    /// <summary>
    /// Reads a string field that must be present.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">The name of the object in messages, for example <c>entry 2</c>.</param>
    /// <returns>The value of the field, which is neither empty nor whitespace.</returns>
    /// <exception cref="CopilotCommandFailedException">
    /// The field is missing, <see langword="null" />, empty, whitespace, or not a string.
    /// </exception>
    public string ReadRequiredString(JsonElement element, string propertyName, string subject)
    {
        var value = ReadOptionalString(element, propertyName, subject);

        return string.IsNullOrWhiteSpace(value)
            ? throw Fail($"The {subject} of the copilot output has no non-empty '{propertyName}' string.")
            : value;
    }

    /// <summary>
    /// Reads a string field that may be missing.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">The name of the object in messages, for example <c>entry 2</c>.</param>
    /// <returns>
    /// The value of the field, or <see langword="null" /> if the field is missing or <see langword="null" />.
    /// </returns>
    /// <exception cref="CopilotCommandFailedException">The field has a value that is not a string.</exception>
    public string? ReadOptionalString(JsonElement element, string propertyName, string subject)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw Fail($"The field '{propertyName}' of the {subject} of the copilot output is not a string.");
    }

    /// <summary>
    /// Reads a field that may be missing, a string, or a JSON object, and returns it if it is a JSON object.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">The name of the object in messages, for example <c>entry 2</c>.</param>
    /// <returns>
    /// The value of the field if it is a JSON object, or <see langword="null" /> if the field is missing,
    /// <see langword="null" />, or a string.
    /// </returns>
    /// <exception cref="CopilotCommandFailedException">
    /// The field has a value that is neither a string nor a JSON object.
    /// </exception>
    public JsonElement? ReadOptionalObjectOrString(JsonElement element, string propertyName, string subject)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => value,
            JsonValueKind.Null or JsonValueKind.String => null,
            _ => throw Fail(
                $"The field '{propertyName}' of the {subject} of the copilot output is neither a string nor a JSON " +
                "object.")
        };
    }

    /// <summary>
    /// Reads a Boolean field that must be present.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">The name of the object in messages, for example <c>entry 2</c>.</param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="CopilotCommandFailedException">
    /// The field is missing, <see langword="null" />, or not a Boolean.
    /// </exception>
    public bool ReadRequiredBoolean(JsonElement element, string propertyName, string subject)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean();
        }

        throw Fail($"The {subject} of the copilot output has no '{propertyName}' Boolean.");
    }

    private CopilotCommandFailedException CreateExitCodeException()
    {
        var exitCodeText = "copilot exited with the exit code " +
                           result.ExitCode.ToString(CultureInfo.InvariantCulture);

        var message = FindLastNonEmptyLine(result.StandardError) ?? FindLastNonEmptyLine(result.StandardOutput);

        var reason = message is null ? $"{exitCodeText}." : $"{exitCodeText}: {Sentences.Terminate(message)}";

        return new CopilotCommandFailedException(arguments, result.ExitCode, reason, message);
    }

    // copilot writes its error last, so the last line names the cause; earlier lines may be progress or warnings.
    // Lines end at CRLF, LF, or a lone CR, so progress text that a CR overwrites is not part of the message.
    private static string? FindLastNonEmptyLine(string text)
    {
        return text.Split(LineSeparators)
            .Select(x => x.Trim())
            .LastOrDefault(x => x.Length > 0);
    }

    private CopilotCommandFailedException Fail(string reason, Exception? innerException = null)
    {
        return new CopilotCommandFailedException(arguments, result.ExitCode, reason, innerException: innerException);
    }
}
