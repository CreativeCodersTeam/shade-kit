using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Set;

/// <summary>
/// Represents the options of the <see cref="FeatureSetCommand" />.
/// </summary>
public class FeatureSetCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the id of the feature that becomes the current feature.
    /// </summary>
    /// <value>The feature id, the first positional argument. The default is an empty string.</value>
    [OptionValue(0, IsRequired = true, HelpText = "The id of the feature that becomes the current feature")]
    public string FeatureId { get; set; } = string.Empty;
}
