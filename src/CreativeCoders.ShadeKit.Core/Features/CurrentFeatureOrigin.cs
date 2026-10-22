namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Specifies where the current feature of a <see cref="CurrentFeatureResolution" /> comes from.
/// </summary>
public enum CurrentFeatureOrigin
{
    /// <summary>
    /// The current feature is selected by the name of the current git branch.
    /// </summary>
    Branch,

    /// <summary>
    /// The current feature is selected by the local override <c>.shadekit/features/current-feature.json</c>.
    /// </summary>
    Override
}
