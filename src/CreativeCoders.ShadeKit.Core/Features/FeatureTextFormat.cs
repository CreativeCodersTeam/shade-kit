using System.Globalization;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Formats the values of a feature as text in the same way wherever SHADE kit writes them for people, for example in
/// the <c>request.md</c> of a tracker issue and in the plain output of <c>shade-kit feature show</c>.
/// </summary>
public static class FeatureTextFormat
{
    /// <summary>
    /// Represents the custom format string of a creation time: ISO 8601 with whole seconds and the UTC offset.
    /// </summary>
    public const string CreatedAtFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>
    /// Formats the specified creation time of a feature.
    /// </summary>
    /// <param name="createdAt">The creation time with its UTC offset.</param>
    /// <returns>
    /// The creation time in ISO 8601 format with the offset of <paramref name="createdAt" />, for example
    /// <c>2026-10-20T15:30:45+02:00</c>.
    /// </returns>
    public static string FormatCreatedAt(DateTimeOffset createdAt)
    {
        return createdAt.ToString(CreatedAtFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats the creation time and the creator of the specified feature as one line.
    /// </summary>
    /// <param name="feature">The <c>feature.json</c> document of the feature.</param>
    /// <returns>
    /// The text <c>&lt;createdAt&gt; by &lt;name&gt; &lt;&lt;email&gt;&gt;</c>, for example
    /// <c>2026-10-20T15:30:45+02:00 by Jane Doe &lt;jane@example.com&gt;</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="feature" /> is <see langword="null" />.</exception>
    public static string FormatCreated(FeatureDocument feature)
    {
        Ensure.NotNull(feature);

        return $"{FormatCreatedAt(feature.CreatedAt)} by {feature.CreatedBy.Name} <{feature.CreatedBy.Email}>";
    }
}
