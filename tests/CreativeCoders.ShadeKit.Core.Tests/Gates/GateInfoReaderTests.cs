using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

/// <summary>
/// Tests <see cref="GateInfoReader" /> with a real <see cref="KitStore" />, a real <see cref="FeatureStore" />, and a
/// real <see cref="GatesConfigTransitionGateProvider" />; only the resolver of the current feature, the git
/// information, and the feature id factory are faked, so no test starts <c>git</c>.
/// </summary>
public sealed class GateInfoReaderTests : IDisposable
{
    private const string OtherFeatureId = "gh-7-export-csv";

    private const string Checklist = "checks/definition-of-ready.md";

    private const string ChecklistPath = ".shadekit/features/gh-142-login-sso/checks/definition-of-ready.md";

    private static readonly WorkflowProfile Feature = BuiltInWorkflowProfiles.Feature;

    private readonly PhaseWorkflowTestContext _context = new PhaseWorkflowTestContext();

    public void Dispose()
    {
        _context.Dispose();
    }

    private static string FeatureInMap => PhaseFields(Feature, WorkflowPhaseNames.Map, null,
        ["approved", "approved", "in-progress"]);

    private static string FeatureDone => PhaseFields(Feature, WorkflowPhaseNames.Finish, null,
        ["approved", "approved", "approved", "approved", "approved", "approved", "approved"]);

