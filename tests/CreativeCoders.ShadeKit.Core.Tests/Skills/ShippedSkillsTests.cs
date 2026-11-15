using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

public partial class ShippedSkillsTests
{
    private const string SkillFileName = ShippedSkillFiles.SkillFileName;

    [Fact]
    [Trait("Requirement", "FP07-AC7")]
    public void SkillFiles_ShippedSkills_ContainNoPathReferenceIntoDocsOrResearch()
    {
        // Arrange
        var repositoryRoot = ShippedSkillFiles.FindRepositoryRoot();

        // Act
        var skillFiles = ShippedSkillFiles.EnumerateSkillFiles(repositoryRoot);
        var violations = FindDocsOrResearchReferences(repositoryRoot);

        // Assert
        skillFiles
            .Should().NotBeEmpty("the repository ships its skills under skills/");

        violations
            .Should().BeEmpty("no shipped file under skills/ may reference a path under docs/ or research/");
    }

    [Fact]
    [Trait("Requirement", "FP07-AC7")]
    public void FindDocsOrResearchReferences_NestedSkillsWithReferences_ReportsFileAndLineOfEachReference()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("See docs/specs/x.md.\nClean line.\nAlso research/notes.md", "skills", "a", SkillFileName);
        root.WriteFile("Clean line.\n", "skills", "b", SkillFileName);
        root.WriteFile("Text\n\nPath ../docs/plans/y.md", "skills", "group", "c", SkillFileName);

