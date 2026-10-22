using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Cli.Output;

/// <summary>
/// Writes the JSON output of a command to a text writer, usually standard output.
/// </summary>
/// <remarks>
/// The document is indented with two spaces, uses camel-case property names, lower-case kebab-case enumeration values,
/// and LF line endings, keeps non-ASCII characters unescaped, and ends with a line break. The writer is flushed after every document. The output does not
/// go through Spectre.Console, which could wrap long lines. The writer records whether a document was written, so
/// that a run in JSON mode never writes a second one.
/// </remarks>
/// <param name="output">The text writer that receives the JSON documents.</param>
public sealed class CliJsonWriter(TextWriter output) : ICliJsonWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonEnumNames.NamingPolicy, allowIntegerValues: false) }
    };

    private readonly TextWriter _output = Ensure.NotNull(output);

    /// <summary>
    /// Gets a value that indicates whether an envelope was written.
    /// </summary>
    /// <value><see langword="true" /> if <see cref="Write" /> completed; otherwise, <see langword="false" />.</value>
    public bool HasWritten => WrittenEnvelope is not null;

    /// <summary>
    /// Gets the envelope that was written.
    /// </summary>
    /// <value>The envelope of the first <see cref="Write" /> that completed, or <see langword="null" /> if none did.</value>
    public CliJsonEnvelope? WrittenEnvelope { get; private set; }

    /// <inheritdoc />
    public void Write(CliJsonEnvelope envelope)
    {
        Ensure.NotNull(envelope);

        _output.Write(JsonSerializer.Serialize(envelope, SerializerOptions));
        _output.Write('\n');
        _output.Flush();

        WrittenEnvelope ??= envelope;
    }

    /// <summary>
    /// Writes the specified envelope only if no envelope was written yet.
    /// </summary>
    /// <param name="envelope">The envelope to write.</param>
    public void WriteIfNothingWritten(CliJsonEnvelope envelope)
    {
        if (!HasWritten)
        {
            Write(envelope);
        }
    }

    /// <summary>
    /// Gets the name with which the JSON output writes an enumeration value, so that plain output can use the same
    /// name.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enumeration.</typeparam>
    /// <param name="value">The enumeration value.</param>
    /// <returns>The name of <paramref name="value" /> in lower-case kebab case, for example <c>created</c>.</returns>
    public static string GetEnumName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonEnumNames.Get(value);
    }
}
