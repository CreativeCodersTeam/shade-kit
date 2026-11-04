namespace CreativeCoders.ShadeKit.Core.Gates.Exceptions;

/// <summary>
/// Represents the exception that is thrown when the template of a checklist that a gate names does not exist, so the
/// checklist cannot be copied into the feature folder.
/// </summary>
/// <param name="templatePath">
/// The repository-relative path of the missing template, for example
/// <c>.shadekit/templates/checks/definition-of-ready.md</c>.
/// </param>
public class ChecklistTemplateNotFoundException(string templatePath)
    : Exception($"The checklist template '{templatePath}' does not exist.")
{
    /// <summary>
    /// Gets the path of the missing checklist template.
    /// </summary>
    /// <value>
    /// The repository-relative path with <c>/</c> as separator, for example
    /// <c>.shadekit/templates/checks/definition-of-ready.md</c>.
    /// </value>
    public string TemplatePath { get; } = templatePath;
}
