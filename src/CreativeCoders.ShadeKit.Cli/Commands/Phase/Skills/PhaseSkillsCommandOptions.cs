using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Skills;

/// <summary>
/// Represents the options of the <see cref="PhaseSkillsCommand" />.
/// </summary>
public class PhaseSkillsCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the phase whose skills are returned.
    /// </summary>
    /// <value>
    /// The name of a phase of the workflow profile of the current feature, the first positional argument. The default
    /// is an empty string.
    /// </value>
    [OptionValue(0, IsRequired = true, HelpText = "The phase whose skills are listed")]
    public string Phase { get; set; } = string.Empty;
}
