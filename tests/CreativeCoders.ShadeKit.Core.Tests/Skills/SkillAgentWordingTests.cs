using System.Text.RegularExpressions;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Skills;

/// <summary>
/// Guards the agent-specific wording that several skills repeat: the question-tool parenthetical and the call-form
/// phrase. Every skill under <c>skills/</c> that states one of these rules contains the canonical text verbatim, so
/// that a renamed tool or a changed call form is changed in every skill together.
/// </summary>
public partial class SkillAgentWordingTests
{
    /// <summary>
    /// The canonical question-tool parenthetical: the single reference for the text that every <c>SKILL.md</c> under
    /// <c>skills/</c> uses where it names the structured question tools. A skill may add agent-specific details (for
    /// example questions per call) after it, but never names a question tool outside it.
    /// </summary>
    private const string QuestionToolParenthetical =
        "(in Claude Code `AskUserQuestion`, in GitHub Copilot CLI `ask_user`, in OpenAI Codex CLI "
        + "`request_user_input`, which Codex offers only in plan mode by default; without one, also in Codex outside "
        + "plan mode, a plain question)";

    /// <summary>
    /// The canonical call-form phrase: the single reference for the text that every <c>SKILL.md</c> under
    /// <c>skills/</c> uses where it states the call form of a skill per agent. Concrete examples such as
    /// <c>$shade-kit:shade-kit-refine</c> may follow it.
    /// </summary>
    private const string CallFormPhrase =
        "`/<skill>` in Claude Code and GitHub Copilot CLI, `$shade-kit:<skill>` in OpenAI Codex CLI";

    private const string SkillFileName = ShippedSkillFiles.SkillFileName;

    private const string QuestionToolReason = "names a question tool outside the canonical question-tool parenthetical";

    private const string CallFormRuleReason = "states the call-form rule without the canonical call-form phrase";

    private const string CallFormMentionReason =
        "mentions the call form, but the skill does not contain the canonical call-form phrase";

    private const string CallFormRuleMarker = "form of the agent you run in";

    private static readonly string CanonicalQuestion =
        $"Ask with the agent's structured question tool {QuestionToolParenthetical}: keep it or drop it.";

    private static readonly string CanonicalCallForm =
        $"Write a skill call in the form of the agent you run in: {CallFormPhrase} (for example "
        + "`/shade-kit-refine` and `$shade-kit:shade-kit-refine`).";

