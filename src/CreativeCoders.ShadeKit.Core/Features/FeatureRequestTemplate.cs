using System.Text;
using System.Text.RegularExpressions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Files;
using CreativeCoders.ShadeKit.Core.Git;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Fills the <c>request.md</c> template with the identity of a new feature.
/// </summary>
/// <remarks>
/// Only the placeholders that <c>shade-kit feature new</c> owns are replaced; every other part of the template, the
/// guidance comment included, stays as it is, because the skill of package 09 completes the file.
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
    /// Decodes the bytes of a template file as UTF-8, without a leading UTF-8 byte order mark.
    /// </summary>
    public static string Decode(byte[] content)
    {
        Ensure.NotNull(content);

        return Encoding.UTF8.GetString(TextFileEncoding.SkipUtf8Bom(content));
    }

    /// <summary>
    /// Replaces every occurrence of the placeholders of <c>shade-kit feature new</c> in the template.
    /// </summary>
    /// <remarks>
    /// The template is scanned once, so a replacement value that contains a placeholder, for example request text
    /// that quotes the template, is never replaced again.
    /// </remarks>
    public static string Fill(string template, FeatureIdentity identity, GitIdentity creator)
    {
        Ensure.NotNull(template);
        Ensure.NotNull(identity);
        Ensure.NotNull(creator);

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [FeatureTitlePlaceholder] = identity.Title,
            [FeatureIdPlaceholder] = identity.Id,
            [SourcePlaceholder] = FormatSource(identity.Source),
            [CreatedPlaceholder] = FeatureTextFormat.FormatCreated(identity.CreatedAt, creator.Name, creator.Email),
            [OriginalInputPlaceholder] = identity.OriginalInput
        };

        return PlaceholderRegex().Replace(template, match => values[match.Value]);
    }

    private static string FormatSource(FeatureSource source)
    {
        var kind = source.Kind switch
        {
            FeatureSourceKind.Text => "text",
            FeatureSourceKind.Url => "url",
            FeatureSourceKind.File => "file",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source.Kind, "The source kind is not defined.")
        };

        return source.Kind == FeatureSourceKind.Text
            ? $"{kind}: {NoSourceReference}"
            : $"{kind}: {source.Value}";
    }

    [GeneratedRegex(
        @"\{\{(?:feature-title|feature-id|source kind and source reference|original input, unchanged|" +
        @"created at and created by)\}\}",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
