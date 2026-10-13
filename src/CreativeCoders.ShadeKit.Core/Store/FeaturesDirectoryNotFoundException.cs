namespace CreativeCoders.ShadeKit.Core.Store;

/// <summary>
/// Represents the exception that is thrown when the SHADE kit store contains no <c>features</c> directory.
/// </summary>
/// <param name="featuresPath">The path of the <c>features</c> directory that was not found.</param>
public class FeaturesDirectoryNotFoundException(string featuresPath)
    : Exception($"The features directory '{featuresPath}' was not found.")
{
    /// <summary>
    /// Gets the path of the <c>features</c> directory that was not found.
    /// </summary>
    /// <value>The full path of the missing <c>features</c> directory.</value>
    public string FeaturesPath { get; } = featuresPath;
}
