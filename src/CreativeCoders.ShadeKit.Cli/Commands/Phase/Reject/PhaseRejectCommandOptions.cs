using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Phase.Reject;

/// <summary>
/// Represents the options of the <see cref="PhaseRejectCommand" />.
/// </summary>
public class PhaseRejectCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the target phase of the pending transition to reject.
    /// </summary>
    /// <value>The name of the phase, the first positional argument. The default is an empty string.</value>
    [OptionValue(0, IsRequired = true, HelpText = "The target phase of the pending transition to reject")]
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the note that is recorded with the rejection.
    /// </summary>
    /// <value>
    /// The note, for example the failed checklist items, or <see langword="null" /> for no note. The note is trimmed;
    /// an empty or whitespace note counts as none. The default is <see langword="null" />.
    /// </value>
    [OptionParameter('n', "note", HelpText = "The reason for the rejection, recorded in the history")]
    public string? Note { get; set; }
}
