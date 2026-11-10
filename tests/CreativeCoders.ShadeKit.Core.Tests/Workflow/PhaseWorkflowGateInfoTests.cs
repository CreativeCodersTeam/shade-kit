using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Features.Exceptions;
using CreativeCoders.ShadeKit.Core.Features.Warnings;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using FakeItEasy;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.PhaseWorkflowTestContext;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow;

/// <summary>
/// Tests <see cref="PhaseWorkflow.GetGateInfoAsync" /> with a real <see cref="KitStore" /> and a real
/// <see cref="GatesConfigTransitionGateProvider" />; only the resolver of the current feature and the git information
/// are faked, so no test starts <c>git</c>.
/// </summary>
public sealed class PhaseWorkflowGateInfoTests : IDisposable
{
    private const string OtherFeatureId = "gh-7-export-csv";

    private const string Checklist = "checks/definition-of-ready.md";

    private const string ChecklistPath = ".shadekit/features/gh-142-login-sso/checks/definition-of-ready.md";

    private const string PlanGateWithChecklist =
        $$"""{ "feature": { "plan": { "approval": true, "checklist": "{{Checklist}}" } } }""";

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

    // gates.json profiles, requested phase, expected approval; none of these gates has a checklist.
    public static TheoryData<string, string, bool> GatesWithoutChecklist { get; } =
        new TheoryData<string, string, bool>
        {
            // A phase without an entry has the default gate.
            { $$"""{ "feature": { "plan": { "approval": false, "checklist": "{{Checklist}}" } } }""", "map", true },
            { """{ "feature": { "verify": { "approval": false } } }""", "verify", false },
            // The gate of an approved phase is its configured gate.
            { """{ "feature": { "refine": { "approval": false } } }""", "refine", false }
        };

    [Theory]
    [Trait("Requirement", "FP06-4-AC4")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetGateInfoAsync_GateWithChecklist_ReturnsGateWithChecklistPathsAndExistence(bool copyExists)
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates(PlanGateWithChecklist);

        if (copyExists)
        {
            _context.WriteKitFile("# Definition of Ready\n", KitStorePaths.FeaturesDirName, FeatureId,
                KitStorePaths.ChecksDirName, "definition-of-ready.md");
        }

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(new PhaseGate(WorkflowPhaseNames.Plan, new TransitionGate(true, Checklist)),
                ChecklistPath, copyExists));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_DirectoryInPlaceOfChecklistCopy_ReturnsExistsFalse()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates(PlanGateWithChecklist);
        _context.Repository.CreateDirectory(KitStorePaths.RootPath, KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate!.ChecklistExists
            .Should().BeFalse();
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC4")]
    [MemberData(nameof(GatesWithoutChecklist))]
    public async Task GetGateInfoAsync_GateWithoutChecklist_ReturnsConfiguredGateWithoutChecklistPaths(
        string profiles, string phase, bool approval)
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates(profiles);

        // Act
        var result = await GetGateInfoAsync(phase, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(new PhaseGate(phase, new TransitionGate(approval, Checklist: null)),
                ChecklistPath: null, ChecklistExists: null));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_GatesFileMissing_ReturnsDefaultGateWithDefaultsUsedWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(
                new PhaseGate(WorkflowPhaseNames.Plan, new TransitionGate(true, GatesConfig.DefinitionOfReadyChecklist)),
                ChecklistPath, ChecklistExists: false));

