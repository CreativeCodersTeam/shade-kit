namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents one feature in the result of <see cref="IFeatureStore.ListAsync" />.
/// </summary>
/// <param name="Id">The id of the feature, which is the name of its folder.</param>
/// <param name="Title">The title of the feature from its <c>feature.json</c>.</param>
/// <param name="Profile">The workflow profile of the feature from its <c>feature.json</c>.</param>
/// <param name="Status">One of the enumeration values that specifies the status of the feature.</param>
public sealed record FeatureSummary(string Id, string Title, string Profile, FeatureStatus Status);
