using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Templates;

namespace CreativeCoders.ShadeKit.Core.Tests.Templates;

public class ShippedTemplatesTests
{
    [Fact]
    public void All_EmbeddedTemplateResources_MatchCatalogOneToOne()
    {
        // Arrange
        var embeddedTemplateResources = typeof(ShippedTemplate).Assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ShippedTemplateReader.ResourceNamePrefix, StringComparison.Ordinal))
            .ToArray();

        // Act
        var catalogResources = ShippedTemplates.All
            .Select(ShippedTemplateReader.GetResourceName)
            .ToArray();

        // Assert
        catalogResources
            .Should().OnlyHaveUniqueItems()
            .And.BeEquivalentTo(embeddedTemplateResources);
    }

    [Fact]
    public void All_Catalog_MatchesTemplateTable()
    {
        // Arrange
        ShippedTemplate[] templateTable =
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

        // Act
        var catalog = ShippedTemplates.All;

        // Assert
        catalog
            .Should().BeEquivalentTo(templateTable);
    }

    [Theory]
    [InlineData("spec.template.md", ".shadekit/templates/spec.md")]
    [InlineData("checks/definition-of-ready.template.md", ".shadekit/templates/checks/definition-of-ready.md")]
    public void All_Fp02Ac8Examples_MapToRepositoryPathFromTemplateTable(string resourcePath, string repositoryPath)
    {
        // Act
        var template = ShippedTemplates.All.Single(x => x.ResourcePath == resourcePath);

        // Assert
        template.RepositoryPath
            .Should().Be(repositoryPath);
    }

    [Fact]
    public void All_RepositoryPaths_AreUniqueIgnoringCase()
    {
        // Act
        var repositoryPaths = ShippedTemplates.All.Select(x => x.RepositoryPath.ToUpperInvariant());

        // Assert
        repositoryPaths
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void All_RepositoryPaths_AreRelativeUnderShadekitTemplatesWithForwardSlashes()
    {
        // Act
        var repositoryPaths = ShippedTemplates.All.Select(x => x.RepositoryPath);

        // Assert
        repositoryPaths
            .Should().AllSatisfy(path => path
                .Should().StartWith(".shadekit/templates/")
                .And.EndWith(".md")
                .And.NotContain("\\")
                .And.NotContain(".template."));
    }
}
