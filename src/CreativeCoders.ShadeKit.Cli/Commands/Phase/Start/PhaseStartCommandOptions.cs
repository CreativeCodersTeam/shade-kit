using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Start;

/// <summary>
/// Represents the options of the <see cref="PhaseStartCommand" />.
/// </summary>
public class PhaseStartCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the phase to start.
    /// </summary>
    /// <value>
    /// The name of a phase of the workflow profile of the current feature, the first positional argument. The default
    /// is an empty string.
    /// </value>
    [OptionValue(0, IsRequired = true,
        HelpText = "The phase to start: the next phase, the current phase, or an earlier phase to reopen")]
    public string Phase { get; set; } = string.Empty;
}
