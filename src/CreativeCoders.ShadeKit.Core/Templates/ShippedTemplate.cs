using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Represents a template that ships with SHADE kit and is copied into a repository by <c>shade-kit init</c>.
/// </summary>
/// <param name="ResourcePath">
/// The path of the template source relative to the shipped template folder, with <c>/</c> as separator, for example
/// <c>checks/definition-of-ready.template.md</c>.
/// </param>
/// <param name="KitPath">
/// The path the template is written to, relative to the SHADE kit root directory, with <c>/</c> as separator, for
/// example <c>templates/checks/definition-of-ready.md</c>.
/// </param>
public sealed record ShippedTemplate(string ResourcePath, string KitPath)
{
    /// <summary>
    /// Gets the path the template is written to, relative to the repository root.
    /// </summary>
    /// <value>
    /// The <see cref="KitPath" /> prefixed with the SHADE kit root directory, for example
    /// <c>.shadekit/templates/checks/definition-of-ready.md</c>.
    /// </value>
    public string RepositoryPath => KitStorePaths.GetRepositoryPath(KitPath);
}
