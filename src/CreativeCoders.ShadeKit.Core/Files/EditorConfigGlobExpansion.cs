using System.Globalization;
using System.Text.RegularExpressions;

namespace CreativeCoders.ShadeKit.Core.Files;

/// <summary>
/// Counts the patterns into which the EditorConfig core library expands the braces of a section glob, without
/// expanding them.
/// </summary>
/// <remarks>
/// The library expands every brace of a glob, alternatives (<c>{a,b}</c>) and numeric ranges (<c>{1..3}</c>), into
/// one string per combination before it matches a path, for every <c>.editorconfig</c> file it reads. A glob such as
/// <c>[{1..20000}{1..20000}]</c> or thirty <c>{a,b}</c> groups would make it build hundreds of millions of strings.
/// The count follows <c>GlobMatcher.BraceExpand</c> of EditorConfig.Core 0.18.0 step by step, including its handling
/// of backslashes and unbalanced braces, and stops counting above the limit.
/// </remarks>
internal static partial class EditorConfigGlobExpansion
{
    /// <summary>
    /// The largest number of patterns into which a section glob may expand.
    /// </summary>
    public const long MaxPatternCount = 10_000;

    // Above the limit the exact count does not matter; counting stops there, so the products cannot overflow.
    private const long TooMany = MaxPatternCount + 1;

    /// <summary>
    /// Determines whether the EditorConfig core library would expand the specified section glob into more than
    /// <see cref="MaxPatternCount" /> patterns.
    /// </summary>
    /// <param name="glob">The glob of a section, as <c>ConfigSection.Glob</c> holds it.</param>
    /// <returns>
    /// <see langword="true" /> if the glob expands into more than <see cref="MaxPatternCount" /> patterns or has a
    /// numeric range with a number that does not fit into an <see cref="int" />; otherwise, <see langword="false" />.
    /// </returns>
    public static bool IsTooLarge(string glob)
    {
        ArgumentNullException.ThrowIfNull(glob);

        // GlobMatcher.Create trims the glob, treats a leading # as a comment, and removes leading ! before it expands
        // the braces.
        var pattern = glob.Trim();

        if (pattern.Length == 0 || pattern[0] == '#')
        {
            return false;
        }

        return CountPatterns(pattern.TrimStart('!')) > MaxPatternCount;
    }

    // Mirrors BraceExpand: returns the number of patterns, or TooMany for more than MaxPatternCount patterns or a
    // number that int.Parse would reject.
    private static long CountPatterns(string pattern)
    {
        var openIndex = pattern.IndexOf('{');

        if (openIndex == -1 || pattern.IndexOf('}', openIndex + 1) == -1)
        {
            return 1;
        }

        if (pattern[0] != '{')
        {
            var braceIndex = FindFirstUnescapedBrace(pattern);

            return braceIndex == -1 ? 1 : CountPatterns(pattern[braceIndex..]);
        }

        var numericRange = NumericRangeRegex().Match(pattern);

        if (numericRange.Success)
        {
            if (!int.TryParse(numericRange.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                    out var from)
                || !int.TryParse(numericRange.Groups[2].Value, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var to))
            {
                return TooMany;
            }

            var rangeLength = Math.Abs((long)to - from) + 1;

            return Multiply(rangeLength, CountPatterns(pattern[numericRange.Length..]));
        }

        return CountAlternatives(pattern);
    }

    // The prefix loop of BraceExpand: a backslash toggles the escape flag, and only another backslash resets it.
    private static int FindFirstUnescapedBrace(string pattern)
    {
        var isEscaped = false;

        for (var index = 0; index < pattern.Length; index++)
        {
            switch (pattern[index])
            {
                case '\\':
                    isEscaped = !isEscaped;
                    break;

                case '{' when !isEscaped:
                    return index;
            }
        }

        return -1;
    }

    // The alternatives of a pattern that starts with {: each alternative is expanded on its own, and the rest of the
    // pattern after the closing brace once.
    private static long CountAlternatives(string pattern)
    {
        var depth = 1;
        var isEscaped = false;
        var alternativeStart = 1;
        List<string> alternatives = [];
        int index;

        for (index = 1; index < pattern.Length && depth > 0; index++)
        {
            var character = pattern[index];

            if (isEscaped)
            {
                isEscaped = false;

                continue;
            }

            switch (character)
            {
                case '\\':
                    isEscaped = true;
                    break;

                case '{':
                    depth++;
                    break;

                case '}':
                    depth--;

                    if (depth == 0)
                    {
                        alternatives.Add(pattern[alternativeStart..index]);
                    }

                    break;

                case ',' when depth == 1:
                    alternatives.Add(pattern[alternativeStart..index]);
                    alternativeStart = index + 1;
                    break;
            }
        }

        // An unbalanced brace is escaped and the pattern expanded again, as BraceExpand does.
        if (depth != 0)
        {
            return CountPatterns("\\" + pattern);
        }

        var alternativeCount = 0L;

        foreach (var alternative in alternatives)
        {
            alternativeCount = Math.Min(alternativeCount + CountPatterns(alternative), TooMany);
        }

        return Multiply(alternativeCount, CountPatterns(pattern[index..]));
    }

    private static long Multiply(long left, long right)
    {
        if (left == 0 || right == 0)
        {
            return 0;
        }

        return left > TooMany / right ? TooMany : Math.Min(left * right, TooMany);
    }

    // The numeric range regex of GlobMatcher, anchored at the start of the pattern.
    [GeneratedRegex(@"^\{(-?[0-9]+)\.\.(-?[0-9]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex NumericRangeRegex();
}
