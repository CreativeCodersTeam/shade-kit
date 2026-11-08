using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Gate.Info;

/// <summary>
/// Represents the options of the <see cref="GateInfoCommand" />.
/// </summary>
public class GateInfoCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the target phase of the gate to read.
    /// </summary>
    /// <value>
    /// The name of a phase of the workflow profile of the feature, for example <c>plan</c>, the first positional
    /// argument. The default is an empty string.
    /// </value>
    [OptionValue(0, IsRequired = true, HelpText = "The phase whose gate is read, for example plan")]
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the id of the feature whose gate is read.
    /// </summary>
    /// <value>
    /// The feature id, passed to the core unchanged, which also validates it; or <see langword="null" /> to read the
    /// current feature, resolved as <c>shade-kit feature current</c> resolves it. The default is
    /// <see langword="null" />.
    /// </value>
    [OptionParameter("feature", HelpText = "The id of the feature (default: the current feature)")]
    public string? FeatureId { get; set; }
}
