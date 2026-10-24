using System.Text.RegularExpressions;
using CreativeCoders.Core;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Provides the rules for valid feature ids.
/// </summary>
/// <remarks>
/// A valid id is groups of lowercase ASCII letters and digits joined by single hyphens, for example
/// <c>gh-142-login-sso</c>, and therefore safe as a directory name.
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
