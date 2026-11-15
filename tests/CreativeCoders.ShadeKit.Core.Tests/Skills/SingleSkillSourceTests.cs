using AwesomeAssertions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

[Trait("Requirement", "FP08-AC4")]
public class SingleSkillSourceTests
{
    private const string SkillFileName = ShippedSkillFiles.SkillFileName;

    private const string ManifestReason =
        "agent-specific plugin or marketplace manifest; the only manifests are in .claude-plugin/";

    private const string AgentsFileReason = "agent-specific skill file; a skill folder holds no agents/ files in v1";

    [Fact]
    public void FindViolations_Repository_ReturnsNoViolation()
    {
        // Arrange
        var repositoryRoot = ShippedSkillFiles.FindRepositoryRoot();

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(repositoryRoot);

        // Assert
        ShippedSkillFiles.EnumerateSkillFiles(repositoryRoot)
            .Should().NotBeEmpty("the repository ships its skills under skills/");

        violations
            .Should().BeEmpty(
                "the repository holds one source per skill (skills/) and one plugin manifest set (.claude-plugin/); "
                + "violations:{0}{1}",
                Environment.NewLine, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FindViolations_ClaudePluginLayoutWithUniqueSkills_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "shade-kit-b", "skills", "shade-kit-b");
        root.WriteFile("Guide", "skills", "shade-kit-a", "references", "agents.md");
        root.WriteFile("{}", ".claude-plugin", "plugin.json");
        root.WriteFile("{}", ".claude-plugin", "marketplace.json");
        root.WriteFile("name: ci", ".github", "workflows", "ci.yml");
        root.WriteFile("{}", "src", "plugin.json");
        root.WriteFile("{}", "src", "marketplace.json");
        root.WriteFile("# Agent", "agents", "reviewer.md");
        root.WriteFile("{}", "src", ".codex-plugin", "plugin.json");
        root.WriteFile("{}", "src", ".github", "plugin", "marketplace.json");
        root.CreateDirectory(".cursor-plugin");
        root.WriteFile("---\nname: local\n---\nBody", ".claude", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_SameFrontmatterNameInTwoSkillFolders_ReportsBothFiles()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-b");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-b")),
                $"{SkillPath("skills", "shade-kit-b")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")));
    }

    [Fact]
    public void FindViolations_FrontmatterNameEqualsDirectoryNameOfOtherSkill_ReportsBothFiles()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "\"shade-kit-a\"", "skills", "group", "other");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("skills", "group", "other")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "group", "other")));
    }

    [Fact]
    public void FindViolations_SkillCopiedOutsideSkillsWithoutFrontmatter_ReportsBothFilesByDirectoryName()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile("# A copy without frontmatter", "dist", "shade-kit-a", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("dist", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("dist", "shade-kit-a")));
    }

    [Fact]
    public void FindViolations_NamesThatDifferOnlyInCaseAndLowercaseFileName_ReportsBothFiles()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile("---\nname: Shade-Kit-A\n---\nBody", "copies", "x", "skill.md");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("copies", "x", "skill.md")}: "
                + DuplicateReason("Shade-Kit-A", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", Path.Combine("copies", "x", "skill.md")));
    }

    [Fact]
    public void FindViolations_SkillNameInThreeFiles_ListsBothOtherFilesForEachFile()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "a", "shade-kit-a");
        WriteSkill(root, "shade-kit-a", "b", "shade-kit-a");
        WriteSkill(root, "shade-kit-a", "c", "shade-kit-a");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("a", "shade-kit-a")}: " + DuplicateReason("shade-kit-a",
                    $"{SkillPath("b", "shade-kit-a")}, {SkillPath("c", "shade-kit-a")}"),
                $"{SkillPath("b", "shade-kit-a")}: " + DuplicateReason("shade-kit-a",
                    $"{SkillPath("a", "shade-kit-a")}, {SkillPath("c", "shade-kit-a")}"),
                $"{SkillPath("c", "shade-kit-a")}: " + DuplicateReason("shade-kit-a",
                    $"{SkillPath("a", "shade-kit-a")}, {SkillPath("b", "shade-kit-a")}"));
    }

    [Theory]
    [InlineData(".git")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("TestResults")]
    [InlineData(".artifacts")]
    [InlineData(".tests")]
    [InlineData(".idea")]
    [InlineData("src", "CreativeCoders.ShadeKit.Cli", "bin", "Release")]
    [InlineData("tests", "CreativeCoders.ShadeKit.Core.Tests", "obj")]
    [InlineData("Bin")]
    public void FindViolations_SkillCopyInBuildOutputOrToolState_IsNotReported(params string[] excludedDirectory)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "shade-kit-a", [.. excludedDirectory, "skills", "shade-kit-a"]);
        WriteSkill(root, "shade-kit-b", [.. excludedDirectory, ".github", "skills", "shade-kit-b"]);
        root.WriteFile("{}", [.. excludedDirectory, ".codex-plugin", "plugin.json"]);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_SkillInExcludedDirectoryNameUnderSkills_IsReported()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "shade-kit-a", "skills", "bin", "shade-kit-a");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("skills", "bin", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "bin", "shade-kit-a")));
    }

    [Theory]
    [InlineData(".github", "skills")]
    [InlineData(".agents", "skills")]
    [InlineData(".claude", "skills")]
    [InlineData(".codex", "skills")]
    [InlineData("src", ".agents", "skills")]
    [InlineData(".GitHub", "Skills")]
    [InlineData("skills", "shade-kit-x", ".claude", "skills")]
    public void FindViolations_SkillFileInAgentSkillLocation_ReportsTheLocation(params string[] location)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "local-skill", [.. location, "local-skill"]);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath([.. location, "local-skill"])}: skill file in the agent skill location "
                + $"'{string.Join('/', location)}/'; skills live only under skills/");
    }

    [Fact]
    public void FindViolations_SkillFileDirectlyInAgentSkillLocation_ReportsTheLocation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("---\nname: local-skill\n---\nBody", ".claude", "skills", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine(".claude", "skills", SkillFileName)}: skill file in the agent skill location "
                + "'.claude/skills/'; skills live only under skills/");
    }

    [Fact]
    public void FindViolations_OtherFilesInAgentSkillLocationsAndSkillsInOtherFolders_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("# Notes", ".github", "skills", "README.md");
        root.WriteFile("{}", ".claude", "settings.json");
        WriteSkill(root, "example", "docs", "skills", "example");
        WriteSkill(root, "other", ".github", "other");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_AgentSkillLocationLinkedToSkills_ReportsLinkedCopies()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.CreateDirectory(".github");
        root.CreateDirectorySymbolicLinkOrSkip(Path.Combine(root.RootPath, "skills"), ".github", "skills");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath(".github", "skills", "shade-kit-a")}: skill file in the agent skill location "
                + "'.github/skills/'; skills live only under skills/",
                $"{SkillPath(".github", "skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath(".github", "skills", "shade-kit-a")));
    }

    [Fact]
    public void FindViolations_DirectoryLinkCycle_VisitsEveryDirectoryOnce()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.CreateDirectory("tools", "nested");
        root.CreateDirectorySymbolicLinkOrSkip(Path.Combine(root.RootPath, "tools"), "tools", "nested", "loop");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("plugin.json")]
    [InlineData("marketplace.json")]
    [InlineData(".plugin", "plugin.json")]
    [InlineData(".plugin", "marketplace.json")]
    [InlineData(".github", "plugin", "plugin.json")]
    [InlineData(".github", "plugin", "marketplace.json")]
    [InlineData(".agents", "plugins", "marketplace.json")]
    [InlineData(".agents", "plugins", "api_marketplace.json")]
    [InlineData(".codex-plugin", "plugin.json")]
    [InlineData(".cursor-plugin", "plugin.json")]
    [InlineData(".cursor-plugin", "marketplace.json")]
    [InlineData(".cursor-plugin", "rules", "x.mdc")]
    [InlineData("Marketplace.json")]
    [InlineData(".Codex-Plugin", "Plugin.json")]
    public void FindViolations_AgentSpecificManifestAtRoot_ReportsTheManifest(params string[] manifest)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile("{}", ".claude-plugin", "plugin.json");
        root.WriteFile("{}", manifest);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal($"{Path.Combine(manifest)}: {ManifestReason}");
    }

    [Theory]
    [InlineData("agents", "openai.yaml")]
    [InlineData("agents", "nested", "config.toml")]
    [InlineData("Agents", "openai.yaml")]
    public void FindViolations_AgentSpecificFileInSkillFolder_ReportsTheFile(params string[] skillRelativePath)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile("policy: {}", ["skills", "shade-kit-a", .. skillRelativePath]);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal($"{Path.Combine(["skills", "shade-kit-a", .. skillRelativePath])}: {AgentsFileReason}");
    }

    [Fact]
    public void FindViolations_AgentSpecificFileInNestedSkillFolder_ReportsTheFile()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "group", "shade-kit-a");
        root.WriteFile("policy: {}", "skills", "group", "shade-kit-a", "agents", "openai.yaml");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("skills", "group", "shade-kit-a", "agents", "openai.yaml")}: {AgentsFileReason}");
    }

    [Fact]
    public void FindViolations_AgentsDirectoryOutsideASkillFolder_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile("policy: {}", "skills", "shade-kit-a", "references", "agents", "openai.yaml");
        root.WriteFile("policy: {}", "skills", "agents", "openai.yaml");
        root.WriteFile("# Agent", "agents", "openai.yaml");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_ViolationsOfSeveralRules_ReportsAllInPathOrder()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile("policy: {}", "skills", "shade-kit-a", "agents", "openai.yaml");
        WriteSkill(root, "local-skill", ".agents", "skills", "local-skill");
        root.WriteFile("{}", ".codex-plugin", "plugin.json");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath(".agents", "skills", "local-skill")}: skill file in the agent skill location "
                + "'.agents/skills/'; skills live only under skills/",
                $"{Path.Combine(".codex-plugin", "plugin.json")}: {ManifestReason}",
                $"{Path.Combine("skills", "shade-kit-a", "agents", "openai.yaml")}: {AgentsFileReason}");
    }

    [Theory]
    [InlineData("---\nname: shade-kit-a\nBody")]
    [InlineData("---\ndescription: x\n---\nname: shade-kit-a\n")]
    [InlineData("---\nmetadata:\n  name: shade-kit-a\n---\nBody")]
    [InlineData(" ---\nname: shade-kit-a\n---\nBody")]
    [InlineData("---\nname: \"shade-kit-a'\n---\nBody")]
    [InlineData("---\nname:\n---\nBody")]
    [InlineData("")]
    public void FindViolations_NameOutsideATopLevelFrontmatterNameEntry_IsNotTakenAsSkillName(string content)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile(content, "docs", "other", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("\uFEFF---\r\nname: shade-kit-a\r\n---\r\nBody\r\n")]
    [InlineData("---\nname: 'shade-kit-a'\n---\nBody")]
    [InlineData("--- \nname: shade-kit-a  \n---\t\nBody")]
    public void FindViolations_FrontmatterNameWithBomCrLfQuotesOrBlanks_IsTakenAsSkillName(string content)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.WriteFile(content, "docs", "other", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("docs", "other")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("docs", "other")));
    }

    [Fact]
    public void FindViolations_FrontmatterNameDiffersFromOwnDirectoryNameOnlyInCase_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-x", "skills", "Shade-Kit-X");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_DirectoryNameAndFrontmatterNameCollideWithDifferentFiles_ReportsBothNames()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-b");
        root.WriteFile("# No frontmatter", "docs", "shade-kit-b", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("docs", "shade-kit-b")}: "
                + DuplicateReason("shade-kit-b", SkillPath("skills", "shade-kit-b")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-b")),
                $"{SkillPath("skills", "shade-kit-b")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-b")}: "
                + DuplicateReason("shade-kit-b", SkillPath("docs", "shade-kit-b")));
    }

    [Theory]
    [InlineData("binaries")]
    [InlineData("objects")]
    [InlineData("TestResultsArchive")]
    [InlineData(".git-old")]
    public void FindViolations_DirectoryNameThatOnlyStartsWithAnExcludedName_IsChecked(string directory)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "local-skill", directory, ".github", "skills", "local-skill");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath(directory, ".github", "skills", "local-skill")}: skill file in the agent skill location "
                + $"'{directory}/.github/skills/'; skills live only under skills/");
    }

    [Fact]
    public void FindViolations_SkillFileLinkedOutsideSkills_ReportsBothFiles()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.CreateDirectory("docs", "shade-kit-a");
        root.CreateSymbolicLinkOrSkip(Path.Combine("..", "..", "skills", "shade-kit-a", SkillFileName),
            "docs", "shade-kit-a", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("docs", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("docs", "shade-kit-a")));
    }

    [Fact]
    public void FindViolations_DanglingSkillFileLink_ReportsItByDirectoryName()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.CreateDirectory("docs", "shade-kit-a");
        root.CreateSymbolicLinkOrSkip("missing.md", "docs", "shade-kit-a", SkillFileName);

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("docs", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("skills", "shade-kit-a")),
                $"{SkillPath("skills", "shade-kit-a")}: "
                + DuplicateReason("shade-kit-a", SkillPath("docs", "shade-kit-a")));
    }

    [Fact]
    public void FindViolations_RelativeDirectoryLinkCycle_VisitsEveryDirectoryOnce()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteSkill(root, "shade-kit-a", "skills", "shade-kit-a");
        root.CreateDirectory("tools", "nested");
        root.CreateDirectorySymbolicLinkOrSkip(Path.Combine("..", ".."), "tools", "nested", "loop");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_LinkCycleReachedThroughLinkedParent_ResolvesTheRealTargetAndStaysInTheRoot()
    {
        // Arrange
        // "alias" points to deep/a/b, and deep/a/b/loop points three levels up, to the root. Resolved by the spelling
        // of the path, alias/loop would be the parent of the root, where a skill file in an agent location lies.
        using var parent = new TempKitDirectory();
        var repositoryRoot = parent.CreateDirectory("repo");
        WriteSkill(parent, "outside-skill", "sibling", ".github", "skills", "outside-skill");
        WriteSkill(parent, "shade-kit-a", "repo", "skills", "shade-kit-a");
        parent.CreateDirectory("repo", "deep", "a", "b");
        parent.CreateDirectorySymbolicLinkOrSkip(Path.Combine(repositoryRoot, "deep", "a", "b"), "repo", "alias");
        parent.CreateDirectorySymbolicLinkOrSkip(Path.Combine("..", "..", ".."), "repo", "deep", "a", "b", "loop");

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(repositoryRoot);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_EmptyRoot_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();

        // Act
        var violations = SingleSkillSourceGuard.FindViolations(root.RootPath);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_MissingRoot_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        using var root = new TempKitDirectory();
        var missingRoot = Path.Combine(root.RootPath, "missing");

        // Act
        var act = () => SingleSkillSourceGuard.FindViolations(missingRoot);

        // Assert
        act
            .Should().Throw<DirectoryNotFoundException>();
    }

    private static void WriteSkill(TempKitDirectory root, string name, params string[] directory)
    {
        root.WriteFile($"---\nname: {name}\ndescription: Test skill.\nmetadata:\n  version: 0.8.0\n---\n\nBody\n",
            [.. directory, SkillFileName]);
    }

    private static string DuplicateReason(string name, string otherFiles)
    {
        return $"skill name '{name}' is also defined by {otherFiles}";
    }

    private static string SkillPath(params string[] directory)
    {
        return Path.Combine([.. directory, SkillFileName]);
    }
}
