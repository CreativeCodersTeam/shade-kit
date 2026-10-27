using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

public class FeaturePhaseFieldsTests
{
    private const string FeatureId = "gh-142-login-sso";

    private const string FeatureFilePath = ".shadekit/features/gh-142-login-sso/feature.json";

    private static readonly PhaseHistoryActor Actor = new PhaseHistoryActor { Name = "Jane Doe", Email = "jane@example.com" };

    private static readonly DateTimeOffset At = new DateTimeOffset(2026, 10, 20, 15, 30, 45, TimeSpan.FromHours(2));

    [Fact]
    public void ToStoredState_DocumentWithPhaseFields_ReturnsFieldsWithFolderIdAndRepositoryPath()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        document.Status = FeatureStatus.Done;
        document.CurrentPhase = WorkflowPhaseNames.Refine;
        document.Phases = new OrderedDictionary<string, PhaseState> { [WorkflowPhaseNames.New] = PhaseState.Approved };
        document.PendingTransition = WorkflowPhaseNames.Map;

        // Act
        var stored = FeaturePhaseFields.ToStoredState(document, "folder-id");

        // Assert
        stored
            .Should().Be(new StoredPhaseState("folder-id", ".shadekit/features/folder-id/feature.json",
                FeatureStatus.Done, WorkflowPhaseNames.Refine, document.Phases, WorkflowPhaseNames.Map));
    }

    [Fact]
    public void ToStoredState_DocumentWithoutPhaseFields_ReturnsNullPhaseFields()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();

        // Act
        var stored = FeaturePhaseFields.ToStoredState(document, FeatureId);

        // Assert
        stored
            .Should().Be(new StoredPhaseState(FeatureId, FeatureFilePath, FeatureStatus.Active, null, null, null));
    }

    [Fact]
    public void Apply_InitialState_WritesAllPhaseFieldsInProfileOrderWithEmptyHistory()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        var state = new PhaseEngine().CreateInitialState(BuiltInWorkflowProfiles.Quick, FeatureId, FeatureFilePath);

        // Act
        FeaturePhaseFields.Apply(document, state);

        // Assert
        document.CurrentPhase
            .Should().Be(WorkflowPhaseNames.New);

        document.Phases!.Keys
            .Should().Equal(WorkflowPhaseNames.New, WorkflowPhaseNames.Quick, WorkflowPhaseNames.Finish);

        document.Phases.Values
            .Should().Equal(PhaseState.InProgress, PhaseState.Pending, PhaseState.Pending);

        document.PendingTransition
            .Should().BeNull();

        document.History
            .Should().NotBeNull().And.BeEmpty();

        document.Status
            .Should().Be(FeatureStatus.Active);
    }

    [Fact]
    public void ApplyChange_ExistingHistory_SetsNewStateAndAppendsEntry()
    {
        // Arrange
        var engine = new PhaseEngine();
        var document = PhaseJsonTestData.CreateDocument();
        var earlierEntry = new PhaseHistoryRecord(PhaseHistoryAction.Start, "x", "y", null).ToEntry(At, Actor);
        document.History = [earlierEntry];
        var state = engine.CreateInitialState(BuiltInWorkflowProfiles.Quick, FeatureId, FeatureFilePath);
        var change = engine.Start(state, WorkflowPhaseNames.Quick, new Dictionary<string, TransitionGate>()).Change!;

        // Act
        FeaturePhaseFields.ApplyChange(document, change, At, Actor);

        // Assert
        document.PendingTransition
            .Should().Be(WorkflowPhaseNames.Quick);

        document.Phases![WorkflowPhaseNames.New]
            .Should().Be(PhaseState.AwaitingApproval);

        document.History
            .Should().BeEquivalentTo([earlierEntry, change.History.ToEntry(At, Actor)],
                options => options.WithStrictOrdering());

        document.History[0]
            .Should().BeSameAs(earlierEntry);
    }

    [Fact]
    public void Apply_DocumentNull_ThrowsArgumentNullException()
    {
        // Arrange
        var state = new PhaseEngine().CreateInitialState(BuiltInWorkflowProfiles.Quick, FeatureId, FeatureFilePath);

        // Act
        var act = () => FeaturePhaseFields.Apply(null!, state);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyChange_ActorNull_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();
        var state = engine.CreateInitialState(BuiltInWorkflowProfiles.Quick, FeatureId, FeatureFilePath);
        var change = engine.Start(state, WorkflowPhaseNames.Quick, new Dictionary<string, TransitionGate>()).Change!;

        // Act
        var act = () => FeaturePhaseFields.ApplyChange(PhaseJsonTestData.CreateDocument(), change, At, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ReadState_DocumentWithoutPhaseFields_ReturnsInitialStateOfItsProfileWithoutChangingDocument()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();

        // Act
        var state = FeaturePhaseFields.ReadState(document, FeatureId, new BuiltInWorkflowProfileCatalog(),
            new PhaseEngine());

        // Assert
        state.Profile.Name
            .Should().Be(document.Profile);

        state.CurrentPhase
            .Should().Be(WorkflowPhaseNames.New);

        state.CurrentPhaseState
            .Should().Be(PhaseState.InProgress);

        state.FeatureFilePath
            .Should().Be(FeatureFilePath);

        document.CurrentPhase
            .Should().BeNull();

        document.Phases
            .Should().BeNull();
    }

    [Fact]
    public void ReadState_DocumentWithPhaseFields_ReturnsStateNormalizedByEngine()
    {
        // Arrange
        var document = CreateDocument(WorkflowProfileNames.Quick);
        document.CurrentPhase = WorkflowPhaseNames.Quick;
        document.Phases = new OrderedDictionary<string, PhaseState>
        {
            [WorkflowPhaseNames.Quick] = PhaseState.InProgress
        };

        // Act
        var state = FeaturePhaseFields.ReadState(document, FeatureId, new BuiltInWorkflowProfileCatalog(),
            new PhaseEngine());

        // Assert
        state.Phases
            .Should().Equal(KeyValuePair.Create(WorkflowPhaseNames.New, PhaseState.Pending),
                KeyValuePair.Create(WorkflowPhaseNames.Quick, PhaseState.InProgress),
                KeyValuePair.Create(WorkflowPhaseNames.Finish, PhaseState.Pending));
    }

    [Fact]
    public void ReadState_UnknownProfile_ThrowsUnknownProfileExceptionNamingFeatureFileAndKnownProfiles()
    {
        // Arrange
        var document = CreateDocument("legacy");

        // Act
        var act = () => FeaturePhaseFields.ReadState(document, FeatureId, new BuiltInWorkflowProfileCatalog(),
            new PhaseEngine());

        // Assert
        var exception = act.Should().Throw<UnknownProfileException>().Which;

        exception.Profile
            .Should().Be("legacy");

        exception.FeatureFilePath
            .Should().Be(FeatureFilePath);

        exception.KnownProfiles
            .Should().Equal(WorkflowProfileNames.Feature, WorkflowProfileNames.Bugfix, WorkflowProfileNames.Quick);
    }

    [Fact]
    public void ReadState_ProfileDiffersOnlyInCase_ThrowsUnknownProfileException()
    {
        // Arrange
        var document = CreateDocument("Feature");

        // Act
        var act = () => FeaturePhaseFields.ReadState(document, FeatureId, new BuiltInWorkflowProfileCatalog(),
            new PhaseEngine());

        // Assert
        act.Should().Throw<UnknownProfileException>()
            .Which.Profile
            .Should().Be("Feature");
    }

    [Fact]
    public void ReadState_InvalidPhaseFields_ThrowsInvalidPhaseStateException()
    {
        // Arrange
        var document = PhaseJsonTestData.CreateDocument();
        document.PendingTransition = WorkflowPhaseNames.Refine;

        // Act
        var act = () => FeaturePhaseFields.ReadState(document, FeatureId, new BuiltInWorkflowProfileCatalog(),
            new PhaseEngine());

        // Assert
        act.Should().Throw<InvalidPhaseStateException>()
            .Which.FeatureFilePath
            .Should().Be(FeatureFilePath);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReadState_ArgumentNull_ThrowsArgumentNullException(int nullArgument)
    {
        // Arrange
        var document = nullArgument == 0 ? null : PhaseJsonTestData.CreateDocument();
        var catalog = nullArgument == 1 ? null : new BuiltInWorkflowProfileCatalog();
        var engine = nullArgument == 2 ? null : new PhaseEngine();

        // Act
        var act = () => FeaturePhaseFields.ReadState(document!, FeatureId, catalog!, engine!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static FeatureDocument CreateDocument(string profile)
    {
        var template = PhaseJsonTestData.CreateDocument();

        return new FeatureDocument
        {
            Id = template.Id,
            Title = template.Title,
            Profile = profile,
            Source = template.Source,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy
        };
    }
}
