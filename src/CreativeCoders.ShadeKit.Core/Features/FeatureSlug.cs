using System.Globalization;
using System.Text;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Builds the slug part of a feature id from a title.
/// </summary>
internal static class FeatureSlug
{
    /// <summary>
    /// Represents the maximum length of a slug.
    /// </summary>
    public const int MaxLength = 40;

    private const char Separator = '-';

    /// <summary>
    /// Creates the slug of the specified title.
    /// </summary>
    /// <remarks>
    /// Removes diacritics, lowercases, turns every run of other characters into one <c>-</c>, and cuts a slug longer
    /// than <see cref="MaxLength" /> at the last <c>-</c> within the limit, so no word is split.
    /// </remarks>
    /// <param name="title">The title to build the slug from.</param>
    /// <returns>
    /// The slug, which contains only <c>a</c>–<c>z</c>, <c>0</c>–<c>9</c>, and single inner <c>-</c>, and is at most
    /// <see cref="MaxLength" /> characters long; or an empty string if the title contains no such letter or digit.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="title" /> is <see langword="null" />.</exception>
    public static string Create(string title)
    {
        Ensure.NotNull(title);

        var normalizedTitle = title.Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(normalizedTitle.Length);
        var pendingSeparator = false;

        foreach (var character in normalizedTitle)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lowerCharacter = char.ToLowerInvariant(character);

            if (!char.IsAsciiLetterLower(lowerCharacter) && !char.IsAsciiDigit(lowerCharacter))
            {
                pendingSeparator = true;

                continue;
            }

            if (pendingSeparator && slug.Length > 0)
            {
                slug.Append(Separator);
            }

            pendingSeparator = false;
            slug.Append(lowerCharacter);
        }

        return Shorten(slug.ToString());
    }

    private static string Shorten(string slug)
    {
        if (slug.Length <= MaxLength)
        {
            return slug;
        }

        // A separator directly after the limit is a word boundary as well, so the first MaxLength characters stay.
        var lastSeparatorIndex = slug.LastIndexOf(Separator, MaxLength);
        var shortenedSlug = lastSeparatorIndex > 0
            ? slug[..lastSeparatorIndex]
            : slug[..MaxLength];

        return shortenedSlug.Trim(Separator);
    }
}
