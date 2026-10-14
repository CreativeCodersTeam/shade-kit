using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands;

/// <summary>
/// Represents the base class of the options of every agent-facing command, which adds the <c>--json</c> switch.
/// </summary>
public class JsonCommandOptions
{
    /// <summary>
    /// Represents the long name of the switch that turns on JSON mode.
    /// </summary>
    public const string JsonOptionName = "json";

    /// <summary>
    /// Gets or sets a value that indicates whether the command writes its result as a JSON document to standard
    /// output.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if the command writes a JSON envelope; otherwise, <see langword="false" />. The default
    /// is <see langword="false" />.
    /// </value>
    [OptionParameter(JsonOptionName, HelpText = "Writes the result as a JSON document to standard output")]
    public bool Json { get; set; }
}
