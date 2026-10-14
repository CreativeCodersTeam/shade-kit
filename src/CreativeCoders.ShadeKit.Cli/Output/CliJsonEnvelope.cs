using System.Text.Json.Serialization;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Represents the JSON document that every agent-facing command writes to standard output in JSON mode.
/// </summary>
/// <remarks>
/// All four fields are always present. On success, <see cref="Ok" /> is <see langword="true" />,
/// <see cref="Data" /> holds the result, and <see cref="Error" /> is <see langword="null" />. On failure,
/// <see cref="Ok" /> is <see langword="false" />, <see cref="Data" /> is <see langword="null" />, and
/// <see cref="Error" /> describes the failure.
/// </remarks>
public sealed record CliJsonEnvelope
{
    private CliJsonEnvelope(bool ok, object? data, IReadOnlyList<CliJsonMessage> warnings, CliJsonMessage? error)
    {
        Ok = ok;
        Data = data;
        Warnings = warnings;
        Error = error;
    }

    /// <summary>
    /// Gets a value that indicates whether the command succeeded.
    /// </summary>
    /// <value><see langword="true" /> if the command succeeded; otherwise, <see langword="false" />.</value>
    [JsonPropertyOrder(0)]
    public bool Ok { get; }

    /// <summary>
    /// Gets the result of the command.
    /// </summary>
    /// <value>The result object, serialized with its runtime type, or <see langword="null" /> on failure.</value>
    [JsonPropertyOrder(1)]
    public object? Data { get; }

    /// <summary>
    /// Gets the warnings the command reported.
    /// </summary>
    /// <value>The warnings in the order they came up; an empty list if there were none.</value>
    [JsonPropertyOrder(2)]
    public IReadOnlyList<CliJsonMessage> Warnings { get; }

    /// <summary>
    /// Gets the error that stopped the command.
    /// </summary>
    /// <value>The error, or <see langword="null" /> on success.</value>
    [JsonPropertyOrder(3)]
    public CliJsonMessage? Error { get; }

    /// <summary>
    /// Creates the envelope of a successful command.
    /// </summary>
    /// <param name="data">The result of the command.</param>
    /// <param name="warnings">The warnings the command reported, or <see langword="null" /> if there were none.</param>
    /// <returns>An envelope with <see cref="Ok" /> set to <see langword="true" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="warnings" /> contains <see langword="null" />.</exception>
    public static CliJsonEnvelope Success(object data, IEnumerable<CliJsonMessage>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new CliJsonEnvelope(true, data, CopyWarnings(warnings), null);
    }

    /// <summary>
    /// Creates the envelope of a failed command.
    /// </summary>
    /// <param name="error">The error that stopped the command.</param>
    /// <param name="warnings">
    /// The warnings the command reported before it failed, or <see langword="null" /> if there were none.
    /// </param>
    /// <returns>An envelope with <see cref="Ok" /> set to <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="warnings" /> contains <see langword="null" />.</exception>
    public static CliJsonEnvelope Failure(CliJsonMessage error, IEnumerable<CliJsonMessage>? warnings = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new CliJsonEnvelope(false, null, CopyWarnings(warnings), error);
    }

    private static CliJsonMessage[] CopyWarnings(IEnumerable<CliJsonMessage>? warnings)
    {
        CliJsonMessage[] copy = [.. warnings ?? []];

        return copy.Contains(null)
            ? throw new ArgumentException("The warnings must not contain null.", nameof(warnings))
            : copy;
    }
}
