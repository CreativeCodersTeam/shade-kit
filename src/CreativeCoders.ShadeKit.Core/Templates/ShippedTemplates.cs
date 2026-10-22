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
    /// Gets the template of the <c>request.md</c> of a new feature, which is also part of <see cref="All" />.
    /// </summary>
    /// <value>The shipped template <c>request.template.md</c>, written to <c>templates/request.md</c>.</value>
    // Declared before All, because static initializers run in the order of declaration.
    public static ShippedTemplate Request { get; } = new("request.template.md",
        TemplatesDir + KitStorePaths.RequestFileName);

    /// <summary>
    /// Gets every template that ships with SHADE kit.
    /// </summary>
    /// <value>The shipped templates, each with its source path and its path in the SHADE kit root.</value>
    public static IReadOnlyList<ShippedTemplate> All { get; } =
    [
        Request,
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
