namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents the input of <see cref="IFeatureStore.CreateAsync" />, the options of <c>shade-kit feature new</c>.
/// </summary>
/// <param name="Source">
/// The source of the feature: an issue URL, the path of an existing UTF-8 text file, or the request text.
/// </param>
/// <param name="Title">
/// The title of the feature; or <see langword="null" /> to take it from the source, which a URL whose host is not a
/// tracker in <c>config.json</c> does not allow.
/// </param>
/// <param name="Profile">
/// The workflow profile of the feature; or <see langword="null" /> to use <c>defaultProfile</c> of <c>config.json</c>.
/// </param>
/// <param name="WorkingDirectory">The directory against which a relative file path in the source is resolved.</param>
public sealed record NewFeatureRequest(string Source, string? Title, string? Profile, string WorkingDirectory);
