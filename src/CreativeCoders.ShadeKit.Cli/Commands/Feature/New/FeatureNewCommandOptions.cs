using CreativeCoders.SysConsole.Cli.Parsing;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature.New;

/// <summary>
/// Represents the options of the <see cref="FeatureNewCommand" />.
/// </summary>
public class FeatureNewCommandOptions : JsonCommandOptions
{
    /// <summary>
    /// Gets or sets the workflow profile of the new feature.
    /// </summary>
    /// <value>
    /// <c>feature</c>, <c>bugfix</c>, or <c>quick</c>, or <see langword="null" /> to use <c>defaultProfile</c> of
    /// <c>config.json</c>. The default is <see langword="null" />.
    /// </value>
    [OptionParameter('p', "profile",
        HelpText = "The workflow profile: feature, bugfix, or quick (default: defaultProfile of config.json)")]
    public string? Profile { get; set; }

    /// <summary>
    /// Gets or sets the source of the request of the new feature.
    /// </summary>
    /// <value>
    /// An absolute <c>http</c> or <c>https</c> URL, the path of an existing UTF-8 text file of at most 1 MiB, or the
    /// request text. The default is an empty string.
    /// </value>
    [OptionParameter('s', "source", IsRequired = true,
        HelpText = "The request: an issue URL, the path of an existing UTF-8 text file (at most 1 MiB), or the " +
                   "request text")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the new feature.
    /// </summary>
    /// <value>
    /// The title, or <see langword="null" /> to derive it from a text or file source. The default is
    /// <see langword="null" />.
    /// </value>
    [OptionParameter('t', "title",
        HelpText = "The feature title; required for a URL source, otherwise derived from the source")]
    public string? Title { get; set; }
}
