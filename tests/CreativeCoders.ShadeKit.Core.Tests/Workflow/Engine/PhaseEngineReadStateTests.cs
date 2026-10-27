using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineReadStateTests
{
    [Theory]
    [Trait("Requirement", "FP04-AC1")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void CreateInitialState_BuiltInProfile_HasNewInProgressAndEveryOtherPhasePending(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);

        // Act
        var state = new PhaseEngine().CreateInitialState(profile, FeatureId, FeatureFilePath);

        // Assert
        state.Should().BeEquivalentTo(new
        {
            Profile = profile,
            FeatureId,
            FeatureFilePath,
            Status = FeatureStatus.Active,
            CurrentPhase = WorkflowPhaseNames.New,
            CurrentPhaseState = PhaseState.InProgress,
            PendingTransition = (string?)null,
            Pending = (PhaseTransition?)null
        });

        state.Phases
            .Should().Equal(profile.Phases.Select(x => KeyValuePair.Create(x.Name,
                x.Name == WorkflowPhaseNames.New ? PhaseState.InProgress : PhaseState.Pending)));
    }

    [Fact]
    public void CreateInitialState_SyntheticProfile_StartsWithFirstPhaseOfProfile()
    {
        // Act
        var state = new PhaseEngine().CreateInitialState(Greek, FeatureId, FeatureFilePath);

        // Assert
        state.Phases
            .Should().Equal(Phases(
                ("alpha", PhaseState.InProgress),
                ("beta", PhaseState.Pending),
                ("gamma", PhaseState.Pending),
                ("delta", PhaseState.Pending)));
    }

    [Theory]
    [InlineData(null, FeatureFilePath)]
    [InlineData(" ", FeatureFilePath)]
    [InlineData(FeatureId, null)]
    [InlineData(FeatureId, "")]
    public void CreateInitialState_FeatureIdOrPathNullOrWhitespace_ThrowsArgumentException(string? featureId,
        string? featureFilePath)
    {
        // Act
        var act = () => new PhaseEngine().CreateInitialState(Greek, featureId!, featureFilePath!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateInitialState_ProfileNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PhaseEngine().CreateInitialState(null!, FeatureId, FeatureFilePath);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [Trait("Requirement", "FP04-AC1")]
    [MemberData(nameof(BuiltInProfileNames), MemberType = typeof(PhaseEngineTestData))]
    public void ReadState_FeatureWithoutPhaseFields_ReturnsInitialStateOfProfile(string profileName)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);
        var engine = new PhaseEngine();

        // Act
        var state = engine.ReadState(profile, CreateStored(null));

        // Assert
        state.Should().BeEquivalentTo(engine.CreateInitialState(profile, FeatureId, FeatureFilePath));
    }

    [Fact]
    public void ReadState_FeatureWithoutCurrentPhaseAndEmptyPhases_ReturnsInitialState()
    {
        // Act
        var state = new PhaseEngine().ReadState(Greek, CreateStored(null, []));

        // Assert
        state.CurrentPhase
            .Should().Be("alpha");

        state.CurrentPhaseState
            .Should().Be(PhaseState.InProgress);
    }

    [Fact]
    public void ReadState_PendingTransitionWithoutCurrentPhase_ThrowsInvalidPhaseStateForPendingTransition()
    {
        // Act
        var act = () => new PhaseEngine().ReadState(Greek, CreateStored(null, pendingTransition: "beta"));

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Should().BeEquivalentTo(new { FeatureFilePath, Field = "pendingTransition" });
    }

    [Fact]
    public void ReadState_PhasesWithoutCurrentPhase_ThrowsInvalidPhaseStateForCurrentPhase()
    {
        // Act
        var act = () => new PhaseEngine().ReadState(Greek,
            CreateStored(null, Phases(("alpha", PhaseState.InProgress))));

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("currentPhase");
    }

    [Fact]
    public void ReadState_DoneFeatureWithoutCurrentPhase_ThrowsInvalidPhaseStateForCurrentPhase()
    {
        // Act
        var act = () => new PhaseEngine().ReadState(Greek, CreateStored(null, status: FeatureStatus.Done));

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("currentPhase");
    }

    [Theory]
    [InlineData("omega")]
    [InlineData("Alpha")]
    [InlineData("")]
    public void ReadState_CurrentPhaseNotInProfile_ThrowsInvalidPhaseStateForCurrentPhase(string currentPhase)
    {
        // Act
        var act = () => new PhaseEngine().ReadState(Greek,
            CreateStored(currentPhase, Phases((currentPhase, PhaseState.InProgress))));

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Should().BeEquivalentTo(new { FeatureFilePath, Field = "currentPhase" });
    }

    [Fact]
    public void ReadState_CurrentPhaseRemovedFromProfile_ThrowsInvalidPhaseStateNamingProfile()
    {
        // Arrange
        var stored = CreateStored("refine", Phases(("new", PhaseState.Approved), ("refine", PhaseState.InProgress)));

        // Act
        var act = () => new PhaseEngine().ReadState(BuiltInWorkflowProfiles.Bugfix, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Reason.Should().Be("names the phase 'refine', which is not a phase of the workflow profile " +
                                      "'bugfix'.");
    }

    [Fact]
    public void ReadState_PendingTransitionNotInProfile_ThrowsInvalidPhaseStateForPendingTransition()
    {
        // Arrange
        var stored = CreateStored("alpha", Phases(("alpha", PhaseState.AwaitingApproval)), "omega");

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("pendingTransition");
    }

    [Fact]
    public void ReadState_PendingTransitionWhileCurrentInProgress_ThrowsInvalidPhaseStateForPendingTransition()
    {
        // Arrange
        var stored = CreateStored("alpha", Phases(("alpha", PhaseState.InProgress)), "beta");

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("pendingTransition");
    }

    [Fact]
    public void ReadState_CurrentAwaitingApprovalWithoutPendingTransition_ThrowsInvalidPhaseStateForPendingTransition()
    {
        // Arrange
        var stored = CreateStored("alpha", Phases(("alpha", PhaseState.AwaitingApproval)));

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Should().BeEquivalentTo(new
            {
                Field = "pendingTransition",
                Reason = "is missing, but the current phase 'alpha' is awaiting-approval."
            });
    }

    [Theory]
    [InlineData("beta", "beta")]
    [InlineData("beta", "delta")]
    [InlineData("alpha", "gamma")]
    public void ReadState_PendingTransitionNeitherNextNorEarlier_ThrowsInvalidPhaseStateForPendingTransition(
        string currentPhase, string pendingTransition)
    {
        // Arrange
        var stored = CreateStored(currentPhase, Phases((currentPhase, PhaseState.AwaitingApproval)),
            pendingTransition);

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("pendingTransition");
    }

    [Theory]
    [InlineData(PhaseState.Pending)]
    [InlineData(PhaseState.Approved)]
    public void ReadState_ActiveFeatureWithCurrentPhasePendingOrApproved_ThrowsInvalidPhaseStateForPhases(
        PhaseState currentState)
    {
        // Arrange
        var stored = CreateStored("beta", Phases(("alpha", PhaseState.Approved), ("beta", currentState)));

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("phases");
    }

    [Fact]
    public void ReadState_CurrentPhaseWithoutEntry_CountsAsPendingAndThrowsInvalidPhaseState()
    {
        // Arrange
        var stored = CreateStored("beta", Phases(("alpha", PhaseState.Approved)));

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Reason.Should().Contain("'beta' the state pending");
    }

    [Fact]
    public void ReadState_CurrentPhaseWithoutPhasesField_CountsEveryPhaseAsPendingAndThrowsInvalidPhaseState()
    {
        // Act
        var act = () => new PhaseEngine().ReadState(Greek, CreateStored("alpha"));

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("phases");
    }

    [Fact]
    public void ReadState_UndefinedStateOfCurrentPhase_ThrowsInvalidPhaseStateForPhases()
    {
        // Arrange
        var stored = CreateStored("alpha", Phases(("alpha", (PhaseState)99)));

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("phases");
    }

    [Fact]
    public void ReadState_OtherPhaseInProgressBesidesCurrentPhase_IsAcceptedBecauseOnlyCurrentPhaseIsChecked()
    {
        // Arrange
        var stored = CreateStored("alpha", Phases(("alpha", PhaseState.InProgress), ("gamma", PhaseState.InProgress)));

        // Act
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Assert
        state.GetPhaseState("gamma")
            .Should().Be(PhaseState.InProgress);
    }

    [Fact]
    public void ReadState_DoneFeatureWithCurrentPhaseNotInProfile_ThrowsInvalidPhaseStateForCurrentPhase()
    {
        // Arrange
        var stored = CreateStored("omega", Phases(("omega", PhaseState.Approved)), status: FeatureStatus.Done);

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("currentPhase");
    }

    [Fact]
    public void ReadState_DoneFeatureWithCurrentPhaseApproved_ReturnsDoneState()
    {
        // Arrange
        var stored = CreateStored("delta", Phases(
            ("alpha", PhaseState.Approved),
            ("beta", PhaseState.Approved),
            ("gamma", PhaseState.Approved),
            ("delta", PhaseState.Approved)), status: FeatureStatus.Done);

        // Act
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Assert
        state.Status
            .Should().Be(FeatureStatus.Done);

        state.CurrentPhaseState
            .Should().Be(PhaseState.Approved);
    }

    [Fact]
    public void ReadState_DoneFeatureWhoseProfileGotANewLastPhase_StaysValid()
    {
        // Arrange
        var stored = CreateStored("gamma", Phases(
            ("alpha", PhaseState.Approved),
            ("beta", PhaseState.Approved),
            ("gamma", PhaseState.Approved)), status: FeatureStatus.Done);

        // Act
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Assert
        state.CurrentPhase
            .Should().Be("gamma");

        state.GetPhaseState("delta")
            .Should().Be(PhaseState.Pending);
    }

    [Theory]
    [InlineData(PhaseState.Pending)]
    [InlineData(PhaseState.InProgress)]
    [InlineData(PhaseState.AwaitingApproval)]
    public void ReadState_DoneFeatureWithCurrentPhaseNotApproved_ThrowsInvalidPhaseStateForPhases(
        PhaseState currentState)
    {
        // Arrange
        var stored = CreateStored("delta", Phases(("delta", currentState)), status: FeatureStatus.Done);

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("phases");
    }

    [Fact]
    public void ReadState_DoneFeatureWithPendingTransition_ThrowsInvalidPhaseStateForPendingTransition()
    {
        // Arrange
        var stored = CreateStored("delta", Phases(("delta", PhaseState.Approved)), "gamma", FeatureStatus.Done);

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.Field.Should().Be("pendingTransition");
    }

    [Fact]
    public void ReadState_PhasesInOtherOrderWithEntriesOfRemovedPhases_OrdersByProfileThenKeepsExtrasInStoredOrder()
    {
        // Arrange
        var stored = CreateStored("gamma", Phases(
            ("removed-two", PhaseState.Approved),
            ("gamma", PhaseState.InProgress),
            ("alpha", PhaseState.Approved),
            ("removed-one", PhaseState.Pending),
            ("beta", PhaseState.Approved)));

        // Act
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Assert
        state.Phases
            .Should().Equal(Phases(
                ("alpha", PhaseState.Approved),
                ("beta", PhaseState.Approved),
                ("gamma", PhaseState.InProgress),
                ("delta", PhaseState.Pending),
                ("removed-two", PhaseState.Approved),
                ("removed-one", PhaseState.Pending)));
    }

    [Fact]
    public void ReadState_PhaseAddedToProfileBeforeCurrentPhase_CountsAsPendingAndIsIgnoredByRules()
    {
        // Arrange
        var stored = CreateStored("gamma", Phases(("alpha", PhaseState.Approved), ("gamma", PhaseState.InProgress)));
        var engine = new PhaseEngine();

        // Act
        var state = engine.ReadState(Greek, stored);
        var decision = engine.Start(state, "delta", NoGates);

        // Assert
        state.GetPhaseState("beta")
            .Should().Be(PhaseState.Pending);

        decision.Outcome
            .Should().Be(PhaseStartOutcome.TransitionRequested);
    }

    [Fact]
    public void ReadState_PendingTransitionToEarlierPhase_ReturnsStateWithPendingReopen()
    {
        // Arrange
        var stored = CreateStored("gamma", Phases(("gamma", PhaseState.AwaitingApproval)), "alpha");

        // Act
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Assert
        state.Pending
            .Should().Be(new PhaseTransition(PhaseTransitionKind.Reopen, "gamma", "alpha"));
    }

    [Fact]
    public void ReadState_StoredPhasesChangedAfterRead_KeepsStateUnchanged()
    {
        // Arrange
        var phases = new OrderedDictionary<string, PhaseState> { ["alpha"] = PhaseState.InProgress };
        var stored = new StoredPhaseState(FeatureId, FeatureFilePath, FeatureStatus.Active, "alpha", phases, null);
        var state = new PhaseEngine().ReadState(Greek, stored);

        // Act
        phases["alpha"] = PhaseState.Approved;

        // Assert
        state.CurrentPhaseState
            .Should().Be(PhaseState.InProgress);
    }

    [Fact]
    public void ReadState_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();

        // Act
        var withoutProfile = () => engine.ReadState(null!, CreateStored(null));
        var withoutStored = () => engine.ReadState(Greek, null!);

        // Assert
        withoutProfile.Should().Throw<ArgumentNullException>();
        withoutStored.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("", FeatureFilePath)]
    [InlineData(FeatureId, " ")]
    public void ReadState_FeatureIdOrPathEmpty_ThrowsArgumentException(string featureId, string featureFilePath)
    {
        // Arrange
        var stored = new StoredPhaseState(featureId, featureFilePath, FeatureStatus.Active, null, null, null);

        // Act
        var act = () => new PhaseEngine().ReadState(Greek, stored);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
