using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Provides the catalog of templates that ship with SHADE kit.
/// </summary>
/// <remarks>
/// An explicit list with exactly one entry per embedded template resource.
/// </remarks>
public static class ShippedTemplates
{
    /// <summary>
    /// Gets every template that ships with SHADE kit.
    /// </summary>
    /// <value>The shipped templates, each with its source path and its path in the SHADE kit root.</value>
    public static IReadOnlyList<ShippedTemplate> All { get; } =
    [
        new ShippedTemplate("discussion.template.md", KitStorePaths.GetTemplatePath("discussion.md")),
        new ShippedTemplate("spec.template.md", KitStorePaths.GetTemplatePath("spec.md")),
        new ShippedTemplate("researches.template.md", KitStorePaths.GetTemplatePath("researches.md")),
        new ShippedTemplate("plan.template.md", KitStorePaths.GetTemplatePath("plan.md")),
        new ShippedTemplate("tasks.template.md", KitStorePaths.GetTemplatePath("tasks.md")),
        new ShippedTemplate("review.template.md", KitStorePaths.GetTemplatePath("review.md")),
        new ShippedTemplate("verification.template.md", KitStorePaths.GetTemplatePath("verification.md")),
        new ShippedTemplate("playbook.template.md", KitStorePaths.GetTemplatePath("playbook.md")),
        new ShippedTemplate("report.template.md", KitStorePaths.GetTemplatePath("report.md")),
        new ShippedTemplate("diagnosis.template.md", KitStorePaths.GetTemplatePath("diagnosis.md")),
        new ShippedTemplate("checks/definition-of-ready.template.md",
            KitStorePaths.GetTemplatePath(GatesConfig.DefinitionOfReadyChecklist)),
        new ShippedTemplate("checks/definition-of-done.template.md",
            KitStorePaths.GetTemplatePath(GatesConfig.DefinitionOfDoneChecklist))
    ];
}
