using System.Text.RegularExpressions;
using CreativeCoders.Core;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Provides the rules for valid feature ids.
/// </summary>
/// <remarks>
/// A valid feature id consists of one or more groups of lowercase ASCII letters (<c>a</c>–<c>z</c>) and digits,
/// joined by single hyphens, for example <c>gh-142-login-sso</c> or <c>20261013-1530-login-sso</c>. Because such an id
/// contains no path separator and no dot, it is safe to use as a directory name; every id is checked before a path is
/// built from it.
/// </remarks>
public static partial class FeatureIds
{
    /// <summary>
    /// Determines whether the specified text is a valid feature id.
    /// </summary>
    /// <param name="featureId">The text to check.</param>
    /// <returns>
    /// <see langword="true" /> if <paramref name="featureId" /> matches <c>^[a-z0-9]+(-[a-z0-9]+)*$</c> without a
    /// trailing line break; otherwise, <see langword="false" />, also if <paramref name="featureId" /> is
    /// <see langword="null" />.
    /// </returns>
    public static bool IsValid(string? featureId)
    {
        return featureId is not null && FeatureIdRegex().IsMatch(featureId);
    }

    /// <summary>
    /// Ensures that the specified text is a valid feature id.
    /// </summary>
    /// <param name="featureId">The text to check.</param>
    /// <exception cref="ArgumentNullException"><paramref name="featureId" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidFeatureIdException"><paramref name="featureId" /> is not a valid feature id.</exception>
    public static void EnsureValid(string featureId)
    {
        Ensure.NotNull(featureId);

        if (!IsValid(featureId))
        {
            throw new InvalidFeatureIdException(featureId);
        }
    }

    // \z instead of $, because $ also matches before a trailing line break.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex FeatureIdRegex();
}
