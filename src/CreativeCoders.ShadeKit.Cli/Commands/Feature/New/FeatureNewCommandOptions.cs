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
    /// request text. The issue of a URL whose host is a tracker in <c>config.json</c> is read with <c>gh</c>; any other
    /// URL is read with an HTTP GET. The default is an empty string.
    /// </value>
    [OptionParameter('s', "source", IsRequired = true,
        HelpText = "The request: an issue URL of a tracker in config.json (read with gh), another http(s) URL " +
                   "(its content is downloaded), the path of an existing UTF-8 text file (at most 1 MiB), or the " +
                   "request text")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the new feature.
    /// </summary>
    /// <value>
    /// The title, or <see langword="null" /> to derive it from a text or file source or from the issue of a tracker
    /// URL; required for a URL whose host is not a tracker. The default is <see langword="null" />.
    /// </value>
    [OptionParameter('t', "title",
        HelpText = "The feature title; required for a URL that is not an issue URL of a tracker in config.json, " +
                   "otherwise derived from the source (the issue title for an issue URL)")]
    public string? Title { get; set; }
}