        // Act
        var violations = FindDocsOrResearchReferences(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("skills", "a", SkillFileName)}:1: See docs/specs/x.md.",
                $"{Path.Combine("skills", "a", SkillFileName)}:3: Also research/notes.md",
                $"{Path.Combine("skills", "group", "c", SkillFileName)}:3: Path ../docs/plans/y.md");
    }

    [Fact]
    [Trait("Requirement", "FP07-AC7")]
    public void FindDocsOrResearchReferences_BundledFilesBesideSkillFiles_ReportsReferencesInEveryTextFile()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("Clean line.\n", "skills", "a", SkillFileName);
        root.WriteFile("Intro\r\nSee research/notes.md", "skills", "a", "references", "guide.md");
        root.WriteFile("{ \"path\": \"docs/x.md\" }", "skills", "a", "assets", "config.json");
        root.WriteFile("first\rdocs/y.txt", "skills", "b", "notes");
        root.WriteFile("docs/ at the skills root", "skills", "README.txt");

        // Act
        var violations = FindDocsOrResearchReferences(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("skills", "README.txt")}:1: docs/ at the skills root",
                $"{Path.Combine("skills", "a", "assets", "config.json")}:1: {{ \"path\": \"docs/x.md\" }}",
                $"{Path.Combine("skills", "a", "references", "guide.md")}:2: See research/notes.md",
                $"{Path.Combine("skills", "b", "notes")}:2: docs/y.txt");
    }

    [Fact]
    public void FindDocsOrResearchReferences_BinaryFileWithReference_IsNotScanned()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("Clean line.\n", "skills", "a", SkillFileName);
        root.WriteFileBytes([.. "docs/x.md"u8, 0, .. "research/y.md"u8], "skills", "a", "assets", "image.png");

        // Act
        var violations = FindDocsOrResearchReferences(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(7999, false)]
    [InlineData(8000, true)]
    public void FindDocsOrResearchReferences_NulByteAtProbeBoundary_ScansOnlyFilesWithoutNulInFirst8000Bytes(
        int nulIndex, bool reported)
    {
        // Arrange
        using var root = new TempKitDirectory();
        var content = Enumerable.Repeat((byte)'x', 9000).ToArray();
        content[nulIndex] = 0;
        "\ndocs/x.md"u8.CopyTo(content.AsSpan(8500));
        root.WriteFileBytes(content, "skills", "a", "asset.bin");

        // Act
        var violations = FindDocsOrResearchReferences(root.RootPath);

        // Assert
        violations
            .Should().HaveCount(reported ? 1 : 0);
    }

    [Fact]
    public void FindDocsOrResearchReferences_TextFileThatIsNotUtf8_ThrowsInvalidDataException()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFileBytes([0x64, 0x6F, 0x63, 0xFC], "skills", "a", "latin1.txt");

        // Act
        var act = () => FindDocsOrResearchReferences(root.RootPath);

        // Assert
        act
            .Should().Throw<InvalidDataException>()
            .WithMessage($"*{Path.Combine("skills", "a", "latin1.txt")}*not valid UTF-8*");
    }

    [Fact]
    public void FindDocsOrResearchReferences_LineWithSeveralReferences_ReportsLineOnce()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("docs/a.md and research/b.md", "skills", "a", SkillFileName);

        // Act
        var violations = FindDocsOrResearchReferences(root.RootPath);

        // Assert
        violations
            .Should().Equal($"{Path.Combine("skills", "a", SkillFileName)}:1: docs/a.md and research/b.md");
    }

    [Theory]
    [InlineData("<!-- Source: docs/feature-packages/09-discuss-and-intake.md. -->")]
    [InlineData("See research/shade-kit-skills.md.")]
    [InlineData("Read `docs/specs/x.md` first.")]
    [InlineData("Path ../../docs/plans/x.md")]
    [InlineData("./research/notes.md")]
    [InlineData("docs/")]
    [InlineData("[spec](docs/specs/x.md)")]
    [InlineData("\"research/x.md\"")]
    [InlineData("<docs/x.md>")]
    [InlineData(@"docs\specs\x.md")]
    [InlineData(@"..\..\research\notes.md")]
    [InlineData("Docs/specs/x.md")]
    [InlineData("RESEARCH/notes.md")]
    [InlineData(".shadekit/research/x.md")]
    [InlineData("https://example.com/docs/x")]
    public void ContainsDocsOrResearchReference_LineWithDocsOrResearchPathSegment_ReturnsTrue(string line)
    {
        // Act
        var result = ContainsDocsOrResearchReference(line);

        // Assert
        result
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Load `shade-kit-research` for the research step.")]
    [InlineData("skills/shade-kit-research/SKILL.md")]
    [InlineData("Write the docs and the research notes.")]
    [InlineData("mydocs/readme.md")]
    [InlineData("my_docs/readme.md")]
    [InlineData(".docs/readme.md")]
    [InlineData("researches.md")]
    [InlineData("docs.md")]
    [InlineData("research-notes/x.md")]
    [InlineData("docsets/x.md")]
    public void ContainsDocsOrResearchReference_LineWithoutDocsOrResearchPathSegment_ReturnsFalse(string line)
    {
        // Act
        var result = ContainsDocsOrResearchReference(line);

        // Assert
        result
            .Should().BeFalse();
    }

    private static string[] FindDocsOrResearchReferences(string repositoryRoot)
    {
        return ShippedSkillFiles.ReadTextFiles(repositoryRoot)
            .SelectMany(file => file.Lines
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(x => ContainsDocsOrResearchReference(x.Line))
                .Select(x => $"{Path.GetRelativePath(repositoryRoot, file.File)}:{x.Number}: {x.Line}"))
            .ToArray();
    }

    private static bool ContainsDocsOrResearchReference(string line)
    {
        return DocsOrResearchPathRegex().IsMatch(line);
    }

    // A path segment "docs" or "research" in any letter case, followed by a slash or backslash. It must not be
    // preceded by a name character, so that "shade-kit-research/", "mydocs/", or ".docs/" do not match. A nested
    // segment (".shadekit/research/") matches on purpose: the guard errs on the side of flagging.
    [GeneratedRegex(@"(?<![\w.-])(?:docs|research)[/\\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex DocsOrResearchPathRegex();
}
