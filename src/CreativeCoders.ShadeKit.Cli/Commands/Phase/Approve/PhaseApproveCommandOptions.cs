using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Approve;

/// <summary>
/// Represents the options of the <see cref="PhaseApproveCommand" />.
/// </summary>
public class PhaseApproveCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the target phase of the pending transition to approve.
    /// </summary>
    /// <value>The name of the phase, the first positional argument. The default is an empty string.</value>
    [OptionValue(0, IsRequired = true, HelpText = "The target phase of the pending transition to approve")]
    public string Phase { get; set; } = string.Empty;
}