    [Fact]
    public void FindViolations_ShippedSkills_UseTheCanonicalAgentWording()
    {
        // Arrange
        var repositoryRoot = ShippedSkillFiles.FindRepositoryRoot();
        var skillFiles = ShippedSkillFiles.EnumerateSkillFiles(repositoryRoot);

        // Act
        var violations = skillFiles
            .SelectMany(file => FindViolations(Path.GetRelativePath(repositoryRoot, file),
                ShippedSkillFiles.ReadLines(file)))
            .ToArray();

        // Assert
        skillFiles
            .Should().NotBeEmpty("the repository ships its skills under skills/");

        violations
            .Should().BeEmpty(
                "every skill uses the canonical question-tool parenthetical and call-form phrase; violations:{0}{1}",
                Environment.NewLine, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FindViolations_ShippedSkills_ContainBothCanonicalTexts()
    {
        // Arrange
        var repositoryRoot = ShippedSkillFiles.FindRepositoryRoot();
        var texts = ShippedSkillFiles.EnumerateSkillFiles(repositoryRoot)
            .Select(file => string.Join('\n', ShippedSkillFiles.ReadLines(file)))
            .ToArray();

        // Act
        var questionToolSkills =
            texts.Count(text => text.Contains(QuestionToolParenthetical, StringComparison.Ordinal));
        var callFormSkills = texts.Count(text => text.Contains(CallFormPhrase, StringComparison.Ordinal));

        // Assert
        questionToolSkills
            .Should().BePositive(
                "the guard must not pass because no skill uses the canonical parenthetical anymore");

        callFormSkills
            .Should().BePositive(
                "the guard must not pass because no skill uses the canonical call-form phrase anymore");
    }

    [Fact]
    public void FindViolations_CanonicalTextsWithAdditions_ReturnsNoViolation()
    {
        // Arrange
        string[] lines =
        [
            "# Skill",
            CanonicalQuestion,
            $"Ask per item {QuestionToolParenthetical}, at most 4 questions per call in Claude Code and 3 in OpenAI "
            + "Codex CLI: the ID.",
            CanonicalCallForm,
            "In OpenAI Codex CLI the message writes `$shade-kit:shade-kit-map`; see the [call form](#call-form).",
            "Without such a tool, also in OpenAI Codex CLI outside plan mode, ask a plain question."
        ];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Fact]
    public void FindViolations_TextWithoutAgentWording_ReturnsNoViolation()
    {
        // Arrange
        string[] lines = ["# Skill", "", "Ask the human a plain question.", "Load `shade-kit-next-steps`."];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData( // the old phase-entry wording
        "(in Claude Code `AskUserQuestion`, in GitHub Copilot CLI `ask_user`, in OpenAI Codex CLI "
        + "`request_user_input`, which Codex offers only in plan mode by default; without such a tool, also in Codex "
        + "outside plan mode, a plain question)")]
    [InlineData( // the old interview wording, without the fallback
        "(in Claude Code `AskUserQuestion`, in GitHub Copilot CLI `ask_user`, in OpenAI Codex CLI "
        + "`request_user_input`, which Codex offers only in plan mode by default)")]
    [InlineData( // the old gate-check wording
        "(in Claude Code `AskUserQuestion`, up to 4 questions per call; in GitHub Copilot CLI `ask_user`; in OpenAI "
        + "Codex CLI `request_user_input`, by default only in plan mode, up to 3 questions per call)")]
    [InlineData( // the plan-mode limit left out
        "(in Claude Code `AskUserQuestion`, in GitHub Copilot CLI `ask_user`, in OpenAI Codex CLI "
        + "`request_user_input`; without one, a plain question)")]
    [InlineData("(in Claude Code `AskUserQuestion`, in GitHub Copilot CLI `ask_user`)")]
    [InlineData("(in Claude Code `AskUserQuestion`)")]
    [InlineData("(in OpenAI Codex CLI request_user_input)")]
    public void FindViolations_DriftedQuestionToolParenthetical_ReportsLine(string parenthetical)
    {
        // Arrange
        string[] lines = ["# Skill", $"Ask with the agent's structured question tool {parenthetical}: yes or no."];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().Equal($"skill.md:2: {QuestionToolReason}: {lines[1]}");
    }

    [Fact]
    public void FindViolations_QuestionToolNamedBesideCanonicalParentheticalInSameLine_ReportsLine()
    {
        // Arrange
        string[] lines = [$"{CanonicalQuestion} In Codex call `request_user_input` with up to 3 questions."];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().Equal($"skill.md:1: {QuestionToolReason}: {lines[0]}");
    }

    [Fact]
    public void FindViolations_DriftedCopyBesideCanonicalCopy_ReportsOnlyTheDriftedLine()
    {
        // Arrange
        string[] lines =
        [
            CanonicalQuestion,
            "",
            "Ask again (in Claude Code `AskUserQuestion`, in Copilot `ask_user`)."
        ];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().Equal($"skill.md:3: {QuestionToolReason}: {lines[2]}");
    }

    [Theory]
    [InlineData( // the old phase-entry wording
        "Write a skill call in the form of the agent you run in: `/shade-kit-<skill>` in Claude Code and GitHub "
        + "Copilot CLI, `$shade-kit:shade-kit-<skill>` in OpenAI Codex CLI.")]
    [InlineData( // the old next-steps wording
        "Write every skill call in the form of the agent you run in: `/<skill>` in Claude Code and GitHub Copilot CLI "
        + "(for example `/shade-kit-refine`), `$shade-kit:<skill>` in OpenAI Codex CLI (for example "
        + "`$shade-kit:shade-kit-refine`).")]
    [InlineData( // the old quick wording
        "(written in the form of the agent you run in: `/shade-kit-new` in Claude Code and GitHub Copilot CLI, "
        + "`$shade-kit:shade-kit-new` in OpenAI Codex CLI)")]
    [InlineData("Write every skill call in the form of the agent you run in.")]
    public void FindViolations_DriftedCallFormRule_ReportsLine(string line)
    {
        // Arrange
        string[] lines = ["# Skill", line];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().Equal($"skill.md:2: {CallFormRuleReason}: {line}");
    }

    [Fact]
    public void FindViolations_DriftedCallFormRuleBesideCanonicalPhrase_ReportsOnlyTheDriftedLine()
    {
        // Arrange
        string[] lines =
        [
            CanonicalCallForm,
            "Write the call in the form of the agent you run in: `$shade-kit:shade-kit-<skill>` in Codex."
        ];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().Equal($"skill.md:2: {CallFormRuleReason}: {lines[1]}");
    }

    [Fact]
    public void FindViolations_CallFormMentionedWithoutCanonicalPhraseInSkill_ReportsEveryMention()
    {
        // Arrange
        string[] lines =
        [
            "In OpenAI Codex CLI the message writes `$shade-kit:shade-kit-refine`.",
            "Plain line.",
            "Use the [call form](#call-form) of your agent."
        ];

        // Act
        var violations = FindViolations("skill.md", lines);

        // Assert
        violations
            .Should().Equal(
                $"skill.md:1: {CallFormMentionReason}: {lines[0]}",
                $"skill.md:3: {CallFormMentionReason}: {lines[2]}");
    }

    [Fact]
    public void FindViolations_NestedSkillsInRepository_ReportsRelativePathOfEachDriftedSkill()
    {
        // Arrange
        using var root = new TempKitDirectory();
        root.WriteFile($"{CanonicalQuestion}\n{CanonicalCallForm}\n", "skills", "a", SkillFileName);
        root.WriteFile("Ask (in Claude Code `AskUserQuestion`).\n", "skills", "b", SkillFileName);
        root.WriteFile("Codex form `$shade-kit:shade-kit-new`.\r\n", "skills", "group", "c", SkillFileName);

        // Act
        var violations = ShippedSkillFiles.EnumerateSkillFiles(root.RootPath)
            .SelectMany(file => FindViolations(Path.GetRelativePath(root.RootPath, file),
                ShippedSkillFiles.ReadLines(file)))
            .ToArray();

        // Assert
        violations
            .Should().Equal(
                $"{Path.Combine("skills", "b", SkillFileName)}:1: {QuestionToolReason}: "
                + "Ask (in Claude Code `AskUserQuestion`).",
                $"{Path.Combine("skills", "group", "c", SkillFileName)}:1: {CallFormMentionReason}: "
                + "Codex form `$shade-kit:shade-kit-new`.");
    }

    /// <summary>
    /// Returns one violation (<c>path:line: reason: text</c>) per line of a skill that states an agent-specific rule
    /// without its canonical text: a line naming a question tool outside the canonical question-tool parenthetical, a
    /// line stating the call-form rule without the canonical call-form phrase, and, when the skill nowhere contains the
    /// canonical call-form phrase, every line that mentions the Codex call form or the call form.
    /// </summary>
    private static string[] FindViolations(string path, string[] lines)
    {
        var hasCallFormPhrase = lines.Any(line => line.Contains(CallFormPhrase, StringComparison.Ordinal));

        return lines
            .Select((line, index) => (Line: line, Number: index + 1, Reason: FindReason(line, hasCallFormPhrase)))
            .Where(x => x.Reason != null)
            .Select(x => $"{path}:{x.Number}: {x.Reason}: {x.Line}")
            .ToArray();
    }

    private static string? FindReason(string line, bool hasCallFormPhrase)
    {
        if (QuestionToolNameRegex().IsMatch(line.Replace(QuestionToolParenthetical, string.Empty,
                StringComparison.Ordinal)))
        {
            return QuestionToolReason;
        }

        if (line.Contains(CallFormRuleMarker, StringComparison.Ordinal))
        {
            return line.Contains(CallFormPhrase, StringComparison.Ordinal) ? null : CallFormRuleReason;
        }

        if (!hasCallFormPhrase && CallFormMentionRegex().IsMatch(line))
        {
            return CallFormMentionReason;
        }

        return null;
    }

    // The names of the structured question tools of Claude Code, GitHub Copilot CLI, and OpenAI Codex CLI.
    [GeneratedRegex(@"\b(?:AskUserQuestion|ask_user|request_user_input)\b", RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuestionToolNameRegex();

    // The Codex call form ("$shade-kit:") or a reference to the call form ("call form", any letter case).
    [GeneratedRegex(@"\$shade-kit:|\bcall form\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex CallFormMentionRegex();
}
