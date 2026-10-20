namespace CreativeCoders.ShadeKit.Core.Init;

/// <summary>
/// Represents the exception that is thrown when a repository is initialised although a <c>.shadekit</c> directory
/// already exists at its root.
/// </summary>
/// <param name="kitRootPath">The path of the existing <c>.shadekit</c> directory.</param>
public class KitAlreadyInitializedException(string kitRootPath)
    : Exception($"The repository is already initialised: '{kitRootPath}' exists.")
{
    /// <summary>
    /// Gets the path of the existing SHADE kit root.
    /// </summary>
    /// <value>The full path of the <c>.shadekit</c> directory at the repository root.</value>
    public string KitRootPath { get; } = kitRootPath;
}
