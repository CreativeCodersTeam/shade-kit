using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Profiles;

public class WorkflowPhaseTests
{
    [Fact]
    public void Constructor_WithoutCanReopen_CanBeReopened()
    {
        // Act
        var phase = new WorkflowPhase("alpha", "skill-alpha", []);

        // Assert
        phase.CanReopen
            .Should().BeTrue();
    }

    [Fact]
    public void Constructor_CanReopenFalse_CannotBeReopened()
    {
        // Act
        var phase = new WorkflowPhase("alpha", "skill-alpha", [], canReopen: false);

        // Assert
        phase.CanReopen
            .Should().BeFalse();
    }

    [Fact]
    public void Constructor_GeneralSkills_KeepsCopyInOrder()
    {
        // Arrange
        var generalSkills = new List<string> { "skill-b", "skill-a" };

        // Act
        var phase = new WorkflowPhase("alpha", "skill-alpha", generalSkills);
        generalSkills.Add("skill-c");

        // Assert
        phase.GeneralSkills
            .Should().Equal("skill-b", "skill-a");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_NameNullEmptyOrWhitespace_ThrowsArgumentException(string? name)
    {
        // Act
        var act = () => new WorkflowPhase(name!, "skill-alpha", []);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_PhaseSkillNullEmptyOrWhitespace_ThrowsArgumentException(string? phaseSkill)
    {
        // Act
        var act = () => new WorkflowPhase("alpha", phaseSkill!, []);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_GeneralSkillsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new WorkflowPhase("alpha", "skill-alpha", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_GeneralSkillNullEmptyOrWhitespace_ThrowsArgumentException(string? generalSkill)
    {
        // Act
        var act = () => new WorkflowPhase("alpha", "skill-alpha", ["skill-a", generalSkill!]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("generalSkills");
    }
}
