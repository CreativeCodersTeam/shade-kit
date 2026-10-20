using CreativeCoders.ShadeKit.Core.Store;

namespace CreativeCoders.ShadeKit.Core.Templates;

/// <summary>
/// Provides the catalog of templates that ship with SHADE kit.
/// </summary>
/// <remarks>
/// The catalog is an explicit list: repository paths are not derived from the names of the template sources. Every
/// entry has exactly one embedded template resource in the SHADE kit core assembly, and every embedded template
/// resource has exactly one entry.
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
        new("request.template.md", TemplatesDir + "request.md"),
        new("discussion.template.md", TemplatesDir + "discussion.md"),
        new("spec.template.md", TemplatesDir + "spec.md"),
        new("researches.template.md", TemplatesDir + "researches.md"),
        new("plan.template.md", TemplatesDir + "plan.md"),
        new("tasks.template.md", TemplatesDir + "tasks.md"),
        new("review.template.md", TemplatesDir + "review.md"),
        new("verification.template.md", TemplatesDir + "verification.md"),
        new("playbook.template.md", TemplatesDir + "playbook.md"),
        new("report.template.md", TemplatesDir + "report.md"),
        new("diagnosis.template.md", TemplatesDir + "diagnosis.md"),
        new("checks/definition-of-ready.template.md", TemplatesDir + "checks/definition-of-ready.md"),
        new("checks/definition-of-done.template.md", TemplatesDir + "checks/definition-of-done.md")
    ];
}
