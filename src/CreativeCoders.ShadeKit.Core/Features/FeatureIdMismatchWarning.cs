namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents a warning that the <c>feature.json</c> of a feature records an <c>id</c> other than the name of its
/// folder; the folder name is used and the file is left unchanged.
/// </summary>
/// <param name="FolderFeatureId">The name of the feature folder, which is used as the id of the feature.</param>
/// <param name="FileFeatureId">The id recorded in the <c>feature.json</c> of the feature.</param>
public sealed record FeatureIdMismatchWarning(string FolderFeatureId, string FileFeatureId) : KitWarning;
