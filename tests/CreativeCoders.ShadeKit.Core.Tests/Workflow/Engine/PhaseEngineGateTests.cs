using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineGateTests
{
    [Theory]
    [InlineData("alpha", false)]
    [InlineData("beta", true)]
    [InlineData("delta", true)]
    public void HasGate_PhaseOfProfile_IsFalseOnlyForFirstPhase(string phase, bool expected)
    {
        // Act
        var hasGate = new PhaseEngine().HasGate(Greek, phase);

        // Assert
        hasGate
            .Should().Be(expected);
    }

    [Fact]
    public void HasGate_OnePhaseProfile_IsFalse()
    {
        // Act
        var hasGate = new PhaseEngine().HasGate(OnePhase, "only");

        // Assert
        hasGate
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("omega")]
    [InlineData("Beta")]
    [InlineData("")]
    public void HasGate_PhaseNotInProfile_ThrowsPhaseNotInProfile(string phase)
    {
        // Act
        var act = () => new PhaseEngine().HasGate(Greek, phase);

        // Assert
        act.Should().Throw<PhaseNotInProfileException>()
            .Which.Should().BeEquivalentTo(new
            {
                Phase = phase,
                Profile = "greek",
                ProfilePhases = Greek.Phases.Select(x => x.Name).ToArray()
            });
    }

    [Fact]
    public void HasGate_NullProfile_ThrowsArgumentNull()
    {
        // Act
        var act = () => new PhaseEngine().HasGate(null!, "alpha");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasGate_NullPhase_ThrowsArgumentNull()
    {
        // Act
        var act = () => new PhaseEngine().HasGate(Greek, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForPhase_PhaseWithEntry_ReturnsConfiguredGate()
    {
        // Arrange
        var gate = new TransitionGate(false, "checks/x.md");
        var gates = new Dictionary<string, TransitionGate> { ["beta"] = gate };

        // Act
        var result = TransitionGate.ForPhase(gates, "beta");

        // Assert
        result
            .Should().BeSameAs(gate);
    }

    [Fact]
    public void ForPhase_PhaseWithoutEntry_ReturnsDefault()
    {
        // Arrange
        var gates = new Dictionary<string, TransitionGate> { ["beta"] = new TransitionGate(false, null) };

        // Act
        var result = TransitionGate.ForPhase(gates, "gamma");

        // Assert
        result
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Fact]
    public void ForPhase_NullGates_ThrowsArgumentNull()
    {
        // Act
        var act = () => TransitionGate.ForPhase(null!, "beta");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ForPhase_NullPhase_ThrowsArgumentNull()
    {
        // Act
        var act = () => TransitionGate.ForPhase(NoGates, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
