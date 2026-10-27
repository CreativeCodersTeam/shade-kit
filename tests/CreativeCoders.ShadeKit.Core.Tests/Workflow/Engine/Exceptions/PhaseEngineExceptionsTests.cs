using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.Exceptions;

public class PhaseEngineExceptionsTests
{
    [Fact]
    public void PhaseNotInProfileException_Values_ExposesThemAndListsPhasesInMessage()
    {
        // Act
        var exception = new PhaseNotInProfileException("refine", "bugfix", ["new", "diagnose", "plan"]);

        // Assert
        exception.Should().BeEquivalentTo(new { Phase = "refine", Profile = "bugfix" });

        exception.ProfilePhases
            .Should().Equal("new", "diagnose", "plan");

        exception.Message
            .Should().Be("The phase 'refine' is not a phase of the workflow profile 'bugfix'. Phases: new, diagnose, " +
                         "plan.");
    }

    [Fact]
    public void PhaseNotInProfileException_ProfilePhases_KeepsCopy()
    {
        // Arrange
        var phases = new List<string> { "new" };

        // Act
        var exception = new PhaseNotInProfileException("refine", "quick", phases);
        phases.Add("quick");

        // Assert
        exception.ProfilePhases
            .Should().Equal("new");
    }

    [Fact]
    public void PhaseNotInProfileException_ProfilePhasesNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PhaseNotInProfileException("refine", "quick", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PhaseTransitionNotAllowedException_Values_ExposesThemAndNamesThemInMessage()
    {
        // Act
        var exception = new PhaseTransitionNotAllowedException("gh-1-login", "plan", "refine",
            PhaseState.AwaitingApproval, "map", PhaseTransitionRefusalReason.OtherTransitionPending);

        // Assert
        exception.Should().BeEquivalentTo(new
        {
            FeatureId = "gh-1-login",
            Phase = "plan",
            CurrentPhase = "refine",
            CurrentState = PhaseState.AwaitingApproval,
            PendingTransition = "map",
            Reason = PhaseTransitionRefusalReason.OtherTransitionPending
        });

        exception.Message
            .Should().Be("The feature 'gh-1-login' cannot move to the phase 'plan' (other-transition-pending); the " +
                         "current phase 'refine' is awaiting-approval.");
    }

    [Theory]
    [InlineData(null, "The feature 'gh-1-login' has no pending transition to 'map'; no transition is pending.")]
    [InlineData("plan",
        "The feature 'gh-1-login' has no pending transition to 'map'; the transition to 'plan' is pending.")]
    public void NoPendingTransitionException_Values_ExposesThemAndNamesPendingTransitionInMessage(
        string? pendingTransition, string expectedMessage)
    {
        // Act
        var exception = new NoPendingTransitionException("gh-1-login", "map", pendingTransition);

        // Assert
        exception.Should().BeEquivalentTo(new
        {
            FeatureId = "gh-1-login",
            Phase = "map",
            PendingTransition = pendingTransition
        });

        exception.Message
            .Should().Be(expectedMessage);
    }

    [Fact]
    public void FeatureDoneException_FeatureId_ExposesItAndNamesItInMessage()
    {
        // Act
        var exception = new FeatureDoneException("gh-1-login");

        // Assert
        exception.FeatureId
            .Should().Be("gh-1-login");

        exception.Message
            .Should().Be("The feature 'gh-1-login' is done; its phases cannot change any more.");
    }

    [Fact]
    public void PhaseCompleteNotAllowedException_Values_ExposesThemAndNamesThemInMessage()
    {
        // Act
        var exception = new PhaseCompleteNotAllowedException("gh-1-login", "verify", PhaseState.InProgress,
            "finish");

        // Assert
        exception.Should().BeEquivalentTo(new
        {
            FeatureId = "gh-1-login",
            CurrentPhase = "verify",
            CurrentState = PhaseState.InProgress,
            LastPhase = "finish"
        });

        exception.Message
            .Should().Be("The feature 'gh-1-login' cannot be completed: only the last phase 'finish' can be " +
                         "completed while it is in progress, but the current phase 'verify' is in-progress.");
    }

    [Fact]
    public void InvalidPhaseStateException_Values_ExposesThemAndNamesFileAndFieldInMessage()
    {
        // Act
        var exception = new InvalidPhaseStateException(".shadekit/features/gh-1-login/feature.json",
            "currentPhase", "is missing, but the field 'phases' is set.");

        // Assert
        exception.Should().BeEquivalentTo(new
        {
            FeatureFilePath = ".shadekit/features/gh-1-login/feature.json",
            Field = "currentPhase",
            Reason = "is missing, but the field 'phases' is set."
        });

        exception.Message
            .Should().Be("The phase state in '.shadekit/features/gh-1-login/feature.json' is invalid: the field " +
                         "'currentPhase' is missing, but the field 'phases' is set.");
    }

    [Theory]
    [InlineData(typeof(PhaseNotInProfileException))]
    [InlineData(typeof(PhaseTransitionNotAllowedException))]
    [InlineData(typeof(NoPendingTransitionException))]
    [InlineData(typeof(FeatureDoneException))]
    [InlineData(typeof(PhaseCompleteNotAllowedException))]
    [InlineData(typeof(InvalidPhaseStateException))]
    public void Exceptions_CoreConvention_AreNotSealed(Type exceptionType)
    {
        // Assert
        exceptionType.IsSealed
            .Should().BeFalse();
    }
}
