using CreativeCoders.ShadeKit.Core.Files.Warnings;
using CreativeCoders.ShadeKit.Core.Warnings;

namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the result of <see cref="IFeatureStore.SetCurrentAsync" />.
/// </summary>
/// <param name="FeatureId">The id of the feature that the local override now selects.</param>
/// <param name="Warnings">
/// The warnings about the current branch, which wins over the local override when it selects another feature,
/// followed by the warnings of writing the override, for example an
/// <see cref="EditorConfigUnreadableWarning" />; each warning only once.
/// </param>
public sealed record SetCurrentFeatureResult(string FeatureId, IReadOnlyList<KitWarning> Warnings);
