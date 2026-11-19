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
    /// <summary>
    /// Gets the reader for the fields of the output, whose failures are reported as
    /// <see cref="CopilotCommandFailedException" />.
    /// </summary>
    /// <value>The shared field reader for the tool name <c>copilot</c>.</value>
    public AgentJsonFieldReader<CopilotCommandFailedException> Fields { get; } = new("copilot", result,
        (reason, copilotMessage, innerException) => new CopilotCommandFailedException(arguments, result.ExitCode,
            reason, copilotMessage, innerException));

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
            throw Fields.CreateExitCodeException();
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
            throw Fields.Fail($"The output of copilot is not valid JSON: {Sentences.Terminate(exception.Message)}",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw Fields.Fail("The output of copilot is not a JSON array.");
            }

            return
            [
                .. root.EnumerateArray().Select((entry, index) =>
                    readEntry(entry, $"entry {(index + 1).ToString(CultureInfo.InvariantCulture)}"))
            ];
        }
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
            _ => throw Fields.Fail(
                $"The field '{propertyName}' of the {Fields.Describe(subject)} is neither a string nor a JSON object.")
        };
    }
}
