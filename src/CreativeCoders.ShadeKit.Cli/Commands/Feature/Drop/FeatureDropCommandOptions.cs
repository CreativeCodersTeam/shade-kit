using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.Drop;

/// <summary>
/// Represents the options of the <see cref="FeatureDropCommand" />.
/// </summary>
public class FeatureDropCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the id of the feature to drop.
    /// </summary>
    /// <value>The feature id, the first positional argument. The default is an empty string.</value>
    [OptionValue(0, IsRequired = true, HelpText = "The id of the feature to drop")]
    public string FeatureId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value that indicates whether the feature is dropped without a confirmation.
    /// </summary>
    /// <value>
    /// <see langword="true" /> to delete the feature at once; <see langword="false" /> to describe what would be
    /// deleted first. The default is <see langword="false" />.
    /// </value>
    [OptionParameter('y', "yes", HelpText = "Drops the feature without asking for a confirmation")]
    public bool Yes { get; set; }
}
