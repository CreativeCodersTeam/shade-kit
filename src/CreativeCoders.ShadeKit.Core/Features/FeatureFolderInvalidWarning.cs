namespace CreativeCoders.ShadeKit.Core.Features;

/// <summary>
/// Represents a warning that a folder in the <c>features</c> directory is not named with a valid feature id and was
/// skipped.
/// </summary>
/// <param name="FolderName">The name of the folder, as it was found.</param>
public sealed record FeatureFolderInvalidWarning(string FolderName) : KitWarning;
