using System.Globalization;
using System.Text.Json;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Text;

namespace CreativeCoders.ShadeKit.Core.Integrations.Codex;

/// <summary>
/// Reads the output of a finished <c>codex</c> command that ran with <c>--json</c> and reports every failure of the
/// command as a <see cref="CodexCommandFailedException" />.
/// </summary>
/// <remarks>
/// <para>
/// A successful command prints one JSON object, and the whole standard output must be that object, so a stray line
/// before or after it is a failure. Standard error is not read on success, because <c>codex</c> writes warnings there.
/// Unknown fields are ignored. Messages name a field of the output by its <c>subject</c>: <see langword="null" /> for
/// the output object itself, otherwise for example <c>entry 2 of 'installed'</c>.
/// </para>
/// <para>
/// FACT (source of <c>openai/codex</c> at tag <c>rust-v0.169.0</c>, <c>codex-rs/cli/src/plugin_cmd.rs</c> and
/// <c>codex-rs/cli/src/marketplace_cmd.rs</c>): every <c>codex plugin</c> command with <c>--json</c> prints its result
/// with one <c>println!</c> of <c>serde_json::to_string_pretty</c>, so the output is one JSON object followed by a
/// line break; a failure prints no JSON.
/// </para>
/// </remarks>
/// <param name="arguments">The arguments of the <c>codex</c> command that produced the output.</param>
/// <param name="result">The result of the finished <c>codex</c> process.</param>
internal sealed class CodexJsonOutputReader(IReadOnlyList<string> arguments, ExternalProcessResult result)
{
    private static readonly char[] LineSeparators = ['\r', '\n'];

    /// <summary>
    /// Reads the JSON object that the command printed and converts it.
    /// </summary>
    /// <typeparam name="T">The type of the converted output.</typeparam>
    /// <param name="readOutput">The function that converts the output object.</param>
    /// <returns>The converted output.</returns>
    /// <exception cref="CodexCommandFailedException">
    /// <c>codex</c> exited with a non-zero exit code; the exception names the last non-empty line of standard error,
    /// or of standard output if standard error has none, where lines end at CRLF, LF, or a lone CR. Or its whole
    /// standard output is not one JSON object, or <paramref name="readOutput" /> finds an unexpected shape.
    /// </exception>
    public T ReadObject<T>(Func<JsonElement, T> readOutput)
    {
        if (result.ExitCode != 0)
        {
            throw CreateExitCodeException();
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException exception)
        {
            throw Fail($"The output of codex is not valid JSON: {Sentences.Terminate(exception.Message)}",
                exception);
        }

        using (document)
        {
            var root = document.RootElement;

            return root.ValueKind == JsonValueKind.Object
                ? readOutput(root)
                : throw Fail("The output of codex is not a JSON object.");
        }
    }

    /// <summary>
    /// Ensures that the command succeeded and printed one JSON object; its fields are not read.
    /// </summary>
    /// <exception cref="CodexCommandFailedException">
    /// <c>codex</c> exited with a non-zero exit code, or its whole standard output is not one JSON object.
    /// </exception>
    public void EnsureObject()
    {
        ReadObject(static _ => true);
    }

    /// <summary>
    /// Reads an array field that must be present and converts each of its entries, which must be JSON objects.
    /// </summary>
    /// <typeparam name="T">The type of the converted entries.</typeparam>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="readEntry">
    /// The function that converts an entry; it receives the entry and its subject in messages, for example
    /// <c>entry 2 of 'installed'</c>.
    /// </param>
    /// <returns>The converted entries in the order of the array.</returns>
    /// <exception cref="CodexCommandFailedException">
    /// The field is missing or not an array, or an entry is not a JSON object.
    /// </exception>
    public IReadOnlyList<T> ReadRequiredObjectArray<T>(JsonElement element, string propertyName,
        Func<JsonElement, string, T> readEntry)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw Fail($"The codex output has no '{propertyName}' array.");
        }

        return
        [
            .. value.EnumerateArray().Select((entry, index) =>
            {
                var subject = $"entry {(index + 1).ToString(CultureInfo.InvariantCulture)} of '{propertyName}'";

                return entry.ValueKind == JsonValueKind.Object
                    ? readEntry(entry, subject)
                    : throw Fail($"The {subject} of the codex output is not a JSON object.");
            })
        ];
    }

    /// <summary>
    /// Reads a string field that must be present.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">
    /// The name of the object in messages, for example <c>entry 2 of 'installed'</c>, or <see langword="null" /> for
    /// the output object.
    /// </param>
    /// <returns>The value of the field, which is neither empty nor whitespace.</returns>
    /// <exception cref="CodexCommandFailedException">
    /// The field is missing, <see langword="null" />, empty, whitespace, or not a string.
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
    /// The name of the object in messages, for example <c>entry 2 of 'installed'</c>, or <see langword="null" /> for
    /// the output object.
    /// </param>
    /// <returns>
    /// The value of the field, or <see langword="null" /> if the field is missing or <see langword="null" />.
    /// </returns>
    /// <exception cref="CodexCommandFailedException">The field has a value that is not a string.</exception>
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
    /// The name of the object in messages, for example <c>entry 2 of 'installed'</c>, or <see langword="null" /> for
    /// the output object.
    /// </param>
    /// <returns>The value of the field.</returns>
    /// <exception cref="CodexCommandFailedException">
    /// The field is missing, <see langword="null" />, or not a Boolean.
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

    /// <summary>
    /// Reads a JSON object field that may be missing.
    /// </summary>
    /// <param name="element">The JSON object that contains the field.</param>
    /// <param name="propertyName">The name of the field.</param>
    /// <param name="subject">
    /// The name of the object in messages, for example <c>entry 2 of 'marketplaces'</c>, or <see langword="null" /> for
    /// the output object.
    /// </param>
    /// <returns>
    /// The value of the field, or <see langword="null" /> if the field is missing or <see langword="null" />.
    /// </returns>
    /// <exception cref="CodexCommandFailedException">The field has a value that is not a JSON object.</exception>
    public JsonElement? ReadOptionalObject(JsonElement element, string propertyName, string? subject)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Object
            ? value
            : throw Fail($"The field '{propertyName}' of the {Describe(subject)} is not a JSON object.");
    }

    private static string Describe(string? subject)
    {
        return subject is null ? "codex output" : $"{subject} of the codex output";
    }

    private CodexCommandFailedException CreateExitCodeException()
    {
        var exitCodeText = "codex exited with the exit code " + result.ExitCode.ToString(CultureInfo.InvariantCulture);

        var message = FindLastNonEmptyLine(result.StandardError) ?? FindLastNonEmptyLine(result.StandardOutput);

        var reason = message is null ? $"{exitCodeText}." : $"{exitCodeText}: {Sentences.Terminate(message)}";

        return new CodexCommandFailedException(arguments, result.ExitCode, reason, message);
    }

    // codex writes its error last (the deepest cause of an error chain is its last line), so the last line names the
    // cause; earlier lines may be warnings. Lines end at CRLF, LF, or a lone CR.
    private static string? FindLastNonEmptyLine(string text)
    {
        return text.Split(LineSeparators)
            .Select(x => x.Trim())
            .LastOrDefault(x => x.Length > 0);
    }

    private CodexCommandFailedException Fail(string reason, Exception? innerException = null)
    {
        return new CodexCommandFailedException(arguments, result.ExitCode, reason, innerException: innerException);
    }
}
