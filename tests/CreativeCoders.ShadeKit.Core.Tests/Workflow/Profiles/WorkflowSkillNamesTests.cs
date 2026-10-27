using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Profiles;

public class WorkflowSkillNamesTests
{
    [Fact]
    public void GeneralSkillNames_Contract_UseShadeKitPrefix()
    {
        // Assert
        new[]
            {
                WorkflowSkillNames.Interview, WorkflowSkillNames.Research, WorkflowSkillNames.Review,
                WorkflowSkillNames.Report, WorkflowSkillNames.GateCheck
            }
            .Should().Equal("shade-kit-interview", "shade-kit-research", "shade-kit-review", "shade-kit-report",
                "shade-kit-gate-check");
    }

    [Theory]
    [InlineData(WorkflowPhaseNames.New, "shade-kit-new")]
    [InlineData(WorkflowPhaseNames.Refine, "shade-kit-refine")]
    [InlineData(WorkflowPhaseNames.Quick, "shade-kit-quick")]
    [InlineData("alpha", "shade-kit-alpha")]
    public void ForPhase_PhaseName_ReturnsShadeKitPrefixedName(string phase, string expectedSkill)
    {
        // Act
        var skill = WorkflowSkillNames.ForPhase(phase);

        // Assert
        skill
            .Should().Be(expectedSkill);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ForPhase_NullEmptyOrWhitespace_ThrowsArgumentException(string? phase)
    {
        // Act
        var act = () => WorkflowSkillNames.ForPhase(phase!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
