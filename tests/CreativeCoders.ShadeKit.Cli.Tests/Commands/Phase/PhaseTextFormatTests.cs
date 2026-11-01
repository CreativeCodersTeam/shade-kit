using AwesomeAssertions;
using CreativeCoders.ShadeKit.Cli.Commands.Phase;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;

namespace CreativeCoders.ShadeKit.Cli.Tests.Commands.Phase;

public class PhaseTextFormatTests
{
    [Fact]
    public void FormatPhaseName_NameWithMarkupCharacters_ReturnsEscapedNameAsInfo()
    {
        // Act
        var markup = PhaseTextFormat.FormatPhaseName("pl[an]");

        // Assert
        markup
            .Should().Be("[blue]pl[[an]][/]");
    }

    [Fact]
    public void FormatTransition_Transition_ReturnsPhaseNamesAsInfoAndKind()
    {
        // Act
        var markup = PhaseTextFormat.FormatTransition(
            new PhaseTransitionData(PhaseTransitionKind.Forward, "refine", "map"));

        // Assert
        markup
            .Should().Be("[blue]refine[/] -> [blue]map[/] (forward)");
    }

    [Fact]
    public void FormatPhase_Phase_ReturnsNameAsInfoAndColoredState()
    {
        // Act
        var markup = PhaseTextFormat.FormatPhase(new PhaseSnapshotData("refine", PhaseState.AwaitingApproval));

        // Assert
        markup
            .Should().Be("[blue]refine[/] ([yellow]awaiting-approval[/])");
    }

    [Theory]
    [InlineData(true, null, "approval: [yellow]required[/], checklist: [grey]none[/]")]
    [InlineData(false, "dor[1]", "approval: not required, checklist: dor[[1]]")]
    public void FormatGate_ApprovalAndChecklist_ReturnsMarkup(bool approval, string? checklist, string expected)
    {
        // Act
        var markup = PhaseTextFormat.FormatGate(approval, checklist);

        // Assert
        markup
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(new[] { "a", "[b]" }, "a, [[b]]")]
    [InlineData(new string[0], "[grey]none[/]")]
    public void FormatSkills_Skills_ReturnsEscapedNamesOrNone(string[] skills, string expected)
    {
        // Act
        var markup = PhaseTextFormat.FormatSkills(skills);

        // Assert
        markup
            .Should().Be(expected);
    }
}
