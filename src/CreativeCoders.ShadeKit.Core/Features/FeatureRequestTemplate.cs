using System.Text.RegularExpressions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Formats;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Fills the <c>request.md</c> template with the <c>feature.json</c> document and the original input of a new feature.
/// </summary>
/// <remarks>
/// Only the placeholders of <c>shade-kit feature new</c> are replaced; a skill completes the rest of the file.
/// </remarks>
internal static partial class FeatureRequestTemplate
{
    public const string FeatureTitlePlaceholder = "{{feature-title}}";

    public const string FeatureIdPlaceholder = "{{feature-id}}";

    public const string SourcePlaceholder = "{{source kind and source reference}}";

    public const string OriginalInputPlaceholder = "{{original input, unchanged}}";

    public const string CreatedPlaceholder = "{{created at and created by}}";

    private const string NoSourceReference = "n/a";

    /// <summary>
    /// Replaces every occurrence of the placeholders of <c>shade-kit feature new</c> in the template.
    /// </summary>
    /// <remarks>
    /// The template is scanned once, so a replacement value that contains a placeholder, for example request text
    /// that quotes the template, is never replaced again.
    /// </remarks>
    public static string Fill(string template, FeatureDocument feature, string originalInput)
    {
        Ensure.NotNull(template);
        Ensure.NotNull(feature);
        Ensure.NotNull(originalInput);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FeatureTitlePlaceholder] = feature.Title,
            [FeatureIdPlaceholder] = feature.Id,
            [SourcePlaceholder] = FormatSource(feature.Source),
            [CreatedPlaceholder] = FeatureTextFormat.FormatCreated(feature),
            [OriginalInputPlaceholder] = originalInput
        };

        return PlaceholderRegex().Replace(template,
            match => values.TryGetValue(match.Value, out var value) ? value : match.Value);
    }

    private static string FormatSource(FeatureSource source)
    {
        return source.Kind switch
        {
            FeatureSourceKind.Text => $"{source.Kind.ToJsonName()}: {NoSourceReference}",
            FeatureSourceKind.Url or FeatureSourceKind.File => $"{source.Kind.ToJsonName()}: {source.Value}",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, "The source kind is not defined.")
        };
    }

    // Every {{...}} is matched; one that is not a placeholder of shade-kit feature new is kept as it is.
    [GeneratedRegex(@"\{\{[^{}]+\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
