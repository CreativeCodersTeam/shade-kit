using System.Diagnostics.CodeAnalysis;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of resolving the current feature with an <see cref="ICurrentFeatureResolver" />.
/// </summary>
/// <remarks>
/// A current feature that cannot be resolved is a result, not an error: <paramref name="FeatureId" /> and
/// <paramref name="Origin" /> are both <see langword="null" />, and the agent asks the human for the feature.
/// </remarks>
/// <param name="FeatureId">
/// The id of the current feature, whose folder exists; or <see langword="null" /> if no current feature is resolved.
/// </param>
/// <param name="Origin">
/// One of the enumeration values that specifies where <paramref name="FeatureId" /> comes from; or
/// <see langword="null" /> if no current feature is resolved.
/// </param>
/// <param name="Warnings">
/// The format warnings from reading the local override first, then the warnings about the current branch and the
/// local override.
/// </param>
public sealed record CurrentFeatureResolution(
    string? FeatureId,
    CurrentFeatureOrigin? Origin,
    IReadOnlyList<KitWarning> Warnings)
{
    /// <summary>
    /// Gets a value that indicates whether a current feature is resolved.
    /// </summary>
    /// <value>
    /// <see langword="true" /> if <see cref="FeatureId" /> is not <see langword="null" />; otherwise,
    /// <see langword="false" />.
    /// </value>
    [MemberNotNullWhen(true, nameof(FeatureId))]
    public bool IsResolved => FeatureId is not null;
}
