namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Represents a template that ships with SHADE kit and is copied into a repository by <c>shade-kit init</c>.
/// </summary>
/// <param name="ResourcePath">
/// The path of the template source relative to the shipped template folder, with <c>/</c> as separator, for example
/// <c>checks/definition-of-ready.template.md</c>.
/// </param>
/// <param name="RepositoryPath">
/// The path the template is written to, relative to the repository root, with <c>/</c> as separator, for example
/// <c>.shadekit/templates/checks/definition-of-ready.md</c>.
/// </param>
/// <param name="OwnerPackage">
/// The number of the feature package that owns the content of the template, for example <c>05</c>.
/// </param>
public sealed record ShippedTemplate(string ResourcePath, string RepositoryPath, string OwnerPackage);
