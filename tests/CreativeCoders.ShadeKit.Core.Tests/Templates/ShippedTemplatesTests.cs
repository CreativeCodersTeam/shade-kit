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
        (string ResourcePath, string RepositoryPath)[] templateTable =
        [
            ("request.template.md", ".shadekit/templates/request.md"),
            ("discussion.template.md", ".shadekit/templates/discussion.md"),
            ("spec.template.md", ".shadekit/templates/spec.md"),
            ("researches.template.md", ".shadekit/templates/researches.md"),
            ("plan.template.md", ".shadekit/templates/plan.md"),
            ("tasks.template.md", ".shadekit/templates/tasks.md"),
            ("review.template.md", ".shadekit/templates/review.md"),
            ("verification.template.md", ".shadekit/templates/verification.md"),
            ("playbook.template.md", ".shadekit/templates/playbook.md"),
            ("report.template.md", ".shadekit/templates/report.md"),
            ("diagnosis.template.md", ".shadekit/templates/diagnosis.md"),
            ("checks/definition-of-ready.template.md", ".shadekit/templates/checks/definition-of-ready.md"),
            ("checks/definition-of-done.template.md", ".shadekit/templates/checks/definition-of-done.md")
        ];

        // Act
        var catalog = ShippedTemplates.All.Select(x => (x.ResourcePath, x.RepositoryPath));

        // Assert
        catalog
            .Should().Equal(templateTable);
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
    public void All_KitPaths_AreTheRepositoryPathsRelativeToTheKitRoot()
    {
        // Act
        var templates = ShippedTemplates.All;

        // Assert
        templates
            .Should().AllSatisfy(template => template.RepositoryPath
                .Should().Be(".shadekit/" + template.KitPath));
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
