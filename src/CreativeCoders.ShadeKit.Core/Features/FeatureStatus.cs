namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Specifies whether work on a feature is still in progress.
/// </summary>
public enum FeatureStatus
{
    /// <summary>
    /// The feature is being worked on. Stored as <c>active</c>.
    /// </summary>
    Active,

    /// <summary>
    /// The feature is finished. Stored as <c>done</c>.
    /// </summary>
    Done
}