    public static TheoryData<string> FirstPhaseProfiles { get; } = new TheoryData<string>
    {
        WorkflowProfileNames.Feature,
        WorkflowProfileNames.Bugfix,
        WorkflowProfileNames.Quick
    };

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_GateWithChecklist_ReturnsGateWithChecklistPathsAndExistence()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("""{ "feature": { "plan": { "approval": true, "checklist": "checks/definition-of-ready.md" } } }""");
        _context.WriteKitFile("# Definition of Ready\n", KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Plan, Approval: true, Checklist, ChecklistPath,
                ChecklistExists: true));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_ChecklistCopyMissing_ReturnsExistsFalse()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("""{ "feature": { "plan": { "approval": true, "checklist": "checks/definition-of-ready.md" } } }""");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Plan, Approval: true, Checklist, ChecklistPath,
                ChecklistExists: false));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_DirectoryInPlaceOfChecklistCopy_ReturnsExistsFalse()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("""{ "feature": { "plan": { "approval": true, "checklist": "checks/definition-of-ready.md" } } }""");
        _context.Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate!.ChecklistExists
            .Should().BeFalse();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_PhaseWithoutGateEntry_ReturnsDefaultGate()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("""{ "feature": { "plan": { "approval": false, "checklist": "checks/definition-of-ready.md" } } }""");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Map, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Map, Approval: true, Checklist: null,
                ChecklistPath: null, ChecklistExists: null));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_GateWithoutApproval_ReturnsApprovalFalse()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("""{ "feature": { "verify": { "approval": false } } }""");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Verify, Approval: false, Checklist: null,
                ChecklistPath: null, ChecklistExists: null));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_GatesFileMissing_ReturnsDefaultGateWithDefaultsUsedWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Plan, Approval: true,
                GatesConfig.DefinitionOfReadyChecklist, ChecklistPath, ChecklistExists: false));

        result.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_GateOfPhaseAlreadyApproved_ReturnsConfiguredGate()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("""{ "feature": { "refine": { "approval": false } } }""");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Refine, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Refine, Approval: false, Checklist: null,
                ChecklistPath: null, ChecklistExists: null));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_Feature_ReturnsProfilePhasesInOrderWithState()
    {
        // Arrange
        _context.WriteFeature(
            """
            "currentPhase": "map", "phases": { "map": "in-progress", "legacy": "approved", "new": "approved", "refine": "approved" }, "history": []
            """);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Map, FeatureId);

        // Assert
        result.FeatureId
            .Should().Be(FeatureId);

        result.Profile
            .Should().Be(WorkflowProfileNames.Feature);

        result.Paths
            .Should().Be(FeaturePaths.For(FeatureId));

        result.Paths.FeatureDir
            .Should().Be(".shadekit/features/gh-142-login-sso/");

        result.Phases
            .Should().Equal(
                new PhaseSnapshot(WorkflowPhaseNames.New, PhaseState.Approved),
                new PhaseSnapshot(WorkflowPhaseNames.Refine, PhaseState.Approved),
                new PhaseSnapshot(WorkflowPhaseNames.Map, PhaseState.InProgress),
                new PhaseSnapshot(WorkflowPhaseNames.Plan, PhaseState.Pending),
                new PhaseSnapshot(WorkflowPhaseNames.Implement, PhaseState.Pending),
                new PhaseSnapshot(WorkflowPhaseNames.Verify, PhaseState.Pending),
                new PhaseSnapshot(WorkflowPhaseNames.Finish, PhaseState.Pending));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_FeatureWithoutPhaseFields_ReturnsInitialStateOfProfile()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Quick);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Quick, FeatureId);

        // Assert
        result.Phases
            .Should().Equal(
                new PhaseSnapshot(WorkflowPhaseNames.New, PhaseState.InProgress),
                new PhaseSnapshot(WorkflowPhaseNames.Quick, PhaseState.Pending),
                new PhaseSnapshot(WorkflowPhaseNames.Finish, PhaseState.Pending));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_ConfigWithCoverageThreshold_ReturnsThreshold()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("{}");
        WriteConfig(""" "coverageThreshold": 82.5 """);

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.CoverageThreshold
            .Should().Be(82.5);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_ConfigWithoutThreshold_ReturnsNull()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("{}");
        WriteConfig(""" "defaultProfile": "feature" """);

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.CoverageThreshold
            .Should().BeNull();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_ConfigMissing_ReturnsNullThreshold()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.CoverageThreshold
            .Should().BeNull();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_ConfigInvalid_ThrowsInvalidKitFile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("{}");
        WriteConfig(""" "coverageThreshold": "80" """);

        // Act
        var act = () => ReadAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        await act
            .Should().ThrowAsync<InvalidKitFileException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_FeatureIdMismatchAndGatesFileMissing_ReturnsFeatureWarningsThenGatesWarnings()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap, recordedId: OtherFeatureId);

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Warnings
            .Should().Equal(
                new FeatureIdMismatchWarning(FeatureId, OtherFeatureId),
                new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_CurrentFeatureWithResolverWarning_ReturnsResolverWarning()
    {
        // Arrange
        var resolverWarning = new CurrentFeatureMismatchWarning(FeatureId, OtherFeatureId);
        _context.SelectFeature(FeatureId, resolverWarning);
        _context.WriteFeature(FeatureInMap);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, featureId: null);

        // Assert
        result.Warnings
            .Should().Equal(resolverWarning);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_AnyFeature_ChangesNoFile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteOverride(FeatureId);
        WriteGates("""{ "feature": { "plan": { "approval": true, "checklist": "checks/definition-of-ready.md" } } }""");
        WriteConfig(""" "coverageThreshold": 80 """);
        _context.WriteKitFile("# Definition of Ready: {{feature-id}}\n", KitStorePaths.TemplatesDirName,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        await ReadAsync(WorkflowPhaseNames.Plan, featureId: null);
        await ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task ReadAsync_WithFeatureId_ReadsThatFeature()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteFeature(FeatureDone, status: "done", featureId: OtherFeatureId);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, OtherFeatureId);

        // Assert
        result.FeatureId
            .Should().Be(OtherFeatureId);

        result.Paths
            .Should().Be(FeaturePaths.For(OtherFeatureId));

        result.Phases
            .Should().AllSatisfy(x => x.State.Should().Be(PhaseState.Approved));

        A.CallTo(() => _context.CurrentFeatureResolver.ResolveAsync(A<IKitStore>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task ReadAsync_WithoutFeatureId_ReadsCurrentFeature()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteFeature(FeatureDone, status: "done", featureId: OtherFeatureId);
        _context.SelectFeature(OtherFeatureId);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Plan, featureId: null);

        // Assert
        result.FeatureId
            .Should().Be(OtherFeatureId);

        result.Paths
            .Should().Be(FeaturePaths.For(OtherFeatureId));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task ReadAsync_UnknownFeature_ThrowsFeatureNotFound()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);

        // Act
        var act = () => ReadAsync(WorkflowPhaseNames.Plan, OtherFeatureId);

        // Assert
        (await act
                .Should().ThrowAsync<FeatureNotFoundException>())
            .Which.FeatureId
            .Should().Be(OtherFeatureId);
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC5")]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("Login SSO")]
    [InlineData("")]
    public async Task ReadAsync_InvalidFeatureId_ThrowsInvalidFeatureId(string featureId)
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);

        // Act
        var act = () => ReadAsync(WorkflowPhaseNames.Plan, featureId);

        // Assert
        await act
            .Should().ThrowAsync<InvalidFeatureIdException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task ReadAsync_NoCurrentFeature_ThrowsNoCurrentFeature()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.SelectFeature(null);

        // Act
        var act = () => ReadAsync(WorkflowPhaseNames.Plan, featureId: null);

        // Assert
        await act
            .Should().ThrowAsync<NoCurrentFeatureException>();
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC6")]
    [MemberData(nameof(FirstPhaseProfiles))]
    public async Task ReadAsync_FirstPhaseOfProfile_ReturnsNullGate(string profile)
    {
        // Arrange
        _context.WriteFeature(string.Empty, profile);
        WriteGates("{}");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.New, FeatureId);

        // Assert
        result.Gate
            .Should().BeNull();

        result.Profile
            .Should().Be(profile);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ReadAsync_PhaseNotInProfile_ThrowsPhaseNotInProfile()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Bugfix);
        WriteGates("{}");

        // Act
        var act = () => ReadAsync(WorkflowPhaseNames.Map, FeatureId);

        // Assert
        var exception = (await act
                .Should().ThrowAsync<PhaseNotInProfileException>())
            .Which;

        exception.Phase
            .Should().Be(WorkflowPhaseNames.Map);

        exception.Profile
            .Should().Be(WorkflowProfileNames.Bugfix);

        exception.ProfilePhases
            .Should().Equal(BuiltInWorkflowProfiles.Bugfix.Phases.Select(x => x.Name));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ReadAsync_PhaseNotInProfileAndGatesAndConfigInvalid_ThrowsPhaseNotInProfile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteKitFile("{ not json", KitStorePaths.GatesFileName);
        _context.WriteKitFile("{ not json", KitStorePaths.ConfigFileName);

        // Act
        var act = () => ReadAsync("Plan", FeatureId);

        // Assert
        await act
            .Should().ThrowAsync<PhaseNotInProfileException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ReadAsync_InvalidPhaseStateAndPhaseNotInProfile_ThrowsInvalidPhaseState()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "unknown", null, ["approved"]));

        // Act
        var act = () => ReadAsync("unknown", FeatureId);

        // Assert
        await act
            .Should().ThrowAsync<InvalidPhaseStateException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ReadAsync_GatesInvalidAndConfigInvalid_ThrowsForGatesFile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteKitFile("{ not json", KitStorePaths.GatesFileName);
        WriteConfig(""" "coverageThreshold": "80" """);

        // Act
        var act = () => ReadAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        (await act
                .Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath
            .Should().Be(".shadekit/gates.json");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task ReadAsync_DoneFeature_ReturnsInfo()
    {
        // Arrange
        _context.WriteFeature(FeatureDone, status: "done");
        WriteGates("""{ "feature": { "finish": { "approval": true, "checklist": "checks/definition-of-done.md" } } }""");
        _context.WriteKitFile("# Definition of Done\n", KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName, "definition-of-done.md");

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Finish, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Finish, Approval: true,
                GatesConfig.DefinitionOfDoneChecklist,
                ".shadekit/features/gh-142-login-sso/checks/definition-of-done.md", ChecklistExists: true));

        result.Phases
            .Should().HaveCount(Feature.Phases.Count)
            .And.AllSatisfy(x => x.State.Should().Be(PhaseState.Approved));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task ReadAsync_SharedChecklistIgnored_ReturnsGateWithoutChecklistAndIgnoredWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates(
            """
            { "feature": {
                "plan": { "approval": true, "checklist": "checks/definition-of-ready.md" },
                "verify": { "approval": false, "checklist": "checks/definition-of-ready.md" } } }
            """);

        // Act
        var result = await ReadAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(WorkflowPhaseNames.Verify, Approval: false, Checklist: null,
                ChecklistPath: null, ChecklistExists: null));

        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(WorkflowProfileNames.Feature, WorkflowPhaseNames.Verify,
                GateEntryIgnoredReason.SharedChecklist));
    }

    [Fact]
    public async Task ReadAsync_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        WriteGates("{}");
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => CreateReader().ReadAsync(_context.CreateStore(), WorkflowPhaseNames.Plan, FeatureId,
            cancellationTokenSource.Token);

        // Assert
        await act
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ReadAsync_NullStore_ThrowsArgumentNull()
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadAsync(null!, WorkflowPhaseNames.Plan, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReadAsync_NullPhase_ThrowsArgumentNull()
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var act = () => reader.ReadAsync(_context.CreateStore(), null!, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act
            .Should().ThrowAsync<ArgumentNullException>();
    }

    private Task<GateInfoResult> ReadAsync(string phase, string? featureId)
    {
        return CreateReader().ReadAsync(_context.CreateStore(), phase, featureId,
            TestContext.Current.CancellationToken);
    }

    private GateInfoReader CreateReader()
    {
        var catalog = new BuiltInWorkflowProfileCatalog();
        var featureStore = new FeatureStore(A.Fake<IFeatureIdFactory>(), _context.CurrentFeatureResolver,
            _context.GitRepositoryInfo, catalog, new PhaseEngine());

        return new GateInfoReader(featureStore, new GatesConfigTransitionGateProvider(catalog));
    }

    private void WriteGates(string profiles)
    {
        _context.WriteKitFile(TestGatesJson.WithProfiles(profiles), KitStorePaths.GatesFileName);
    }

    private void WriteConfig(string fields)
    {
        _context.WriteKitFile($$"""{ "schemaVersion": 1, "minEngineVersion": "0.1.0", {{fields}} }""",
            KitStorePaths.ConfigFileName);
    }
}
