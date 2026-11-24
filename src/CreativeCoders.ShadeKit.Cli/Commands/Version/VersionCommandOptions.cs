using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the options of the <see cref="VersionCommand" />.
/// </summary>
public class VersionCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the CLI contract that the kit skills are written for.
    /// </summary>
    /// <value>
    /// The CLI contract number, for example <c>1</c>, or <see langword="null" /> to skip the contract check and list
    /// the installed integrations instead. <c>--expect-contract</c> without a value or with a value that is not a
    /// CLI contract number is rejected while the arguments are read (see
    /// <see cref="ExpectedContractOptionConverter" />). The default is <see langword="null" />.
    /// </value>
    [OptionParameter("expect-contract",
        HelpText = "The CLI contract the kit skills are written for; the command fails unless this engine supports it",
        Converter = typeof(ExpectedContractOptionConverter))]
    public int? ExpectContract { get; set; }
}
