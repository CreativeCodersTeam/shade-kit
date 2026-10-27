namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents one feature in the result of <see cref="IFeatureStore.ListAsync" />.
/// </summary>
/// <param name="Id">The id of the feature, which is the name of its folder.</param>
/// <param name="Title">The title of the feature from its <c>feature.json</c>.</param>
/// <param name="Profile">The workflow profile of the feature from its <c>feature.json</c>.</param>
/// <param name="Status">One of the enumeration values that specifies the status of the feature.</param>
/// <param name="CurrentPhase">
/// The current phase of the feature, normalized against its workflow profile: the first phase of the profile for a
/// feature whose <c>feature.json</c> has no phase fields yet.
/// </param>
public sealed record FeatureSummary(string Id, string Title, string Profile, FeatureStatus Status,
    string CurrentPhase);
