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

    [Fact]
    public void Constructor_WithoutArtifacts_HasNoArtifacts()
    {
        // Act
        var phase = new WorkflowPhase("alpha", "skill-alpha", []);

        // Assert
        phase.Artifacts
            .Should().BeEmpty();
    }

    [Fact]
    public void Constructor_Artifacts_KeepsCopyInOrder()
    {
        // Arrange
        var artifacts = new List<PhaseArtifact> { new PhaseArtifact("b.md"), new PhaseArtifact("a.md", onDemand: true) };

        // Act
        var phase = new WorkflowPhase("alpha", "skill-alpha", [], artifacts: artifacts);
        artifacts.Add(new PhaseArtifact("c.md"));

        // Assert
        phase.Artifacts
            .Should().Equal(new PhaseArtifact("b.md"), new PhaseArtifact("a.md", onDemand: true));
    }

    [Fact]
    public void Constructor_ArtifactNull_ThrowsArgumentException()
    {
        // Act
        var act = () => new WorkflowPhase("alpha", "skill-alpha", [], artifacts: [new PhaseArtifact("a.md"), null!]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("artifacts");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_SameArtifactFileTwice_ThrowsArgumentException(bool secondOnDemand)
    {
        // Act
        var act = () => new WorkflowPhase("alpha", "skill-alpha", [],
            artifacts: [new PhaseArtifact("a.md"), new PhaseArtifact("a.md", secondOnDemand)]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("artifacts");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void PhaseArtifact_FileNullEmptyOrWhitespace_ThrowsArgumentException(string? file)
    {
        // Act
        var act = () => new PhaseArtifact(file!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PhaseArtifact_WithoutOnDemand_IsAlwaysWritten()
    {
        // Act
        var artifact = new PhaseArtifact("spec.md");

        // Assert
        artifact.OnDemand
            .Should().BeFalse();
    }
}
