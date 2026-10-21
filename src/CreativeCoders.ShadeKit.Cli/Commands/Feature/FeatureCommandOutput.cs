using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Cli.Output;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Formats;
using Spectre.Console;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature;

/// <summary>
/// Provides the output steps that all commands of the <see cref="FeatureCommandGroup" /> share.
/// </summary>
internal static class FeatureCommandOutput
{
    /// <summary>
    /// Maps the format warnings and the feature warnings of a core result to the warnings of a JSON envelope.
    /// </summary>
    /// <param name="formatWarnings">The format warnings that came up while reading the files.</param>
    /// <param name="featureWarnings">The feature warnings of the result.</param>
    /// <returns>The format warnings first, then the feature warnings, each in its original order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="formatWarnings" /> or <paramref name="featureWarnings" /> is <see langword="null" />.
    /// </exception>
    public static IReadOnlyList<CliJsonMessage> CreateWarnings(IEnumerable<FormatWarning> formatWarnings,
        IEnumerable<FeatureWarning> featureWarnings)
    {
        Ensure.NotNull(formatWarnings);
        Ensure.NotNull(featureWarnings);

        return
        [
            .. formatWarnings.Select(CliErrorMapper.MapWarning),
            .. featureWarnings.Select(CliErrorMapper.MapWarning)
        ];
    }

    /// <summary>
    /// Writes the result of a command as one JSON envelope or as plain text after the warnings.
    /// </summary>
    /// <remarks>
    /// In JSON mode, the envelope carries <paramref name="data" /> and <paramref name="warnings" />, and
    /// <paramref name="writePlain" /> is not called. In plain mode, every warning is written as one line
    /// <c>Warning: &lt;message&gt;</c> with the message markup-escaped, followed by the lines of
    /// <paramref name="writePlain" />.
    /// </remarks>
    /// <param name="ansiConsole">The console that receives the plain output.</param>
    /// <param name="jsonWriter">The writer that receives the JSON envelope.</param>
    /// <param name="json">
    /// <see langword="true" /> if the command runs in JSON mode; otherwise, <see langword="false" />.
    /// </param>
    /// <param name="data">The <c>data</c> object of the envelope.</param>
    /// <param name="warnings">The warnings of the command.</param>
    /// <param name="writePlain">The action that writes the plain result lines.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="ansiConsole" />, <paramref name="jsonWriter" />, <paramref name="data" />,
    /// <paramref name="warnings" />, or <paramref name="writePlain" /> is <see langword="null" />.
    /// </exception>
    public static void Write(IAnsiConsole ansiConsole, ICliJsonWriter jsonWriter, bool json, object data,
        IReadOnlyList<CliJsonMessage> warnings, Action writePlain)
    {
        Ensure.NotNull(ansiConsole);
        Ensure.NotNull(jsonWriter);
        Ensure.NotNull(data);
        Ensure.NotNull(warnings);
        Ensure.NotNull(writePlain);

        if (json)
        {
            jsonWriter.Write(CliJsonEnvelope.Success(data, warnings));

            return;
        }

        WriteWarnings(ansiConsole, warnings);
        writePlain();
    }

    /// <summary>
    /// Writes every warning as one plain line <c>Warning: &lt;message&gt;</c> with the message markup-escaped.
    /// </summary>
    /// <param name="ansiConsole">The console that receives the lines.</param>
    /// <param name="warnings">The warnings to write.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="ansiConsole" /> or <paramref name="warnings" /> is <see langword="null" />.
    /// </exception>
    public static void WriteWarnings(IAnsiConsole ansiConsole, IEnumerable<CliJsonMessage> warnings)
    {
        Ensure.NotNull(ansiConsole);
        Ensure.NotNull(warnings);

        foreach (var warning in warnings)
        {
            ansiConsole.MarkupLine($"Warning: {Markup.Escape(warning.Message)}");
        }
    }

    /// <summary>
    /// Writes the specified text as one plain line with every markup character escaped.
    /// </summary>
    /// <param name="ansiConsole">The console that receives the line.</param>
    /// <param name="text">The text of the line.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="ansiConsole" /> or <paramref name="text" /> is <see langword="null" />.
    /// </exception>
    public static void WriteLine(IAnsiConsole ansiConsole, string text)
    {
        Ensure.NotNull(ansiConsole);
        Ensure.NotNull(text);

        ansiConsole.MarkupLine(Markup.Escape(text));
    }
}
