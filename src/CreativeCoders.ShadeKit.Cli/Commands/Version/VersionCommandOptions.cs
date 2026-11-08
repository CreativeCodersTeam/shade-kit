using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Version;

/// <summary>
/// Represents the options of the <see cref="VersionCommand" />.
/// </summary>
public class VersionCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the engine version that the kit skills expect.
    /// </summary>
    /// <value>
    /// The SemVer 2.0.0 version as passed, for example <c>1.2.0</c>, or <see langword="null" /> to skip the version
    /// check. An empty string is checked like any other value and is rejected. <c>--expect</c> without a value is
    /// rejected while the arguments are read (see <see cref="ExpectedVersionOptionConverter" />). The default is
    /// <see langword="null" />.
    /// </value>
    [OptionParameter("expect",
        HelpText = "The engine version the kit skills expect; the major and minor versions must be equal",
        Converter = typeof(ExpectedVersionOptionConverter))]
    public string? Expect { get; set; }
}
