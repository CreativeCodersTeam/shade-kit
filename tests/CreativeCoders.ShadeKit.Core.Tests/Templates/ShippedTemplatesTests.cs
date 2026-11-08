using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Templates;

namespace CreativeCoders.ShadeKit.Core.Tests.Templates;

public class ShippedTemplatesTests
{
    public static TheoryData<string, string[]> DefaultChecklists { get; } = new()
    {
        {
            "checks/definition-of-ready.template.md",
            [
                "DOR-01 Every requirement in `spec.md` has an ID (`REQ-nnn`) and at least one acceptance criterion.",
                "DOR-02 Every acceptance criterion can be decided by an automated test, or is marked \"(manual)\" " +
                "with a reason.",
                "DOR-03 Every requirement has a priority: must, should, or could.",
                "DOR-04 `spec.md` has no open questions: \"Open questions\" says \"None.\" and no `(open: Q-nnn)` " +
                "marker is left.",
                "DOR-05 `spec.md` has no template leftovers: no `{{placeholder}}` and no guidance comment.",
                "DOR-06 \"Out of scope\" is stated, also when nothing is excluded.",
                "DOR-07 Every assumption is confirmed by a human or marked as a risk.",
                "DOR-08 Every edge case has an acceptance criterion or is listed in \"Out of scope\".",
                "DOR-09 \"Security considerations\" lists the findings, or says \"None identified.\" with a reason.",
                "DOR-10 The affected areas are known: `map` is done, was skipped on purpose with a recorded reason, " +
                "or `diagnosis.md` lists the affected code."
            ]
        },
        {
            "checks/definition-of-done.template.md",
            [
                "DOD-01 Build, test, and lint pass with the quality commands recorded in `review.md`, as recorded in " +
                "`verification.md`.",
                "DOD-02 Coverage was measured and recorded in `verification.md`. If a coverage threshold is set " +
                "(`coverageThreshold`, shown by `shade-kit gate info`), the value reaches it.",
                "DOD-03 Every acceptance criterion is covered by a passing test, or is marked manual and has an item " +
                "in `playbook.md`.",
                "DOD-04 Every task in `tasks.md` is done, or has a \"Not done\" reason that the human accepted.",
                "DOD-05 `review.md` has no open blocker.",
                "DOD-06 The quality bar was not lowered: no new analyzer or lint suppressions, skipped or deleted " +
                "tests, or weakened assertions without a reason accepted in `review.md`.",
                "DOD-07 Every deviation from the plan is recorded in `plan.md` with a reason, and every migration or " +
                "rollout step is done or handed over.",
                "DOD-08 The documentation matches the change (for example README, changelog, API docs), or Evidence " +
                "states why no update is needed.",
                "DOD-09 Every item in `playbook.md` is checked off by a human."
            ]
        }
    };

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
    public void All_EveryChecklistOfDefaultGates_HasShippedTemplateAtItsTemplatePath()
    {
        // Arrange
        var checklists = GatesConfig.CreateDefault().Profiles!.Values
            .SelectMany(gates => gates.Values)
            .Select(gate => gate.Checklist)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Act
        var kitPaths = ShippedTemplates.All.Select(template => template.KitPath).ToList();

        // Assert
        checklists
            .Should().NotBeEmpty();

        checklists
            .Should().AllSatisfy(checklist => kitPaths
                .Should().Contain(KitStorePaths.GetTemplatePath(checklist)));
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

    [Theory]
    [Trait("Requirement", "FP05-AC9")]
    [MemberData(nameof(DefaultChecklists))]
    public void ReadContent_ChecklistTemplate_ContainsDefaultItemsPendingAndCheckRunsWithoutRunLine(
        string resourcePath, string[] expectedItems)
    {
        // Arrange
        var template = ShippedTemplates.All.Single(x => x.ResourcePath == resourcePath);

        // Act
        var content = new ShippedTemplateReader().ReadContent(template);

        // Assert
        var lines = content.Split('\n');

        lines
            .Should().Contain("| ID | Criterion | Result | Evidence |");

        var items = lines
            .Where(line => line.StartsWith("| DO", StringComparison.Ordinal))
            .Select(line => line.Split('|').Select(cell => cell.Trim()).ToArray())
            .ToArray();

        items.Select(cells => $"{cells[1]} {cells[2]}")
            .Should().Equal(expectedItems);

        items
            .Should().AllSatisfy(cells =>
            {
                cells.Should().HaveCount(6, "a row has four cells between its outer pipes");
                cells[3].Should().Be("pending", cells[1]);
                cells[4].Should().BeEmpty(cells[1]);
            });

        const string checkRunsHeading = "\n## Check runs\n";

        content
            .Should().Contain(checkRunsHeading);

        var checkRuns = content[(content.IndexOf(checkRunsHeading, StringComparison.Ordinal) +
                                 checkRunsHeading.Length)..].Trim();

        checkRuns
            .Should().StartWith("<!--", "the section holds only the guidance comment")
            .And.EndWith("-->", "the section holds only the guidance comment");

        checkRuns.IndexOf("-->", StringComparison.Ordinal)
            .Should().Be(checkRuns.Length - 3, "the section holds no run line after the guidance comment");
    }

    [Fact]
    public void All_Catalog_ContainsNoRequestTemplate()
    {
        // Act
        var catalog = ShippedTemplates.All;

        // Assert
        catalog
            .Should().NotContain(x => x.ResourcePath == "request.template.md"
                                      || x.RepositoryPath == ".shadekit/templates/request.md");
    }
}
