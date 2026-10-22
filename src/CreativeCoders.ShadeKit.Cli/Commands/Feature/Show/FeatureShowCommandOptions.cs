using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Show;

/// <summary>
/// Represents the options of the <see cref="FeatureShowCommand" />.
/// </summary>
public class FeatureShowCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the id of the feature to show.
    /// </summary>
    /// <value>The feature id, the first positional argument. The default is an empty string.</value>
    [OptionValue(0, IsRequired = true, HelpText = "The id of the feature to show")]
    public string FeatureId { get; set; } = string.Empty;
}
