using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Warnings;
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
    public async Task GetGatesAsync_GateWithApprovalAndChecklist_ReturnsGateWithChecklistUnchanged()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Plan,
            new GateConfig { Approval = true, Checklist = "checks/team/ready.md" }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(true, "checks/team/ready.md"));
    }

    [Fact]
    public async Task GetGatesAsync_GateWithoutApproval_ReturnsGateWithoutApproval()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Map,
            new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[WorkflowPhaseNames.Map]
            .Should().Be(new TransitionGate(false, null));
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature)]
    [InlineData(WorkflowProfileNames.Bugfix)]
    [InlineData(WorkflowProfileNames.Quick)]
    public async Task GetGatesAsync_AnyProfile_ReturnsGateOfEveryPhaseOfProfileInProfileOrder(string profileName)
    {
        // Arrange
        SetUpReader(GatesConfig.CreateDefault());
        var profile = new BuiltInWorkflowProfileCatalog().GetProfile(profileName);
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, profile, TestContext.Current.CancellationToken);

        // Assert
        result.Gates.Keys
            .Should().Equal(profile.Phases.Select(phase => phase.Name));
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGatesAsync_PhaseWithoutEntry_ReturnsDefaultGate()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Plan,
            new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[WorkflowPhaseNames.Refine]
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGatesAsync_ProfileWithoutEntry_ReturnsDefaultGateForEveryPhase()
    {
        // Arrange
        SetUpReader(CreateConfig(WorkflowProfileNames.Feature, WorkflowPhaseNames.Plan,
            new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Bugfix,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates.Values
            .Should().AllSatisfy(gate => gate.Should().BeSameAs(TransitionGate.Default));
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGatesAsync_ProfileWithoutGates_ReturnsDefaultGateForEveryPhase()
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
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates.Values
            .Should().AllSatisfy(gate => gate.Should().BeSameAs(TransitionGate.Default));
    }

    [Fact]
    public async Task GetGatesAsync_EmptyProfiles_ReturnsDefaultGateForEveryPhase()
    {
        // Arrange
        SetUpReader(new GatesConfig { Profiles = [] });
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates.Values
            .Should().AllSatisfy(gate => gate.Should().BeSameAs(TransitionGate.Default));
    }

    [Theory]
    [InlineData("Feature", WorkflowPhaseNames.Plan)]
    [InlineData(WorkflowProfileNames.Feature, "Plan")]
    public async Task GetGatesAsync_NamesDifferOnlyInCase_ReturnsDefaultGate(string profileName, string phaseName)
    {
        // Arrange
        SetUpReader(CreateConfig(profileName, phaseName, new GateConfig { Approval = false }));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[WorkflowPhaseNames.Plan]
            .Should().BeSameAs(TransitionGate.Default);
    }

    [Theory]
    [InlineData(WorkflowPhaseNames.Quick, false, null)]
    [InlineData(WorkflowPhaseNames.Finish, true, GatesConfig.DefinitionOfDoneChecklist)]
    public async Task GetGatesAsync_DefaultConfigQuickProfile_ReturnsDefaultGatesOfQuickProfile(string targetPhase,
        bool expectedApproval, string? expectedChecklist)
    {
        // Arrange
        SetUpReader(GatesConfig.CreateDefault());
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[targetPhase]
            .Should().Be(new TransitionGate(expectedApproval, expectedChecklist));
    }

    [Fact]
    public async Task GetGatesAsync_ReaderReturnsWarnings_ReturnsWarningsUnchangedInOrder()
    {
        // Arrange
        KitWarning[] warnings =
        [
            new GatesDefaultsUsedWarning(IsFileMissing: false),
            new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile)
        ];
        SetUpReader(GatesConfig.CreateDefault(), warnings);
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var result = await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(warnings);
    }

    [Fact]
    public async Task GetGatesAsync_AnyProfile_ReadsConfigOfGivenStoreOnceWithGivenToken()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        SetUpReader(GatesConfig.CreateDefault());
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        await provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        A.CallTo(() => _reader.ReadAsync(_store, cancellationToken))
            .MustHaveHappenedOnceExactly();

        A.CallTo(_reader)
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGatesAsync(null!, BuiltInWorkflowProfiles.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");

        A.CallTo(_reader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetGatesAsync_NullProfile_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGatesAsync(_store, null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("profile");

        A.CallTo(_reader)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetGatesAsync_ReaderCanceled_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        A.CallTo(() => _reader.ReadAsync(_store, cancellationTokenSource.Token))
            .ThrowsAsync(new OperationCanceledException(cancellationTokenSource.Token));
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            cancellationTokenSource.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetGatesAsync_ReaderThrows_PropagatesException()
    {
        // Arrange
        var exception = new InvalidKitFileException(".shadekit/gates.json", "The file is invalid.");
        A.CallTo(() => _reader.ReadAsync(_store, A<CancellationToken>._))
            .ThrowsAsync(exception);
        var provider = new GatesConfigTransitionGateProvider(_reader);

        // Act
        var act = () => provider.GetGatesAsync(_store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>())
            .Which.Should().BeSameAs(exception);
    }

    [Fact]
    public async Task GetGatesAsync_RealReaderWithGatesFile_ReturnsGatesFromFile()
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

        // Act
        var result = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Bugfix,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[WorkflowPhaseNames.Diagnose]
            .Should().Be(new TransitionGate(false, null));

        result.Gates[WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(true, "checks/team-ready.md"));

        result.Gates[WorkflowPhaseNames.Finish]
            .Should().BeSameAs(TransitionGate.Default);

        result.Warnings
            .Should().BeEmpty();
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

    // The reader returns a read-only view; the view of the configuration is built here as the reader builds it.
    private void SetUpReader(GatesConfig config, IReadOnlyList<KitWarning>? warnings = null)
    {
        var profiles = config.Profiles!.ToDictionary(
            profile => profile.Key,
            IReadOnlyDictionary<string, TransitionGate> (profile) => profile.Value.ToDictionary(gate => gate.Key,
                gate => new TransitionGate(gate.Value.Approval, gate.Value.Checklist)));

        A.CallTo(() => _reader.ReadAsync(_store, A<CancellationToken>._))
            .Returns(new GatesConfigReadResult(profiles, warnings ?? []));
    }
}
