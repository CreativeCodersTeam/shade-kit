using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Engine;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

public partial class SkillKitVersionTests
{
    private const string SkillFileName = ShippedSkillFiles.SkillFileName;

    private const string FrontmatterDelimiter = "---";

    private static readonly SemanticVersion DefaultEngineVersion = SemanticVersion.Parse("0.8.3");

    [Fact]
    [Trait("Requirement", "FP07-AC2")]
    public void SkillFiles_ShippedSkills_CarryOneKitVersionThatMatchesTheEngineMajorMinor()
    {
        // Arrange
        var repositoryRoot = ShippedSkillFiles.FindRepositoryRoot();
        var engineVersionText = new EngineVersionProvider().Version;

        // Act
        var engineVersionParsed = SemanticVersion.TryParse(engineVersionText, out var engineVersion);

        // Assert
        engineVersionParsed
            .Should().BeTrue($"the engine version '{engineVersionText}' of the build must be a SemVer version");

        var violations = FindKitVersionViolations(repositoryRoot, engineVersion!);

        violations
            .Should().BeEmpty(
                "every skill carries one kit version x.y.z with the major.minor of the engine {0}, and every "
                + "handshake text names that version; violations:{1}{2}",
                engineVersionText, Environment.NewLine, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FindKitVersionViolations_SkillsWithOneVersionAndMatchingHandshakes_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a",
            "1. Version handshake: run `shade-kit version --json --expect 0.8.0`; 0.8.0 is the kit version of this "
            + "skill (`metadata.version` in its frontmatter).");
        WriteSkill(root, "0.8.0", "b", "No handshake here.");
        root.WriteFile("Run `shade-kit version --json --expect 0.8.0` first.", "skills", "b", "references", "x.md");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindKitVersionViolations_SkillWithOtherVersion_ReportsFileLineAndExpectedAndFoundVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Body");
        WriteSkill(root, "0.8.1", "b", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("b")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("a")}), "
                + "found 0.8.1");
    }

    [Fact]
    public void FindKitVersionViolations_HandshakeTextsWithOtherVersion_ReportsEveryOccurrence()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a",
            "1. Version handshake: run `shade-kit version --json --expect 0.7.0`; 0.7.0 is the kit version of this "
            + "skill.\nThe only command is `shade-kit version --json --expect=0.9.0`.");
        root.WriteFile("Line\nRun `shade-kit version --expect 1.0.0 --json`.", "skills", "a", "references", "x.md");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:8: expected --expect 0.8.0, found --expect 0.7.0",
                $"{SkillPath("a")}:8: expected '0.8.0 is the kit version of this skill', "
                + "found '0.7.0 is the kit version of this skill'",
                $"{SkillPath("a")}:9: expected --expect 0.8.0, found --expect 0.9.0",
                $"{Path.Combine("skills", "a", "references", "x.md")}:2: expected --expect 0.8.0, "
                + "found --expect 1.0.0");
    }

    [Theory]
    [InlineData("Run `shade-kit version --json --expect \"0.7.0\"`.")]
    [InlineData("Run `shade-kit version --json --expect '0.7.0'`.")]
    [InlineData("Run shade-kit version --json --expect `0.7.0`.")]
    [InlineData("Run `shade-kit version --json --expect\t0.7.0`.")]
    [InlineData("Run shade-kit version --json --expect 0.7.0.")]
    public void FindKitVersionViolations_QuotedOrPunctuatedExpectValue_ReportsTheBareVersion(string body)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", body);

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"{SkillPath("a")}:8: expected --expect 0.8.0, found --expect 0.7.0");
    }

    [Theory]
    [InlineData("Run shade-kit version --json --expect 0.8.0.")]
    [InlineData("Run shade-kit version --json --expect 0.8.0: the handshake.")]
    [InlineData("**0.8.0** is the kit version of this skill.")]
    [InlineData("(0.8.0\tis the kit version of this skill)")]
    [InlineData("\"0.8.0\" is the kit version of this skill.")]
    public void FindKitVersionViolations_KitVersionWithPunctuationOrEmphasis_ReturnsNoViolation(string body)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", body);

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindKitVersionViolations_HandshakeWrappedAcrossLines_ReportsTheLineWhereTheMatchStarts()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a",
            "Version handshake: run `shade-kit version --json --expect\n0.7.0`; 0.7.0 is the kit\r\nversion of "
            + "this skill.\n**0.9.0**\nis the kit version of this skill.");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:8: expected --expect 0.8.0, found --expect 0.7.0",
                $"{SkillPath("a")}:9: expected '0.8.0 is the kit version of this skill', "
                + "found '0.7.0 is the kit\r\nversion of this skill'",
                $"{SkillPath("a")}:11: expected '0.8.0 is the kit version of this skill', "
                + "found '0.9.0**\nis the kit version of this skill'");
    }

    [Fact]
    public void FindKitVersionViolations_MatchingAndOtherExpectOnOneLine_ReportsOnlyTheOtherOne()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Run `--expect 0.8.0` or `--expect 0.7.1`, never `--expect 0.8.0`.");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"{SkillPath("a")}:8: expected --expect 0.8.0, found --expect 0.7.1");
    }

    [Fact]
    public void FindKitVersionViolations_PlaceholderAfterExpect_ReportsThePlaceholder()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Run `shade-kit version --json --expect <version>`.");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"{SkillPath("a")}:8: expected --expect 0.8.0, found --expect <version>");
    }

    [Fact]
    public void FindKitVersionViolations_LongBundledLineWithoutWhitespace_FinishesWithoutRegexTimeout()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Body");
        root.WriteFile(new string('a', 200_000) + " is the kit version of this skill", "skills", "a", "asset.svg");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().ContainSingle()
            .Which.Should().StartWith($"{Path.Combine("skills", "a", "asset.svg")}:1: expected '0.8.0 is the kit");
    }

    [Fact]
    public void FindKitVersionViolations_SkillWithOtherVersionAndOtherMajorMinor_ReportsBothForTheSkill()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Body");
        WriteSkill(root, "0.9.0", "b", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("b")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("a")}), "
                + "found 0.9.0",
                $"{SkillPath("b")}:5: expected the major.minor 0.8 of the engine version 0.8.3, found 0.9.0");
    }

    [Fact]
    public void FindKitVersionViolations_OutlierFirstInPathOrder_ReportsOnlyTheOutlier()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.1", "a", "Body");
        WriteSkill(root, "0.8.0", "b", "Run `shade-kit version --json --expect 0.8.0`.");
        WriteSkill(root, "0.8.0", "c", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("b")}), "
                + "found 0.8.1");
    }

    [Fact]
    public void FindKitVersionViolations_MoreFrequentVersionWithOtherMajorMinor_IsTheKitVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Body");
        WriteSkill(root, "0.9.0", "b", "Body");
        WriteSkill(root, "0.9.0", "c", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version 0.9.0 (the kit version of {SkillPath("b")}), "
                + "found 0.8.0",
                $"{SkillPath("b")}:5: expected the major.minor 0.8 of the engine version 0.8.3, found 0.9.0",
                $"{SkillPath("c")}:5: expected the major.minor 0.8 of the engine version 0.8.3, found 0.9.0");
    }

    [Fact]
    public void FindKitVersionViolations_TieWithOneVersionMatchingEngineMajorMinor_TakesThatVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.9.0", "a", "Body");
        WriteSkill(root, "0.9.0", "b", "Body");
        WriteSkill(root, "0.8.0", "c", "Run `shade-kit version --json --expect 0.8.0`.");
        WriteSkill(root, "0.8.0", "d", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("c")}), "
                + "found 0.9.0",
                $"{SkillPath("a")}:5: expected the major.minor 0.8 of the engine version 0.8.3, found 0.9.0",
                $"{SkillPath("b")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("c")}), "
                + "found 0.9.0",
                $"{SkillPath("b")}:5: expected the major.minor 0.8 of the engine version 0.8.3, found 0.9.0");
    }

    [Fact]
    public void FindKitVersionViolations_TieOfInterleavedVersionsWithSameMajorMinor_TakesTheVersionOfTheFirstSkill()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.1", "a", "Body");
        WriteSkill(root, "0.8.0", "b", "Body");
        WriteSkill(root, "0.8.1", "c", "Body");
        WriteSkill(root, "0.8.0", "d", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("b")}:5: expected metadata.version 0.8.1 (the kit version of {SkillPath("a")}), "
                + "found 0.8.0",
                $"{SkillPath("d")}:5: expected metadata.version 0.8.1 (the kit version of {SkillPath("a")}), "
                + "found 0.8.0");
    }

    [Fact]
    public void FindKitVersionViolations_TieOfSingleSkillsWithEngineMatchLastInPathOrder_TakesTheMatchingVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.9.0", "a", "Body");
        WriteSkill(root, "0.7.0", "b", "Body");
        WriteSkill(root, "0.8.0", "c", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, SemanticVersion.Parse("0.8.3-feature.2"));

        // Assert
        violations
            .Where(x => x.Contains(": expected metadata.version ", StringComparison.Ordinal))
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("c")}), "
                + "found 0.9.0",
                $"{SkillPath("b")}:5: expected metadata.version 0.8.0 (the kit version of {SkillPath("c")}), "
                + "found 0.7.0");
    }

    [Fact]
    public void FindKitVersionViolations_SeveralInvalidVersionsAndOneValid_TakesTheValidVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8", "a", "Body");
        WriteSkill(root, "0.8", "b", "Body");
        WriteSkill(root, "0.8.0", "c", "Run `shade-kit version --json --expect 0.8.0`.");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version as a plain x.y.z version, found '0.8'",
                $"{SkillPath("b")}:5: expected metadata.version as a plain x.y.z version, found '0.8'");
    }

    [Theory]
    [InlineData("0.8.1", "0.8.0")]
    [InlineData("0.9.0", "0.7.0")]
    public void FindKitVersionViolations_TieWithEqualEngineMatch_TakesTheVersionOfTheFirstSkillInPathOrder(
        string firstVersion, string secondVersion)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, firstVersion, "a", "Body");
        WriteSkill(root, secondVersion, "b", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Where(x => x.Contains(": expected metadata.version ", StringComparison.Ordinal))
            .Should().Equal(
                $"{SkillPath("b")}:5: expected metadata.version {firstVersion} "
                + $"(the kit version of {SkillPath("a")}), found {secondVersion}");
    }

    [Theory]
    [InlineData("0.7.0")]
    [InlineData("0.9.0")]
    [InlineData("1.8.0")]
    public void FindKitVersionViolations_KitVersionWithOtherMajorMinorThanEngine_ReportsEverySkill(
        string kitVersion)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, kitVersion, "a", "Body");
        WriteSkill(root, kitVersion, "b", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, SemanticVersion.Parse("0.8.3-feature.2"));

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected the major.minor 0.8 of the engine version 0.8.3-feature.2, "
                + $"found {kitVersion}",
                $"{SkillPath("b")}:5: expected the major.minor 0.8 of the engine version 0.8.3-feature.2, "
                + $"found {kitVersion}");
    }

    [Theory]
    [InlineData("0.8.7")]
    [InlineData("0.8.0")]
    public void FindKitVersionViolations_KitPatchDiffersFromEnginePatch_ReturnsNoViolation(string kitVersion)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, kitVersion, "a", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("0.8.0-beta.1")]
    [InlineData("0.8.0+build.5")]
    [InlineData("\"0.8.0\"")]
    [InlineData("'0.8.0'")]
    [InlineData("0.8")]
    [InlineData("v0.8.0")]
    [InlineData("08.0.0")]
    [InlineData("0.8.0 # kit version")]
    [InlineData("")]
    public void FindKitVersionViolations_VersionThatIsNotPlainSemVer_ReportsFileLineAndFoundValue(
        string version)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, version, "a", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version as a plain x.y.z version, found '{version}'");
    }

    [Fact]
    public void FindKitVersionViolations_InvalidVersionInFirstSkill_TakesKitVersionFromNextValidSkill()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8", "a", "Run `shade-kit version --json --expect 0.8.0`.");
        WriteSkill(root, "0.8.0", "b", "Body");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"{SkillPath("a")}:5: expected metadata.version as a plain x.y.z version, found '0.8'");
    }

    [Theory]
    [InlineData("")]
    [InlineData("---")]
    [InlineData(" ---\nname: a\n---\nBody")]
    [InlineData("# shade-kit-a\n\nBody")]
    [InlineData("\n---\nname: a\n---\nBody")]
    [InlineData("---\nname: a\nmetadata:\n  version: 0.8.0\n")]
    public void FindKitVersionViolations_SkillWithoutFrontmatter_ReportsMissingFrontmatter(string content)
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile(content, "skills", "a", SkillFileName);

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:1: expected a frontmatter between a first line '---' and a second line '---', "
                + "found none");
    }

    [Theory]
    [InlineData("---\nname: a\n---\nmetadata:\n  version: 0.8.0\n", 3)]
    [InlineData("---\nname: a\nversion: 0.8.0\n---\nBody", 4)]
    [InlineData("---\nname: a\nmetadata:\n  author: x\nversion: 0.8.0\n---\nBody", 6)]
    [InlineData("---\nname: a\nmetadata:\n  other:\n    version: 0.8.0\n---\nBody", 6)]
    [InlineData("---\nname: a\nmetadata: {version: 0.8.0}\n---\nBody", 4)]
    [InlineData("---\nname: a\nmetadata:\n  version:0.8.0\n---\nBody", 5)]
    [InlineData("---\nname: a\nmetadata:\n---\nBody", 4)]
    [InlineData("---\n---\nBody", 2)]
    [InlineData("--- \nname: a\n---\t\nBody", 3)]
    public void FindKitVersionViolations_FrontmatterWithoutMetadataVersion_ReportsClosingDelimiterLine(
        string content, int closingLine)
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile(content, "skills", "a", SkillFileName);

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:{closingLine}: expected metadata.version in the frontmatter, found none");
    }

    [Fact]
    public void FindKitVersionViolations_FrontmatterWithBomCrLfAndOtherMetadata_ReadsVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFileBytes(
            [
                0xEF, 0xBB, 0xBF,
                .. "---\r\nname: a\r\nmetadata:\r\n  author: x\r\n\r\n  version: 0.8.0   \r\n---\r\nBody\r\n"u8
            ],
            "skills", "a", SkillFileName);

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindKitVersionViolations_VersionOutsideFrontmatter_IsNotTakenAsKitVersion()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("---\nname: a\n---\nmetadata:\n  version: 0.8.0\n---\n", "skills", "a", SkillFileName);

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"{SkillPath("a")}:3: expected metadata.version in the frontmatter, found none");
    }

    [Fact]
    public void FindKitVersionViolations_SkillFileThatIsNotUtf8_ThrowsInvalidDataException()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFileBytes([.. "---\nname: a\n---\n"u8, 0xFC], "skills", "a", SkillFileName);

        // Act
        var act = () => FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        act
            .Should().Throw<InvalidDataException>()
            .WithMessage($"*{SkillPath("a")}*not valid UTF-8*");
    }

    [Fact]
    public void FindKitVersionViolations_BundledFileWithBom_ReportsTheVersionWithoutTheBom()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0", "a", "Body");
        root.WriteFileBytes([0xEF, 0xBB, 0xBF, .. "0.7.0 is the kit version of this skill"u8],
            "skills", "a", "notes.md");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("skills", "a", "notes.md")}:1: expected '0.8.0 is the kit version of this skill', "
                + "found '0.7.0 is the kit version of this skill'");
    }

    [Fact]
    public void FindKitVersionViolations_LowercaseSkillFileName_IsNotASkillFile()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("---\nname: a\nmetadata:\n  version: 0.8.0\n---\n", "skills", "a", "skill.md");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"skills: expected at least one {SkillFileName}, found none");
    }

    [Fact]
    public void FindKitVersionViolations_NoSkillFiles_ReportsMissingSkills()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("Run `shade-kit version --json --expect 0.7.0`.", "skills", "a", "notes.md");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal($"skills: expected at least one {SkillFileName}, found none");
    }

    [Fact]
    public void FindKitVersionViolations_NoValidKitVersion_ReportsOnlyTheInvalidVersions()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "0.8.0-beta.1", "a", "Run `shade-kit version --json --expect 0.7.0`.");

        // Act
        var violations = FindKitVersionViolations(root.RootPath, DefaultEngineVersion);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a")}:5: expected metadata.version as a plain x.y.z version, found '0.8.0-beta.1'");
    }

    private static void WriteSkill(TempKitDirectory root, string version, string name, string body)
    {
        root.WriteFile(
            $"---\nname: shade-kit-{name}\ndescription: Test skill {name}.\nmetadata:\n  version: {version}\n---\n\n"
            + $"{body}\n",
            "skills", name, SkillFileName);
    }

    private static string SkillPath(string name)
    {
        return Path.Combine("skills", name, SkillFileName);
    }

    /// <summary>
    /// Checks the kit version of the skills under <c>skills/</c>: every <c>SKILL.md</c> has a frontmatter
    /// <c>metadata.version</c> as a plain <c>x.y.z</c> version; all skills carry the same value (the kit version,
    /// chosen by <see cref="SelectKitSkill" />); its major.minor is the major.minor of the engine; every
    /// <c>--expect &lt;v&gt;</c> and every "&lt;v&gt; is the kit version of this skill" in any text file under
    /// <c>skills/</c> names the kit version.
    /// </summary>
    /// <returns>One line per violation: the path relative to the repository, the line, and expected vs. found.</returns>
    private static string[] FindKitVersionViolations(string repositoryRoot, SemanticVersion engineVersion)
    {
        var skills = ShippedSkillFiles.EnumerateSkillFiles(repositoryRoot)
            .Select(file => ReadFrontmatterVersion(Path.GetRelativePath(repositoryRoot, file),
                ShippedSkillFiles.ReadLines(file)))
            .ToArray();

        if (skills.Length == 0)
        {
            return [$"{ShippedSkillFiles.SkillsDirectoryName}: expected at least one {SkillFileName}, found none"];
        }

        var violations = new List<string>();
        var kitSkill = SelectKitSkill(skills, engineVersion);

        foreach (var skill in skills)
        {
            if (skill.Version is null)
            {
                violations.Add($"{skill.File}:{skill.Line}: {skill.Error}");
                continue;
            }

            if (skill.Version != kitSkill?.Version)
            {
                violations.Add($"{skill.File}:{skill.Line}: expected metadata.version {kitSkill?.Version} "
                               + $"(the kit version of {kitSkill?.File}), found {skill.Version}");
            }

            if (skill.Version.Major != engineVersion.Major || skill.Version.Minor != engineVersion.Minor)
            {
                violations.Add($"{skill.File}:{skill.Line}: expected the major.minor "
                               + $"{engineVersion.Major}.{engineVersion.Minor} of the engine version {engineVersion}, "
                               + $"found {skill.Version}");
            }
        }

        if (kitSkill?.Version is not null)
        {
            violations.AddRange(FindHandshakeViolations(repositoryRoot, kitSkill.Version.ToString()));
        }

        return violations.ToArray();
    }

    /// <summary>
    /// Selects the skill whose version is the kit version, so that a single skill with another version is the one
    /// that is reported.
    /// </summary>
    /// <remarks>
    /// The kit version is the most frequent valid version. On a tie, a version with the major.minor of the engine wins,
    /// then the version of the skill that comes first in ordinal path order.
    /// </remarks>
    /// <returns>
    /// The first skill in ordinal path order that carries the kit version, or <see langword="null" /> if no skill has a
    /// valid version.
    /// </returns>
    private static SkillVersion? SelectKitSkill(SkillVersion[] skills, SemanticVersion engineVersion)
    {
        // GroupBy keeps the groups in the order of their first skill and the skills of a group in path order, and the
        // sort is stable, so the remaining ties keep the path order.
        return skills
            .Where(x => x.Version is not null)
            .GroupBy(x => x.Version!)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group =>
                group.Key.Major == engineVersion.Major && group.Key.Minor == engineVersion.Minor)
            .Select(group => group.First())
            .FirstOrDefault();
    }

    // The texts are matched as a whole, so that a handshake wrapped across lines is found too; the line of a violation
    // is the line where the match starts.
    private static IEnumerable<string> FindHandshakeViolations(string repositoryRoot, string kitVersion)
    {
        var expectedSentence = $"{kitVersion} is the kit version of this skill";

        return ShippedSkillFiles.ReadTextFiles(repositoryRoot)
            .SelectMany(file =>
            {
                var path = Path.GetRelativePath(repositoryRoot, file.File);

                return ExpectOptionRegex().Matches(file.Text)
                    .Select(match => (match.Index, Version: match.Groups["version"].Value.TrimEnd('.', ':'),
                        Found: (string?)null))
                    .Concat(KitVersionSentenceRegex().Matches(file.Text)
                        .Select(match => (match.Index, Version: match.Groups["version"].Value,
                            Found: (string?)match.Value)))
                    .Where(x => x.Version != kitVersion)
                    .OrderBy(x => x.Index)
                    .Select(x =>
                    {
                        var location = $"{path}:{ShippedSkillFiles.SplitLines(file.Text[..x.Index]).Length}";

                        return x.Found is null
                            ? $"{location}: expected --expect {kitVersion}, found --expect {x.Version}"
                            : $"{location}: expected '{expectedSentence}', found '{x.Found}'";
                    });
            });
    }

    private static SkillVersion ReadFrontmatterVersion(string file, string[] lines)
    {
        var closingIndex = lines.Length > 0 && lines[0].TrimEnd() == FrontmatterDelimiter
            ? Array.FindIndex(lines, 1, line => line.TrimEnd() == FrontmatterDelimiter)
            : -1;

        if (closingIndex < 0)
        {
            return new SkillVersion(file, 1, null,
                $"expected a frontmatter between a first line '{FrontmatterDelimiter}' and a second line "
                + $"'{FrontmatterDelimiter}', found none");
        }

        var versionIndex = FindMetadataVersionIndex(lines, closingIndex);

        if (versionIndex >= 0)
        {
            var value = MetadataVersionRegex().Match(lines[versionIndex]).Groups["value"].Value;

            return IsPlainVersion(value, out var version)
                ? new SkillVersion(file, versionIndex + 1, version, null)
                : new SkillVersion(file, versionIndex + 1, null,
                    $"expected metadata.version as a plain x.y.z version, found '{value}'");
        }

        return new SkillVersion(file, closingIndex + 1, null,
            "expected metadata.version in the frontmatter, found none");
    }

    // The index of the line "  version: ..." among the entries of "metadata:" in the frontmatter (lines 1 to
    // closingIndex - 1), or -1. The entries are the indented or blank lines that follow "metadata:".
    private static int FindMetadataVersionIndex(string[] lines, int closingIndex)
    {
        var metadataIndex = Array.FindIndex(lines, 1, closingIndex - 1, line => MetadataKeyRegex().IsMatch(line));

        if (metadataIndex < 0)
        {
            return -1;
        }

        for (var index = metadataIndex + 1; index < closingIndex; index++)
        {
            var line = lines[index];

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                return -1;
            }

            if (MetadataVersionRegex().IsMatch(line))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsPlainVersion(string value, out SemanticVersion? version)
    {
        return SemanticVersion.TryParse(value, out version) && !version.IsPrerelease && version.ToString() == value;
    }

    private sealed record SkillVersion(string File, int Line, SemanticVersion? Version, string? Error);

    [GeneratedRegex(@"^metadata:[ \t]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MetadataKeyRegex();

    // A direct child "version:" of the metadata map: exactly two spaces, the indentation the skills use. YAML needs a
    // space after the colon ("version:0.8.0" is no key), so the value follows only after a blank.
    [GeneratedRegex(@"^  version:(?:[ \t]+(?<value>.*?))?[ \t]*$", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex MetadataVersionRegex();

    // The value after "--expect" (a blank, a line break, or "="; optionally quoted); a trailing "." or ":" of the
    // sentence is cut off in code.
    [GeneratedRegex(@"--expect(?:\s+|=)[""'`]?(?<version>[^\s`'"";,)]+)", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExpectOptionRegex();

    // The version token before the sentence, which may be wrapped across lines. The lookbehind starts a match only at
    // the start of a token and the atomic group gives up a token without backtracking, so a long line without
    // whitespace takes linear time. Quotes, parentheses, and Markdown emphasis are not part of the token.
    [GeneratedRegex(
        @"(?<![^\s`'""(*_])(?<version>(?>[^\s`'""(*_]+))[*_`'""]*\s+is\s+the\s+kit\s+version\s+of\s+this\s+skill",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KitVersionSentenceRegex();
}
