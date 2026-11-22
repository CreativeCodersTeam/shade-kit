using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Versions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

/// <summary>
/// Guards that the shipped skills name the CLI contract of the engine (<see cref="CliContract.Current" />) in their
/// version handshake, and that no leftover of the removed kit version (<c>--expect</c>, a frontmatter
/// <c>version:</c>) remains.
/// </summary>
public partial class SkillContractTests
{
    private const string SkillFileName = ShippedSkillFiles.SkillFileName;

    private const string FrontmatterDelimiter = "---";

    private const string ContractSentenceTail = "is the CLI contract this skill is written for";

    private const int Contract = 3;

    private static readonly string[] HandshakeSkills =
    [
        "shade-kit-new", "shade-kit-discuss", "shade-kit-drop", "shade-kit-next-phase", "shade-kit-phase-entry"
    ];

    [Fact]
    [Trait("Requirement", "FP07-AC2")]
    public void SkillFiles_ShippedSkills_NameTheCurrentCliContractEverywhere()
    {
        // Arrange
        var repositoryRoot = ShippedSkillFiles.FindRepositoryRoot();

        // Act
        var violations = FindContractViolations(repositoryRoot, CliContract.Current);

        // Assert
        violations
            .Should().BeEmpty(
                "every handshake of the skills names the CLI contract {0} of the engine, and no skill carries the "
                + "removed --expect option or a frontmatter version; violations:{1}{2}",
                CliContract.Current, Environment.NewLine, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FindContractViolations_MatchingSkills_ReturnsNoViolation()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other", "Run `shade-kit version --json --expect-contract 3` first.");
        root.WriteFile("Run `shade-kit version --expect-contract=3 --json`.", "skills", "shade-kit-other",
            "references", "x.md");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindContractViolations_OtherExpectContractValue_ReportsFileLineAndValue()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other",
            "Line\nRun `shade-kit version --json --expect-contract 2`.\nOr `--expect-contract <n>`.");
        root.WriteFile("Run `shade-kit version --expect-contract=4 --json`.", "skills", "shade-kit-other",
            "references", "x.md");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:6: --expect-contract names another CLI contract "
                + "(expected 3, found 2)",
                $"{SkillPath("shade-kit-other")}:7: --expect-contract names another CLI contract "
                + "(expected 3, found <n>)",
                $"{Path.Combine("skills", "shade-kit-other", "references", "x.md")}:1: --expect-contract names "
                + "another CLI contract (expected 3, found 4)");
    }

    [Theory]
    [InlineData("Run `shade-kit version --json --expect-contract \"2\"`.")]
    [InlineData("Run `shade-kit version --json --expect-contract '2'`.")]
    [InlineData("Run shade-kit version --json --expect-contract `2`.")]
    [InlineData("Run `shade-kit version --json --expect-contract\t2`.")]
    [InlineData("Run shade-kit version --json --expect-contract 2.")]
    [InlineData("Run shade-kit version --json --expect-contract 2: the handshake.")]
    public void FindContractViolations_QuotedOrPunctuatedExpectContractValue_ReportsTheBareValue(string body)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other", body);

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:5: --expect-contract names another CLI contract "
                + "(expected 3, found 2)");
    }

    [Fact]
    public void FindContractViolations_MatchingAndOtherExpectContractOnOneLine_ReportsOnlyTheOtherOne()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other",
            "Run `--expect-contract 3` or `--expect-contract 30`, never `--expect-contract 3`.");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:5: --expect-contract names another CLI contract "
                + "(expected 3, found 30)");
    }

    [Fact]
    public void FindContractViolations_OtherContractInHandshakeSentence_ReportsFileLineAndValue()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other",
            "Line\n**2** is the CLI contract this skill is written for.\n(3 is the CLI contract this skill is "
            + "written for)");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:6: handshake sentence names another CLI contract "
                + "(expected '3 is the CLI contract this skill is written for', "
                + "found '2** is the CLI contract this skill is written for')");
    }

    [Fact]
    public void FindContractViolations_HandshakeSkillWithoutCanonicalSentence_ReportsSkill()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-drop",
            "1. Version handshake: run `shade-kit version --json --expect-contract 3`; 3 is the CLI contract of "
            + "this skill.");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-drop")}:1: canonical handshake sentence missing (expected "
                + $"'{CanonicalSentence(Contract)}', found none)");
    }

    [Fact]
    public void FindContractViolations_HandshakeSkillWithSentenceOfOtherContract_ReportsSkillAndBothValues()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-new", $"1. {CanonicalSentence(2)}");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-new")}:5: --expect-contract names another CLI contract "
                + "(expected 3, found 2)",
                $"{SkillPath("shade-kit-new")}:5: handshake sentence names another CLI contract "
                + "(expected '3 is the CLI contract this skill is written for', "
                + "found '2 is the CLI contract this skill is written for')",
                $"{SkillPath("shade-kit-new")}:1: canonical handshake sentence missing (expected "
                + $"'{CanonicalSentence(Contract)}', found none)");
    }

    [Fact]
    public void FindContractViolations_HandshakeSkillMissing_ReportsSkill()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        File.Delete(Path.Combine(root.RootPath, SkillPath("shade-kit-next-phase")));

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-next-phase")}: handshake skill missing (expected a {SkillFileName} with "
                + $"'{CanonicalSentence(Contract)}', found none)");
    }

    [Theory]
    [InlineData("Run `shade-kit version --json --expect 0.9.0`.", "--expect 0.9.0")]
    [InlineData("Run `shade-kit version --json --expect=0.9.0`.", "--expect 0.9.0")]
    [InlineData("Run shade-kit version --json --expect.", "--expect")]
    [InlineData("The `--expect` option.", "--expect")]
    public void FindContractViolations_OldExpectOption_ReportsFileAndLine(string body, string found)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other", "Line\n" + body);

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:6: removed option --expect (expected --expect-contract 3, "
                + $"found {found})");
    }

    [Theory]
    [InlineData("The option `--expect-contract` replaces it.")]
    [InlineData("The skill expected `--expected`.")]
    public void FindContractViolations_TextThatIsNotTheOldExpectOption_ReturnsNoViolation(string body)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other", body);

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("---\nname: shade-kit-other\nversion: 0.9.0\n---\nBody", 3, "version: 0.9.0")]
    [InlineData("---\nname: shade-kit-other\nmetadata:\n  version: 0.9.0\n---\nBody", 4, "version: 0.9.0")]
    [InlineData("---\nname: shade-kit-other\nmetadata:\n  author: x\n    version:\n---\nBody", 5, "version:")]
    [InlineData("---\r\nname: shade-kit-other\r\nversion : 1\r\n---\r\nBody", 3, "version : 1")]
    public void FindContractViolations_FrontmatterVersion_ReportsFileAndLine(string content, int line,
        string found)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        root.WriteFile(content, "skills", "shade-kit-other", SkillFileName);

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:{line}: frontmatter version key (expected none, found '{found}')");
    }

    [Theory]
    [InlineData("---\nname: shade-kit-other\ndescription: \"The version: handshake.\"\n---\nBody")]
    [InlineData("---\nname: shade-kit-other\nversions: 2\nversion:1\n---\nBody")]
    [InlineData("---\nname: shade-kit-other\n---\nversion: 0.9.0\n")]
    [InlineData("# shade-kit-other\nversion: 0.9.0\n")]
    public void FindContractViolations_VersionThatIsNoFrontmatterKey_ReturnsNoViolation(string content)
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        root.WriteFile(content, "skills", "shade-kit-other", SkillFileName);

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindContractViolations_FrontmatterVersionWithBom_ReportsFileAndLine()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        root.WriteFileBytes([0xEF, 0xBB, 0xBF, .. "---\r\nname: shade-kit-other\r\nversion: 1\r\n---\r\n"u8],
            "skills", "shade-kit-other", SkillFileName);

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:3: frontmatter version key (expected none, found 'version: 1')");
    }

    [Fact]
    public void FindContractViolations_NoSkillFiles_ReportsMissingSkills()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile("Run `shade-kit version --json --expect-contract 2`.", "skills", "a", "notes.md");
        root.WriteFile("---\nname: a\n---\n", "skills", "b", "skill.md");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal($"skills: expected at least one {SkillFileName}, found none");
    }

    [Fact]
    public void FindContractViolations_SkillFileThatIsNotUtf8_ThrowsInvalidDataException()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        root.WriteFileBytes([.. "---\nname: a\n---\n"u8, 0xFC], "skills", "a", SkillFileName);

        // Act
        var act = () => FindContractViolations(root.RootPath, Contract);

        // Assert
        act
            .Should().Throw<InvalidDataException>()
            .WithMessage($"*{SkillPath("a")}*not valid UTF-8*");
    }

    [Fact]
    public void FindContractViolations_HandshakeWrappedAcrossLines_ReportsTheLineWhereTheMatchStarts()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        WriteSkill(root, "shade-kit-other",
            "Version handshake: run `shade-kit version --json --expect-contract\n2`; 2 is the CLI\r\ncontract "
            + "this skill is written for.\n**4**\nis the CLI contract this skill is written for.");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{SkillPath("shade-kit-other")}:5: --expect-contract names another CLI contract "
                + "(expected 3, found 2)",
                $"{SkillPath("shade-kit-other")}:6: handshake sentence names another CLI contract "
                + "(expected '3 is the CLI contract this skill is written for', "
                + "found '2 is the CLI\r\ncontract this skill is written for')",
                $"{SkillPath("shade-kit-other")}:8: handshake sentence names another CLI contract "
                + "(expected '3 is the CLI contract this skill is written for', "
                + "found '4**\nis the CLI contract this skill is written for')");
    }

    [Fact]
    public void FindContractViolations_BundledFileWithBom_ReportsTheValueWithoutTheBom()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        root.WriteFileBytes([0xEF, 0xBB, 0xBF, .. "2 is the CLI contract this skill is written for"u8],
            "skills", "shade-kit-new", "notes.md");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("skills", "shade-kit-new", "notes.md")}:1: handshake sentence names another CLI "
                + "contract (expected '3 is the CLI contract this skill is written for', "
                + "found '2 is the CLI contract this skill is written for')");
    }

    [Fact]
    public void FindContractViolations_LongBundledLineWithoutWhitespace_FinishesWithoutRegexTimeout()
    {
        // Arrange
        using var root = new TempKitDirectory();
        WriteHandshakeSkills(root, Contract);
        root.WriteFile(new string('a', 200_000) + " is the CLI contract this skill is written for", "skills",
            "shade-kit-new", "asset.svg");

        // Act
        var violations = FindContractViolations(root.RootPath, Contract);

        // Assert
        violations
            .Should().ContainSingle()
            .Which.Should().StartWith(
                $"{Path.Combine("skills", "shade-kit-new", "asset.svg")}:1: handshake sentence names another CLI "
                + "contract (expected '3 is the CLI contract this skill is written for', found 'aaa");
    }

    // The canonical handshake sentence of the skills in HandshakeSkills, for the specified CLI contract.
    private static string CanonicalSentence(int contract)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"Version handshake: run `shade-kit version --json --expect-contract {contract}`; {contract} "
            + $"{ContractSentenceTail}.");
    }

    private static void WriteHandshakeSkills(TempKitDirectory root, int contract)
    {
        foreach (var skill in HandshakeSkills)
        {
            WriteSkill(root, skill, $"1. {CanonicalSentence(contract)}");
        }
    }

    // The body starts at line 5.
    private static void WriteSkill(TempKitDirectory root, string name, string body)
    {
        root.WriteFile($"---\nname: {name}\ndescription: Test skill {name}.\n---\n{body}\n", "skills", name,
            SkillFileName);
    }

    private static string SkillPath(string name)
    {
        return Path.Combine("skills", name, SkillFileName);
    }

    /// <summary>
    /// Checks the CLI contract of the skills under <c>skills/</c>: every <c>--expect-contract &lt;n&gt;</c> and every
    /// "&lt;n&gt; is the CLI contract this skill is written for" in any text file names
    /// <paramref name="currentContract" />; each skill in <see cref="HandshakeSkills" /> exists and contains the
    /// canonical handshake sentence verbatim; no text file names the removed option <c>--expect</c>; and no
    /// <c>SKILL.md</c> has a <c>version</c> key in its frontmatter, at any indentation.
    /// </summary>
    /// <returns>
    /// One line per violation: the path relative to the repository, the line (none for a missing file), what is wrong,
    /// and expected vs. found.
    /// </returns>
    /// <exception cref="InvalidDataException">A text file under <c>skills/</c> is not valid UTF-8.</exception>
    internal static string[] FindContractViolations(string repositoryRoot, int currentContract)
    {
        var skillFiles = ShippedSkillFiles.EnumerateSkillFiles(repositoryRoot)
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .ToHashSet(StringComparer.Ordinal);

        if (skillFiles.Count == 0)
        {
            return [$"{ShippedSkillFiles.SkillsDirectoryName}: expected at least one {SkillFileName}, found none"];
        }

        var contract = currentContract.ToString(CultureInfo.InvariantCulture);
        var textFiles = ShippedSkillFiles.ReadTextFiles(repositoryRoot)
            .Select(file => (Path: Path.GetRelativePath(repositoryRoot, file.File), file.Text, file.Lines))
            .ToArray();

        return textFiles
            .SelectMany(file => FindTextViolations(file.Path, file.Text, contract))
            .Concat(textFiles
                .Where(file => skillFiles.Contains(file.Path))
                .SelectMany(file => FindFrontmatterVersionViolations(file.Path, file.Lines)))
            .Concat(FindHandshakeSkillViolations(textFiles.ToDictionary(file => file.Path, file => file.Text,
                StringComparer.Ordinal), currentContract))
            .ToArray();
    }

    // The texts are matched as a whole, so that a handshake wrapped across lines is found too; the line of a violation
    // is the line where the match starts.
    private static IEnumerable<string> FindTextViolations(string path, string text, string contract)
    {
        var expectedSentence = $"{contract} {ContractSentenceTail}";

        var expectContract = ExpectContractOptionRegex().Matches(text)
            .Select(match => (match.Index, Value: match.Groups["value"].Value.TrimEnd('.', ':')))
            .Where(x => x.Value != contract)
            .Select(x => (x.Index,
                Message: $"--expect-contract names another CLI contract (expected {contract}, found {x.Value})"));

        var sentence = ContractSentenceRegex().Matches(text)
            .Where(match => match.Groups["value"].Value != contract)
            .Select(match => (match.Index,
                Message: "handshake sentence names another CLI contract "
                         + $"(expected '{expectedSentence}', found '{match.Value}')"));

        var oldExpect = OldExpectOptionRegex().Matches(text)
            .Select(match => (match.Index,
                Message: $"removed option --expect (expected --expect-contract {contract}, found "
                         + $"{("--expect " + match.Groups["value"].Value.TrimEnd('.', ':')).TrimEnd()})"));

        return expectContract
            .Concat(sentence)
            .Concat(oldExpect)
            .OrderBy(x => x.Index)
            .Select(x => $"{path}:{ShippedSkillFiles.SplitLines(text[..x.Index]).Length}: {x.Message}");
    }

    // A frontmatter is the block between a first line "---" and the next line "---"; a "version" key on any of its
    // lines, top-level or nested (for example under "metadata:"), is a leftover of the removed kit version.
    private static IEnumerable<string> FindFrontmatterVersionViolations(string path, string[] lines)
    {
        var closingIndex = lines.Length > 0 && lines[0].TrimEnd() == FrontmatterDelimiter
            ? Array.FindIndex(lines, 1, line => line.TrimEnd() == FrontmatterDelimiter)
            : -1;

        return Enumerable.Range(1, Math.Max(closingIndex - 1, 0))
            .Where(index => FrontmatterVersionKeyRegex().IsMatch(lines[index]))
            .Select(index =>
                $"{path}:{index + 1}: frontmatter version key (expected none, found '{lines[index].Trim()}')");
    }

    private static IEnumerable<string> FindHandshakeSkillViolations(Dictionary<string, string> textsByPath,
        int contract)
    {
        var canonicalSentence = CanonicalSentence(contract);

        foreach (var path in HandshakeSkills.Select(SkillPath))
        {
            if (!textsByPath.TryGetValue(path, out var text))
            {
                yield return $"{path}: handshake skill missing (expected a {SkillFileName} with "
                             + $"'{canonicalSentence}', found none)";
            }
            else if (!text.Contains(canonicalSentence, StringComparison.Ordinal))
            {
                yield return $"{path}:1: canonical handshake sentence missing (expected '{canonicalSentence}', "
                             + "found none)";
            }
        }
    }

    // The value after "--expect-contract" (a blank, a line break, or "="; optionally quoted); a trailing "." or ":" of
    // the sentence is cut off in code.
    [GeneratedRegex(@"--expect-contract(?:\s+|=)[""'`]?(?<value>[^\s`'"";,)]+)", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExpectContractOptionRegex();

    // The removed option "--expect": not "--expect-contract" and not a longer word such as "--expected". The value, if
    // any, is read like the one of "--expect-contract".
    [GeneratedRegex(@"--expect(?![-\w])(?:(?:\s+|=)[""'`]?(?<value>[^\s`'"";,)]+))?", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex OldExpectOptionRegex();

    // The contract token before the sentence, which may be wrapped across lines. The lookbehind starts a match only at
    // the start of a token and the atomic group gives up a token without backtracking, so a long line without
    // whitespace takes linear time. Quotes, parentheses, and Markdown emphasis are not part of the token.
    [GeneratedRegex(
        @"(?<![^\s`'""(*_])(?<value>(?>[^\s`'""(*_]+))[*_`'""]*\s+is\s+the\s+CLI\s+contract\s+this\s+skill\s+is"
        + @"\s+written\s+for", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ContractSentenceRegex();

    // A "version" key: at the start of the line after any indentation; YAML needs a blank or the line end after the
    // colon ("version:1" is no key).
    [GeneratedRegex(@"^[ \t]*version[ \t]*:(?:[ \t]|$)", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex FrontmatterVersionKeyRegex();
}
