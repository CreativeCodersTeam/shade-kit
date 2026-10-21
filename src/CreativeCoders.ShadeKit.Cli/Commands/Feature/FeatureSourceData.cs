using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Cli.Commands.Feature;

/// <summary>
/// Represents the <c>source</c> object in the JSON output of the feature commands.
/// </summary>
/// <param name="Kind">The kind of the source, written as <c>text</c>, <c>url</c>, or <c>file</c>.</param>
/// <param name="Value">
/// The URL or the file path of the source, or <see langword="null" /> for a <c>text</c> source.
/// </param>
public sealed record FeatureSourceData(FeatureSourceKind Kind, string? Value)
{
    /// <summary>
    /// Creates the data from the source of a feature.
    /// </summary>
    /// <param name="source">The source of the feature.</param>
    /// <returns>The data with the kind and the value of <paramref name="source" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source" /> is <see langword="null" />.</exception>
    public static FeatureSourceData FromSource(FeatureSource source)
    {
        Ensure.NotNull(source);

        return new FeatureSourceData(source.Kind, source.Value);
    }
}
