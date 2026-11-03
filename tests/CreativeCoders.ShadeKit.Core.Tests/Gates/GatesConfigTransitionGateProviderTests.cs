using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

public sealed class GatesConfigTransitionGateProviderTests : IDisposable
{
    private readonly IGatesConfigReader _reader = A.Fake<IGatesConfigReader>();

    private readonly IKitStore _store = A.Fake<IKitStore>();

    private readonly TempKitDirectory _repository = new TempKitDirectory();

    public void Dispose()
    {
        _repository.Dispose();
    }

    [Fact]
    public void Ctor_NullGatesConfigReader_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GatesConfigTransitionGateProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("gatesConfigReader");
    }

    [Fact]
    public async Task GetGateAsync_GateWithApprovalAndChecklist_ReturnsGateWithChecklistUnchanged()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Plan,
            new GateConfig { Approval = true, Checklist = "checks/team/ready.md" }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().Be(new TransitionGate(true, "checks/team/ready.md"));
    }

    [Fact]
    public async Task GetGateAsync_GateWithoutApproval_ReturnsGateWithoutApproval()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Map,
            new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Map,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().Be(new TransitionGate(false, null));
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGateAsync_PhaseWithoutEntry_ReturnsDefaultGate()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Plan,
            new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Refine,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGateAsync_ProfileWithoutEntry_ReturnsDefaultGate()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Plan,
            new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Bugfix, WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGateAsync_ProfileWithoutGates_ReturnsDefaultGate()
    {
        // Arrange
        SetUpReader(new GatesConfig
        {
            Profiles = new Dictionary<string, Dictionary<string, GateConfig>>
            {
                [WorkflowProfileNames.Feature] = []
            }
        });
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Fact]
    public async Task GetGateAsync_SeveralCalls_ReturnsGateOfEachTransition()
    {
        // Arrange
        SetUpReader(GatesConfig.CreateDefault());
        var provider = new GatesConfigTransitionGateProvider(_reader);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var planGate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Bugfix, WorkflowPhaseNames.Plan,
            cancellationToken);
        var quickGate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Quick, WorkflowPhaseNames.Quick,
            cancellationToken);

        // Assert
        planGate
            .Should().Be(new TransitionGate(true, GatesConfig.DefinitionOfReadyChecklist));

        quickGate
            .Should().Be(new TransitionGate(false, null));

        A.CallTo(() => _reader.ReadAsync(_store, cancellationToken))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task GetGateAsync_ConfigWithoutProfiles_ReturnsDefaultGate()
    {
        // Arrange
        SetUpReader(new GatesConfig());
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Theory]
    [InlineData("Feature", WorkflowPhaseNames.Plan)]
    [InlineData(WorkflowProfileNames.Feature, "Plan")]
    public async Task GetGateAsync_NamesDifferOnlyInCase_ReturnsDefaultGate(string profileName, string phaseName)
    {
        // Arrange
        SetUpReader(CreateConfig(profileName, phaseName, new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Theory]
    [InlineData(WorkflowPhaseNames.Quick, false, null)]
    [InlineData(WorkflowPhaseNames.Finish, true, GatesConfig.DefinitionOfDoneChecklist)]
    public async Task GetGateAsync_DefaultConfigQuickProfile_ReturnsDefaultGatesOfQuickProfile(string targetPhase,
        bool expectedApproval, string? expectedChecklist)
    {
        // Arrange
        SetUpReader(GatesConfig.CreateDefault());
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var gate = await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Quick, targetPhase,
            TestContext.Current.CancellationToken);

        // Assert
        gate
            .Should().Be(new TransitionGate(expectedApproval, expectedChecklist));
    }

    [Fact]
    public async Task GetGateAsync_AnyPhase_ReadsConfigOfGivenStoreWithGivenToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpReader(GatesConfig.CreateDefault());
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        await provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            cancellationToken);

        // Assert
        A.CallTo(() => _reader.ReadAsync(_store, cancellationToken))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGateAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGateAsync(null!, BuiltInWorkflowProfiles.Quick, WorkflowPhaseNames.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");

        A.CallTo(_reader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetGateAsync_NullProfile_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGateAsync(_store, null!, WorkflowPhaseNames.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("profile");

        A.CallTo(_reader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetGateAsync_NullTargetPhase_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Quick, null!,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("targetPhase");

        A.CallTo(_reader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetGateAsync_ReaderCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        A.CallTo(() => _reader.ReadAsync(_store, cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationTokenSource.Token));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetGateAsync_ReaderThrows_PropagatesException()
    {
        // Arrange
        var exception = new InvalidKitFileException(".shadekit/gates.json", "The file is invalid.");
        A.CallTo(() => _reader.ReadAsync(_store, A<CancellationToken>._))
            .ThrowsAsync(exception);
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGateAsync(_store, BuiltInWorkflowProfiles.Feature, WorkflowPhaseNames.Plan,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task GetGateAsync_RealReaderWithGatesFile_ReturnsGatesFromFile()
    {
        // Arrange
        _repository.CreateGitRoot();
        var kitStorePath = _repository.CreateDirectory(KitStorePaths.RootPath);
        _repository.WriteFile(
            """
            {
              "schemaVersion": 1, "minEngineVersion": "0.1.0",
              "profiles": {
                "bugfix": {
                  "diagnose": { "approval": false },
                  "plan": { "approval": true, "checklist": "checks/team-ready.md" }
                }
              }
            }
            """,
            KitStorePaths.RootPath, KitStorePaths.GatesFileName);
        var store = TestKitStores.Create(kitStorePath);
        var provider = new GatesConfigTransitionGateProvider(new GatesConfigReader(new BuiltInWorkflowProfileCatalog()));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var diagnoseGate = await provider.GetGateAsync(store, BuiltInWorkflowProfiles.Bugfix,
            WorkflowPhaseNames.Diagnose, cancellationToken);
        var planGate = await provider.GetGateAsync(store, BuiltInWorkflowProfiles.Bugfix, WorkflowPhaseNames.Plan,
            cancellationToken);
        var finishGate = await provider.GetGateAsync(store, BuiltInWorkflowProfiles.Bugfix,
            WorkflowPhaseNames.Finish, cancellationToken);

        // Assert
        diagnoseGate
            .Should().Be(new TransitionGate(false, null));

        planGate
            .Should().Be(new TransitionGate(true, "checks/team-ready.md"));

        finishGate
            .Should().BeSameAs(TransitionGate.Default);
    }

    private static GatesConfig CreateConfig(string profileName, string phaseName, GateConfig gate)
    {
        return new GatesConfig
        {
            Profiles = new Dictionary<string, Dictionary<string, GateConfig>>
            {
                [profileName] = new Dictionary<string, GateConfig> { [phaseName] = gate }
            }
        };
    }

    private void SetUpReader(GatesConfig config)
    {
        A.CallTo(() => _reader.ReadAsync(_store, A<CancellationToken>._))
            .Returns(new GatesConfigReadResult(config, []));
    }
}
