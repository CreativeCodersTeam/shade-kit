using System.Globalization;
using System.Text.Json;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;
using CreativeCoders.ShadeKit.Core.Processes;
using CreativeCoders.ShadeKit.Core.Text;

namespace CreativeCoders.ShadeKit.Core.Integrations.Claude;

/// <summary>
/// Reads the JSON output of a finished <c>claude ... --json</c> command and reports every failure of the command as a
/// <see cref="ClaudeCommandFailedException" />.
/// </summary>
/// <remarks>
/// A command that changes the configuration prints one JSON object per line, with the fields <c>outcome</c>,
/// <c>message</c>, and, on failure, <c>failureCode</c>; the last non-empty line of standard output that is a JSON
/// object is its result. A list command prints one JSON array. Unknown fields are ignored.
/// </remarks>
/// <param name="arguments">The arguments of the <c>claude</c> command that produced the output.</param>
/// <param name="result">The result of the finished <c>claude</c> process.</param>
internal sealed class ClaudeJsonOutputReader(IReadOnlyList<string> arguments, ExternalProcessResult result)
{
    /// <summary>
    /// Represents the outcome that <c>claude</c> reports for a successful command.
    /// </summary>
    public const string OkOutcome = "ok";

    private const string OutcomeProperty = "outcome";

    private const string MessageProperty = "message";

    private const string FailureCodeProperty = "failureCode";

    /// <summary>
    /// Gets the reader for the fields of the output, whose failures are reported as
    /// <see cref="ClaudeCommandFailedException" />.
    /// </summary>
    /// <value>The shared field reader for the tool name <c>claude</c>.</value>
    public AgentJsonFieldReader<ClaudeCommandFailedException> Fields { get; } = new("claude", result,
        (reason, claudeMessage, innerException) => new ClaudeCommandFailedException(arguments, result.ExitCode,
            reason, claudeMessage, innerException: innerException));

    /// <summary>
    /// Returns the result object of a command that changes the configuration.
    /// </summary>
    /// <returns>
    /// A copy of the last JSON object line of standard output, whose <c>outcome</c> is <see cref="OkOutcome" />.
    /// </returns>
    /// <exception cref="ClaudeCommandFailedException">
    /// <c>claude</c> exited with a non-zero exit code, printed no JSON object line, or reported an outcome other than
    /// <see cref="OkOutcome" />.
    /// </exception>
    public JsonElement ReadResultObject()
    {
        var resultObject = FindLastJsonObjectLine();

        if (result.ExitCode != 0)
        {
            throw CreateExitCodeException(resultObject);
        }

        if (resultObject is not { } root)
        {
            throw Fail(CreateNoJsonObjectReason("claude printed no JSON object on standard output"));
        }

        var outcome = GetStringOrNull(root, OutcomeProperty);

        if (string.Equals(outcome, OkOutcome, StringComparison.Ordinal))
        {
            return root;
        }

        var outcomeText = outcome is null
            ? "claude reported no outcome"
            : $"claude reported the outcome '{outcome}'";

        throw CreateReportedFailureException(outcomeText, root);
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
    /// <exception cref="ClaudeCommandFailedException">
    /// <c>claude</c> exited with a non-zero exit code or its standard output is not a JSON array.
    /// </exception>
    public IReadOnlyList<T> ReadArray<T>(Func<JsonElement, string, T> readEntry)
    {
        if (result.ExitCode != 0)
        {
            throw CreateExitCodeException(FindLastJsonObjectLine());
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException exception)
        {
            throw Fail($"The output of claude is not valid JSON: {Sentences.Terminate(exception.Message)}",
                innerException: exception);
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw Fail("The output of claude is not a JSON array.");
            }

            return
            [
                .. root.EnumerateArray().Select((entry, index) =>
                    readEntry(entry, $"entry {(index + 1).ToString(CultureInfo.InvariantCulture)}"))
            ];
        }
    }

    // Searches from the end, because claude may print other lines before its result line.
    private JsonElement? FindLastJsonObjectLine()
    {
        var lines = result.StandardOutput.Split('\n')
            .Select(x => x.Trim())
            .Where(x => x.StartsWith('{'))
            .Reverse();

        foreach (var line in lines)
        {
            try
            {
                using var document = JsonDocument.Parse(line);

                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return document.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                // Not a JSON object line; an earlier line may be the result.
            }
        }

        return null;
    }

    private ClaudeCommandFailedException CreateExitCodeException(JsonElement? resultObject)
    {
        var exitCodeText = Fields.ExitCodeText;

        if (resultObject is not { } root)
        {
            return Fail(CreateNoJsonObjectReason($"{exitCodeText} and printed no JSON object on standard output"));
        }

        var outcome = GetStringOrNull(root, OutcomeProperty);

        var failureText = outcome is null
            ? exitCodeText
            : $"{exitCodeText} and reported the outcome '{outcome}'";

        return CreateReportedFailureException(failureText, root);
    }

    private ClaudeCommandFailedException CreateReportedFailureException(string failureText, JsonElement root)
    {
        var message = GetStringOrNull(root, MessageProperty);
        var failureCode = GetStringOrNull(root, FailureCodeProperty);

        var reason = failureText;

        if (!string.IsNullOrWhiteSpace(message))
        {
            reason += $": {message.Trim().TrimEnd('.')}";
        }

        if (!string.IsNullOrWhiteSpace(failureCode))
        {
            reason += $" (failure code '{failureCode}')";
        }

        return Fail(reason + ".", message, failureCode);
    }

    private string CreateNoJsonObjectReason(string failureText)
    {
        var standardError = result.StandardError.Trim();

        return standardError.Length == 0
            ? $"{failureText}."
            : $"{failureText}: {Sentences.Terminate(standardError)}";
    }

    private ClaudeCommandFailedException Fail(string reason, string? claudeMessage = null,
        string? failureCode = null, Exception? innerException = null)
    {
        return new ClaudeCommandFailedException(arguments, result.ExitCode, reason, claudeMessage, failureCode,
            innerException);
    }

    // The fields of a result object are read leniently: a value of another type counts as missing.
    private static string? GetStringOrNull(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