        result.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_PendingTransitionIntoPhase_ReturnsConfiguredApproval()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, WorkflowPhaseNames.Map, WorkflowPhaseNames.Plan,
            ["approved", "approved", "awaiting-approval"]));
        _context.WriteGates("""{ "feature": { "plan": { "approval": false } } }""");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Gate!.Gate.Gate
            .Should().Be(new TransitionGate(Approval: false, Checklist: null));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_Feature_ReturnsPhasesInOrderWithStateAndArtifacts()
    {
        // Arrange
        _context.WriteFeature(
            """
            "currentPhase": "map", "phases": { "map": "in-progress", "legacy": "approved", "new": "approved", "refine": "approved" }, "history": []
            """);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Map, FeatureId);

        // Assert
        result.FeatureId
            .Should().Be(FeatureId);

        result.Profile
            .Should().Be(WorkflowProfileNames.Feature);

        result.Paths.FeatureDir
            .Should().Be(".shadekit/features/gh-142-login-sso/");

        result.Phases
            .Select(x => (x.Name, x.State))
            .Should().Equal(
                (WorkflowPhaseNames.New, PhaseState.Approved),
                (WorkflowPhaseNames.Refine, PhaseState.Approved),
                (WorkflowPhaseNames.Map, PhaseState.InProgress),
                (WorkflowPhaseNames.Plan, PhaseState.Pending),
                (WorkflowPhaseNames.Implement, PhaseState.Pending),
                (WorkflowPhaseNames.Verify, PhaseState.Pending),
                (WorkflowPhaseNames.Finish, PhaseState.Pending),
                ("legacy", PhaseState.Approved));

        result.Phases
            .Select(x => x.Artifacts)
            .Should().Equal([.. Feature.Phases.Select(x => x.Artifacts), []],
                (actual, expected) => actual.SequenceEqual(expected));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_FeatureWithoutPhaseFields_ReturnsInitialStateOfProfile()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Quick);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Quick, FeatureId);

        // Assert
        result.Phases
            .Select(x => (x.Name, x.State))
            .Should().Equal(
                (WorkflowPhaseNames.New, PhaseState.InProgress),
                (WorkflowPhaseNames.Quick, PhaseState.Pending),
                (WorkflowPhaseNames.Finish, PhaseState.Pending));
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC4")]
    [InlineData(""", "coverageThreshold": 82.5 """, 82.5)]
    [InlineData(""", "defaultProfile": "feature" """, null)]
    public async Task GetGateInfoAsync_Config_ReturnsCoverageThreshold(string fields, double? expected)
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates("{}");
        _context.WriteConfig(fields);

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.CoverageThreshold
            .Should().Be(expected);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_ConfigMissing_ReturnsNullThreshold()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.CoverageThreshold
            .Should().BeNull();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_ConfigInvalid_ThrowsInvalidKitFile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates("{}");
        _context.WriteConfig(""", "coverageThreshold": "80" """);

        // Act
        var act = () => GetGateInfoAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        await act
            .Should().ThrowAsync<InvalidKitFileException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_FeatureIdMismatchAndGatesFileMissing_ReturnsFeatureWarningsThenGatesWarnings()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap, recordedId: OtherFeatureId);

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        result.Warnings
            .Should().Equal(
                new FeatureIdMismatchWarning(FeatureId, OtherFeatureId),
                new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_CurrentFeatureWithResolverWarning_ReturnsResolverWarning()
    {
        // Arrange
        var resolverWarning = new CurrentFeatureMismatchWarning(FeatureId, OtherFeatureId);
        _context.SelectFeature(FeatureId, resolverWarning);
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, featureId: null);

        // Assert
        result.Warnings
            .Should().Equal(resolverWarning);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_AnyFeature_ChangesNoFile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteOverride(FeatureId);
        _context.WriteGates(PlanGateWithChecklist);
        _context.WriteConfig(""", "coverageThreshold": 80 """);
        _context.WriteKitFile("# Definition of Ready: {{feature-id}}\n", KitStorePaths.TemplatesDirName,
            KitStorePaths.ChecksDirName, "definition-of-ready.md");
        var snapshot = _context.Repository.TakeSnapshot();

        // Act
        await GetGateInfoAsync(WorkflowPhaseNames.Plan, featureId: null);
        await GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        _context.Repository.TakeSnapshot()
            .Should().BeEquivalentTo(snapshot);

        _context.VerifyChecklistNotCopied();
        _context.VerifyIdentityNotRead();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GetGateInfoAsync_WithFeatureId_ReadsThatFeature()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteFeature(FeatureDone, status: "done", featureId: OtherFeatureId);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, OtherFeatureId);

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
    public async Task GetGateInfoAsync_WithoutFeatureId_ReadsCurrentFeature()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteFeature(FeatureDone, status: "done", featureId: OtherFeatureId);
        _context.SelectFeature(OtherFeatureId);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Plan, featureId: null);

        // Assert
        result.FeatureId
            .Should().Be(OtherFeatureId);

        result.Paths
            .Should().Be(FeaturePaths.For(OtherFeatureId));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GetGateInfoAsync_UnknownFeature_ThrowsFeatureNotFound()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);

        // Act
        var act = () => GetGateInfoAsync(WorkflowPhaseNames.Plan, OtherFeatureId);

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
    public async Task GetGateInfoAsync_InvalidFeatureId_ThrowsInvalidFeatureId(string featureId)
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);

        // Act
        var act = () => GetGateInfoAsync(WorkflowPhaseNames.Plan, featureId);

        // Assert
        await act
            .Should().ThrowAsync<InvalidFeatureIdException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC5")]
    public async Task GetGateInfoAsync_NoCurrentFeature_ThrowsNoCurrentFeature()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.SelectFeature(null);

        // Act
        var act = () => GetGateInfoAsync(WorkflowPhaseNames.Plan, featureId: null);

        // Assert
        await act
            .Should().ThrowAsync<NoCurrentFeatureException>();
    }

    [Theory]
    [Trait("Requirement", "FP06-4-AC6")]
    [MemberData(nameof(FirstPhaseProfiles))]
    public async Task GetGateInfoAsync_FirstPhaseOfProfile_ReturnsNullGate(string profile)
    {
        // Arrange
        _context.WriteFeature(string.Empty, profile);
        _context.WriteGates("{}");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.New, FeatureId);

        // Assert
        result.Gate
            .Should().BeNull();

        result.Profile
            .Should().Be(profile);
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GetGateInfoAsync_PhaseNotInProfile_ThrowsPhaseNotInProfile()
    {
        // Arrange
        _context.WriteFeature(string.Empty, WorkflowProfileNames.Bugfix);
        _context.WriteGates("{}");

        // Act
        var act = () => GetGateInfoAsync(WorkflowPhaseNames.Map, FeatureId);

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
    public async Task GetGateInfoAsync_PhaseNotInProfileAndGatesAndConfigInvalid_ThrowsPhaseNotInProfile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteKitFile("{ not json", KitStorePaths.GatesFileName);
        _context.WriteKitFile("{ not json", KitStorePaths.ConfigFileName);

        // Act
        var act = () => GetGateInfoAsync("Plan", FeatureId);

        // Assert
        await act
            .Should().ThrowAsync<PhaseNotInProfileException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GetGateInfoAsync_InvalidPhaseStateAndPhaseNotInProfile_ThrowsInvalidPhaseState()
    {
        // Arrange
        _context.WriteFeature(PhaseFields(Feature, "unknown", null, ["approved"]));

        // Act
        var act = () => GetGateInfoAsync("unknown", FeatureId);

        // Assert
        await act
            .Should().ThrowAsync<InvalidPhaseStateException>();
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GetGateInfoAsync_GatesInvalidAndConfigInvalid_ThrowsForGatesFile()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteKitFile("{ not json", KitStorePaths.GatesFileName);
        _context.WriteConfig(""", "coverageThreshold": "80" """);

        // Act
        var act = () => GetGateInfoAsync(WorkflowPhaseNames.Plan, FeatureId);

        // Assert
        (await act
                .Should().ThrowAsync<InvalidKitFileException>())
            .Which.FilePath
            .Should().Be(".shadekit/gates.json");
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC6")]
    public async Task GetGateInfoAsync_DoneFeature_ReturnsInfo()
    {
        // Arrange
        _context.WriteFeature(FeatureDone, status: "done");
        _context.WriteGates(
            """{ "feature": { "finish": { "approval": true, "checklist": "checks/definition-of-done.md" } } }""");
        _context.WriteKitFile("# Definition of Done\n", KitStorePaths.FeaturesDirName, FeatureId,
            KitStorePaths.ChecksDirName, "definition-of-done.md");

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Finish, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(
                new PhaseGate(WorkflowPhaseNames.Finish, new TransitionGate(true, GatesConfig.DefinitionOfDoneChecklist)),
                ".shadekit/features/gh-142-login-sso/checks/definition-of-done.md", ChecklistExists: true));

        result.Phases
            .Should().HaveCount(Feature.Phases.Count)
            .And.AllSatisfy(x => x.State.Should().Be(PhaseState.Approved));
    }

    [Fact]
    [Trait("Requirement", "FP06-4-AC4")]
    public async Task GetGateInfoAsync_SharedChecklistIgnored_ReturnsGateWithoutChecklistAndIgnoredWarning()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates(
            $$"""
              { "feature": {
                  "plan": { "approval": true, "checklist": "{{Checklist}}" },
                  "verify": { "approval": false, "checklist": "{{Checklist}}" } } }
              """);

        // Act
        var result = await GetGateInfoAsync(WorkflowPhaseNames.Verify, FeatureId);

        // Assert
        result.Gate
            .Should().Be(new GateInfoGate(new PhaseGate(WorkflowPhaseNames.Verify, new TransitionGate(false, null)),
                ChecklistPath: null, ChecklistExists: null));

        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(WorkflowProfileNames.Feature, WorkflowPhaseNames.Verify,
                GateEntryIgnoredReason.SharedChecklist));
    }

    [Fact]
    public async Task GetGateInfoAsync_CanceledToken_ThrowsOperationCanceled()
    {
        // Arrange
        _context.WriteFeature(FeatureInMap);
        _context.WriteGates("{}");
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        var act = () => CreateWorkflow().GetGateInfoAsync(_context.CreateStore(), WorkflowPhaseNames.Plan, FeatureId,
            cancellationTokenSource.Token);

        // Assert
        await act
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetGateInfoAsync_NullStore_ThrowsArgumentNull()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var act = () => workflow.GetGateInfoAsync(null!, WorkflowPhaseNames.Plan, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetGateInfoAsync_NullPhase_ThrowsArgumentNull()
    {
        // Arrange
        var workflow = CreateWorkflow();

        // Act
        var act = () => workflow.GetGateInfoAsync(_context.CreateStore(), null!, FeatureId,
            TestContext.Current.CancellationToken);

        // Assert
        await act
            .Should().ThrowAsync<ArgumentNullException>();
    }

    private Task<GateInfoResult> GetGateInfoAsync(string phase, string? featureId)
    {
        return CreateWorkflow().GetGateInfoAsync(_context.CreateStore(), phase, featureId,
            TestContext.Current.CancellationToken);
    }

    private PhaseWorkflow CreateWorkflow()
    {
        return _context.CreateWorkflow(new GatesConfigTransitionGateProvider(new BuiltInWorkflowProfileCatalog()));
    }
}
