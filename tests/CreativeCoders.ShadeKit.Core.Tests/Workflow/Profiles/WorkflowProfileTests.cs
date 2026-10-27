using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Profiles;

public class WorkflowProfileTests
{
    [Fact]
    public void Constructor_Phases_KeepsCopyInOrder()
    {
        // Arrange
        var phases = new List<WorkflowPhase> { CreatePhase("beta"), CreatePhase("alpha") };

        // Act
        var profile = new WorkflowProfile("custom", phases);
        phases.Add(CreatePhase("gamma"));

        // Assert
        profile.Phases.Select(x => x.Name)
            .Should().Equal("beta", "alpha");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_NameNullEmptyOrWhitespace_ThrowsArgumentException(string? name)
    {
        // Act
        var act = () => new WorkflowProfile(name!, [CreatePhase("alpha")]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_PhasesNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new WorkflowProfile("custom", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NoPhases_ThrowsArgumentException()
    {
        // Act
        var act = () => new WorkflowProfile("custom", []);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("phases");
    }

    [Fact]
    public void Constructor_PhasesContainNull_ThrowsArgumentException()
    {
        // Act
        var act = () => new WorkflowProfile("custom", [CreatePhase("alpha"), null!]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("phases");
    }

    [Fact]
    public void Constructor_DuplicatePhaseName_ThrowsArgumentExceptionNamingPhase()
    {
        // Act
        var act = () => new WorkflowProfile("custom", [CreatePhase("alpha"), CreatePhase("beta"), CreatePhase("alpha")]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("phases")
            .WithMessage("*'alpha'*");
    }

    [Fact]
    public void Constructor_PhaseNamesDifferingOnlyInCase_AcceptsBoth()
    {
        // Act
        var profile = new WorkflowProfile("custom", [CreatePhase("alpha"), CreatePhase("Alpha")]);

        // Assert
        profile.Phases.Select(x => x.Name)
            .Should().Equal("alpha", "Alpha");
    }

    [Theory]
    [InlineData("alpha", 0)]
    [InlineData("beta", 1)]
    [InlineData("gamma", 2)]
    public void IndexOf_PhaseOfProfile_ReturnsPositionInProfileOrder(string phaseName, int expectedIndex)
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var index = profile.IndexOf(phaseName);

        // Assert
        index
            .Should().Be(expectedIndex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Alpha")]
    [InlineData(" alpha")]
    [InlineData("alpha ")]
    [InlineData("delta")]
    public void IndexOf_UnknownPhase_ReturnsMinusOne(string? phaseName)
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var index = profile.IndexOf(phaseName);

        // Assert
        index
            .Should().Be(-1);
    }

    [Theory]
    [InlineData("alpha", 0)]
    [InlineData("beta", 1)]
    [InlineData("gamma", 2)]
    public void TryGetPhase_PhaseOfProfile_ReturnsTrueAndPhase(string phaseName, int expectedIndex)
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var found = profile.TryGetPhase(phaseName, out var phase);

        // Assert
        found
            .Should().BeTrue();

        phase
            .Should().BeSameAs(profile.Phases[expectedIndex]);
    }

    [Fact]
    public void IndexOf_SinglePhaseProfile_ReturnsZeroForItsPhaseAndMinusOneOtherwise()
    {
        // Arrange
        var profile = new WorkflowProfile("single", [CreatePhase("alpha")]);

        // Act
        var indexOfPhase = profile.IndexOf("alpha");
        var indexOfOther = profile.IndexOf("beta");

        // Assert
        indexOfPhase
            .Should().Be(0);

        indexOfOther
            .Should().Be(-1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Beta")]
    [InlineData("delta")]
    public void TryGetPhase_UnknownPhase_ReturnsFalseAndNull(string? phaseName)
    {
        // Arrange
        var profile = CreateProfile();

        // Act
        var found = profile.TryGetPhase(phaseName, out var phase);

        // Assert
        found
            .Should().BeFalse();

        phase
            .Should().BeNull();
    }

    private static WorkflowProfile CreateProfile()
    {
        return new WorkflowProfile("custom", [CreatePhase("alpha"), CreatePhase("beta"), CreatePhase("gamma")]);
    }

    private static WorkflowPhase CreatePhase(string name)
    {
        return new WorkflowPhase(name, $"skill-{name}", []);
    }
}
