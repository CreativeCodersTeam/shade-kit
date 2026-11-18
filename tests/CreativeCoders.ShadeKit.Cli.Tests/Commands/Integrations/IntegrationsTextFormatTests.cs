using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations;
using Spectre.Console;
using Spectre.Console.Testing;
using static CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations.IntegrationsCommandTestData;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Integrations;

public class IntegrationsTextFormatTests
{
    [Fact]
    public void FormatResult_UpdatedWithoutPreviousKitVersion_WritesOnlyNewVersion()
    {
        // Arrange
        var result = CreateResult(IntegrationOutcome.Updated);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, ReleaseSource);

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: updated shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void FormatResult_BranchWithoutKitVersion_WritesOnlyRef()
    {
        // Arrange
        var result = new IntegrationResult("claude", PluginId, IntegrationOutcome.Installed, null, null, "main", true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result,
            MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "main"));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: installed shade-kit@creativecoders-shade-kit (main)");
    }

    [Fact]
    public void FormatResult_DefaultBranchOfGitHubSource_WritesDefaultBranch()
    {
        // Arrange
        var result = new IntegrationResult("claude", PluginId, IntegrationOutcome.Installed, null, null, null, true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result,
            MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit"));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: installed shade-kit@creativecoders-shade-kit (default branch)");
    }

    [Fact]
    public void FormatResult_UpdatedToDefaultBranchOfGitHubSource_WritesPreviousVersionAndDefaultBranch()
    {
        // Arrange
        var result = new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.7.0", null, null, true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result,
            MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit"));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: updated shade-kit@creativecoders-shade-kit 0.7.0 -> (default branch)");
    }

    [Theory]
    [InlineData(IntegrationOutcome.Installed, "installed")]
    [InlineData(IntegrationOutcome.Current, "already current")]
    public void FormatResult_NotUpdatedWithPreviousKitVersion_WritesNoArrow(IntegrationOutcome outcome,
        string outcomeText)
    {
        // Arrange
        var result = CreateResult(outcome, "0.7.0");

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, ReleaseSource);

        // Assert
        ToPlainText(markup)
            .Should().Be($"claude: {outcomeText} shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void FormatResult_DirectoryPathWithMarkupCharacters_EscapesPath()
    {
        // Arrange
        var source = MarketplaceSource.ForDirectory(Path.Combine(Path.GetTempPath(), "[kit]"));
        var result = new IntegrationResult("claude", PluginId, IntegrationOutcome.Installed, null, null, null, true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, source);

        // Assert
        ToPlainText(markup)
            .Should().Be($"claude: installed shade-kit@creativecoders-shade-kit from {source.Path}");
    }

    [Fact]
    public void FormatResult_NullResult_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationsTextFormat.FormatResult(null!, ReleaseSource);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    [Fact]
    public void FormatResult_NullSource_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationsTextFormat.FormatResult(CreateResult(IntegrationOutcome.Installed), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void FormatResult_DirectorySource_WritesPathOfDirectory()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit");
        var source = MarketplaceSource.ForDirectory(path);
        var result = new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.7.0", null, null, true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, source);

        // Assert
        ToPlainText(markup)
            .Should().Be($"claude: updated shade-kit@creativecoders-shade-kit 0.7.0 -> from {source.Path}");
    }

    [Fact]
    public void FormatResult_ValuesWithMarkupCharacters_EscapesThem()
    {
        // Arrange
        var result = new IntegrationResult("claude", "[x]@y", IntegrationOutcome.Updated, "[old]", "[new]", "[ref]",
            true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, ReleaseSource);

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: updated [x]@y [old] -> [new] ([ref])");
    }

    [Fact]
    public void FormatInstalled_EnabledIntegration_WritesAgentPluginIdAndVersion()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatInstalled(
            new InstalledIntegration("claude", PluginId, "0.8.0", "v0.8.0", true));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void FormatInstalled_DisabledIntegrationWithoutRef_WritesDisabledMark()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatInstalled(
            new InstalledIntegration("claude", PluginId, null, null, false));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: shade-kit@creativecoders-shade-kit (disabled)");
    }

    [Fact]
    public void FormatInstalled_ValuesWithMarkupCharacters_EscapesThem()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatInstalled(
            new InstalledIntegration("claude", "[x]@y", "[1]", "[ref]", true));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: [x]@y [1] ([ref])");
    }

    [Fact]
    public void FormatInstalled_NullIntegration_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationsTextFormat.FormatInstalled(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("integration");
    }

    [Fact]
    public void FormatDisabledNote_IdsWithMarkupCharacters_EscapesThem()
    {
        // Act
        var claudeNote = IntegrationsTextFormat.FormatDisabledNote("claude", "[x]@y");
        var otherNote = IntegrationsTextFormat.FormatDisabledNote("[other]", "kit@market");

        // Assert
        ToPlainText(claudeNote)
            .Should().Be("The plugin is disabled in Claude Code. Run 'claude plugin enable [x]@y' to enable it.");

        ToPlainText(otherNote)
            .Should().Be("The integration is disabled in the agent '[other]'; enable it there.");
    }

    [Fact]
    public void FormatDisabledNote_OtherAgent_SaysToEnableItInTheAgent()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatDisabledNote("other", "kit@market");

        // Assert
        ToPlainText(markup)
            .Should().Be("The integration is disabled in the agent 'other'; enable it there.");
    }

    [Fact]
    public void WriteRun_OnlyOtherAgentChanged_WritesNoRestartHintForClaude()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(new IntegrationResult("other", "kit@market", IntegrationOutcome.Installed, null, "0.8.0",
            "v0.8.0", true));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines
            .Should().Equal("other: installed kit@market 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void WriteRun_ClaudeCurrentAndOtherAgentInstalled_WritesNoRestartHint()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateResult(IntegrationOutcome.Current),
            new IntegrationResult("other", "kit@market", IntegrationOutcome.Installed, null, "0.8.0", "v0.8.0", true));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines
            .Should().Equal(
                "claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "other: installed kit@market 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void WriteRun_SeveralAgentsWithDisabledOnes_WritesEachNoteAfterItsLineAndOneRestartHintAtTheEnd()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateResult(IntegrationOutcome.Installed, enabled: false),
            new IntegrationResult("other", "kit@market", IntegrationOutcome.Updated, "0.7.0", "0.8.0", "v0.8.0",
                false));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines
            .Should().Equal(
                "claude: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "  The plugin is disabled in Claude Code. Run 'claude plugin enable " +
                "shade-kit@creativecoders-shade-kit' to enable it.",
                "other: updated kit@market 0.7.0 -> 0.8.0 (v0.8.0)",
                "  The integration is disabled in the agent 'other'; enable it there.",
                "Restart Claude Code to load the skills.");
    }

    [Theory]
    [InlineData("copilot", "Restart GitHub Copilot CLI to load the skills.")]
    [InlineData("codex", "Start a new Codex session to load the skills.")]
    public void WriteRun_AgentInstalled_WritesRestartNoteOfThatAgent(string agentId, string expectedNote)
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateAgentResult(agentId, IntegrationOutcome.Installed));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines
            .Should().Equal($"{agentId}: installed shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)", expectedNote);
    }

    [Fact]
    public void WriteRun_ResultsInOtherOrder_WritesRestartNotesInRegistrationOrder()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateAgentResult("codex", IntegrationOutcome.Updated),
            CreateAgentResult("copilot", IntegrationOutcome.Installed), CreateResult(IntegrationOutcome.Installed));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines.Skip(3)
            .Should().Equal(
                "Restart Claude Code to load the skills.",
                "Restart GitHub Copilot CLI to load the skills.",
                "Start a new Codex session to load the skills.");
    }

    [Fact]
    public void WriteRun_OnlySomeAgentsChanged_WritesRestartNotesOnlyForThem()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateResult(IntegrationOutcome.Current),
            CreateAgentResult("copilot", IntegrationOutcome.Current),
            CreateAgentResult("codex", IntegrationOutcome.Updated));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines
            .Should().Equal(
                "claude: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "copilot: already current shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "codex: updated shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)",
                "Start a new Codex session to load the skills.");
    }

    [Fact]
    public void WriteRun_SameAgentTwice_WritesItsRestartNoteOnce()
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateAgentResult("copilot", IntegrationOutcome.Installed),
            CreateAgentResult("copilot", IntegrationOutcome.Updated));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines.Count(x => x == "Restart GitHub Copilot CLI to load the skills.")
            .Should().Be(1);
    }

    [Theory]
    [InlineData("copilot")]
    [InlineData("codex")]
    public void WriteRun_DisabledIntegrationOfAgent_WritesDisabledNoteOfThatAgentAfterItsLine(string agentId)
    {
        // Arrange
        var console = new TestConsole().Width(1000);
        var run = CreateRun(CreateAgentResult(agentId, IntegrationOutcome.Updated, enabled: false));

        // Act
        IntegrationsTextFormat.WriteRun(console, run);

        // Assert
        console.Lines[1]
            .Should().Be($"  {ToPlainText(IntegrationsTextFormat.FormatDisabledNote(agentId, PluginId))}");
    }

    [Fact]
    public void FormatDisabledNote_Copilot_NamesCopilotPluginEnable()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatDisabledNote("copilot", PluginId);

        // Assert
        ToPlainText(markup)
            .Should().Be("The plugin is disabled in GitHub Copilot CLI. Run 'copilot plugin enable " +
                         "shade-kit@creativecoders-shade-kit' to enable it.");
    }

    [Fact]
    public void FormatDisabledNote_Codex_NamesPluginsCommandOfCodexSession()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatDisabledNote("codex", PluginId);

        // Assert
        ToPlainText(markup)
            .Should().Be("The plugin is disabled in Codex. Enable it in Codex with /plugins.");
    }

    [Fact]
    public void FormatDisabledNote_CopilotWithMarkupCharactersInPluginId_EscapesThem()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatDisabledNote("copilot", "[x]@y");

        // Assert
        ToPlainText(markup)
            .Should().Be("The plugin is disabled in GitHub Copilot CLI. Run 'copilot plugin enable [x]@y' to enable " +
                         "it.");
    }

    [Fact]
    public void FormatResult_UpdatedWithSamePreviousKitVersion_WritesVersionOnce()
    {
        // Arrange
        var result = CreateAgentResult("copilot", IntegrationOutcome.Updated, previousKitVersion: "0.8.0");

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, ReleaseSource);

        // Assert
        ToPlainText(markup)
            .Should().Be("copilot: updated shade-kit@creativecoders-shade-kit 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void FormatResult_ClaudeUpdatedToOtherCommitWithSameKitVersion_WritesVersionOnce()
    {
        // Arrange
        var result = new IntegrationResult("claude", PluginId, IntegrationOutcome.Updated, "0.8.0", "0.8.0", "main",
            true);

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result,
            MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "main"));

        // Assert
        ToPlainText(markup)
            .Should().Be("claude: updated shade-kit@creativecoders-shade-kit 0.8.0 (main)");
    }

    [Fact]
    public void FormatResult_UpdatedWithOtherPreviousKitVersion_WritesArrow()
    {
        // Arrange
        var result = CreateAgentResult("codex", IntegrationOutcome.Updated, previousKitVersion: "0.7.0");

        // Act
        var markup = IntegrationsTextFormat.FormatResult(result, ReleaseSource);

        // Assert
        ToPlainText(markup)
            .Should().Be("codex: updated shade-kit@creativecoders-shade-kit 0.7.0 -> 0.8.0 (v0.8.0)");
    }

    [Fact]
    public void FormatInstalled_CodexWithoutKitVersionAndRef_WritesNoVersionAndNoNullText()
    {
        // Act
        var markup = IntegrationsTextFormat.FormatInstalled(
            new InstalledIntegration("codex", PluginId, null, null, true));

        // Assert
        ToPlainText(markup)
            .Should().Be("codex: shade-kit@creativecoders-shade-kit");
    }

    [Fact]
    public void WriteRun_NothingInstalledWithoutSource_SaysNothingIsInstalledAndHowToInstall()
    {
        // Arrange
        var console = new TestConsole().Width(1000);

        // Act
        IntegrationsTextFormat.WriteRun(console, CreateNothingInstalledRun());

        // Assert
        console.Lines
            .Should().Equal(
                "No SHADE kit integration is installed.",
                "Run 'shade-kit integrations install' to install it.");
    }

    [Fact]
    public void WriteRun_SourceWithoutResults_SaysNothingIsInstalledAndHowToInstall()
    {
        // Arrange
        var console = new TestConsole().Width(1000);

        // Act
        IntegrationsTextFormat.WriteRun(console, CreateRun());

        // Assert
        console.Lines
            .Should().Equal(
                "No SHADE kit integration is installed.",
                "Run 'shade-kit integrations install' to install it.");
    }

    [Fact]
    public void WriteRun_NullConsole_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationsTextFormat.WriteRun(null!, CreateRun());

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("console");
    }

    [Fact]
    public void WriteRun_NullResult_ThrowsArgumentNullException()
    {
        // Act
        var act = () => IntegrationsTextFormat.WriteRun(new TestConsole(), null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }

    private static IntegrationResult CreateAgentResult(string agentId, IntegrationOutcome outcome,
        string? previousKitVersion = null, bool enabled = true)
    {
        return new IntegrationResult(agentId, PluginId, outcome, previousKitVersion, "0.8.0", "v0.8.0", enabled);
    }

    private static string ToPlainText(string markup)
    {
        var console = new TestConsole().Width(1000);
        console.MarkupLine(markup);

        return console.Lines.Single();
    }
}
