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
    private const string TemplatesDir = KitStorePaths.TemplatesDirName + "/";

    /// <summary>
    /// Gets every template that ships with SHADE kit.
    /// </summary>
    /// <value>The shipped templates, each with its source path and its path in the SHADE kit root.</value>
    public static IReadOnlyList<ShippedTemplate> All { get; } =
    [
        new ShippedTemplate("discussion.template.md", TemplatesDir + "discussion.md"),
        new ShippedTemplate("spec.template.md", TemplatesDir + "spec.md"),
        new ShippedTemplate("researches.template.md", TemplatesDir + "researches.md"),
        new ShippedTemplate("plan.template.md", TemplatesDir + "plan.md"),
        new ShippedTemplate("tasks.template.md", TemplatesDir + "tasks.md"),
        new ShippedTemplate("review.template.md", TemplatesDir + "review.md"),
        new ShippedTemplate("verification.template.md", TemplatesDir + "verification.md"),
        new ShippedTemplate("playbook.template.md", TemplatesDir + "playbook.md"),
        new ShippedTemplate("report.template.md", TemplatesDir + "report.md"),
        new ShippedTemplate("diagnosis.template.md", TemplatesDir + "diagnosis.md"),
        new ShippedTemplate("checks/definition-of-ready.template.md", TemplatesDir + "checks/definition-of-ready.md"),
        new ShippedTemplate("checks/definition-of-done.template.md", TemplatesDir + "checks/definition-of-done.md")
    ];
}
