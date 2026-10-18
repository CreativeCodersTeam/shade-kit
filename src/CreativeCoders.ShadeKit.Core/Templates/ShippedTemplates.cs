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
    /// <summary>
    /// Gets every template that ships with SHADE kit.
    /// </summary>
    /// <value>The shipped templates, each with its source path, its repository path, and its owner package.</value>
    public static IReadOnlyList<ShippedTemplate> All { get; } =
    [
        new("request.template.md", ".shadekit/templates/request.md", "09"),
        new("discussion.template.md", ".shadekit/templates/discussion.md", "09"),
        new("spec.template.md", ".shadekit/templates/spec.md", "10"),
        new("researches.template.md", ".shadekit/templates/researches.md", "06"),
        new("plan.template.md", ".shadekit/templates/plan.md", "12"),
        new("tasks.template.md", ".shadekit/templates/tasks.md", "12"),
        new("review.template.md", ".shadekit/templates/review.md", "13"),
        new("verification.template.md", ".shadekit/templates/verification.md", "14"),
        new("playbook.template.md", ".shadekit/templates/playbook.md", "14"),
        new("report.template.md", ".shadekit/templates/report.md", "15"),
        new("diagnosis.template.md", ".shadekit/templates/diagnosis.md", "16"),
        new("checks/definition-of-ready.template.md", ".shadekit/templates/checks/definition-of-ready.md", "05"),
        new("checks/definition-of-done.template.md", ".shadekit/templates/checks/definition-of-done.md", "05")
    ];
}
