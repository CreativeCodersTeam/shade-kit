using System.Diagnostics;
using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Formats;
using CreativeCoders.ShadeKit.Core.Formats.Exceptions;
using CreativeCoders.ShadeKit.Core.Formats.Warnings;
using CreativeCoders.ShadeKit.Core.Gates;
using CreativeCoders.ShadeKit.Core.Gates.Warnings;
using CreativeCoders.ShadeKit.Core.Store;
using CreativeCoders.ShadeKit.Core.Tests.Features;
using CreativeCoders.ShadeKit.Core.Tests.Store;
using CreativeCoders.ShadeKit.Core.Workflow;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Tests.Shared;
using FakeItEasy;

namespace CreativeCoders.ShadeKit.Core.Tests.Gates;

public sealed class GatesConfigTransitionGateProviderTests : IDisposable
{
    private const string GatesDisplayPath = ".shadekit/gates.json";

    private static readonly TimeSpan ReadStartTimeout = TimeSpan.FromSeconds(10);

    private readonly TempKitDirectory _repository = new TempKitDirectory();

    private readonly string _kitStorePath;

    public GatesConfigTransitionGateProviderTests()
    {
        _repository.CreateGitRoot();
        _kitStorePath = _repository.CreateDirectory(KitStorePaths.RootPath);
    }

    public void Dispose()
    {
        _repository.Dispose();
    }

    [Fact]
    public void Ctor_NullWorkflowProfileCatalog_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GatesConfigTransitionGateProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("workflowProfileCatalog");
    }

    [Fact]
    public async Task GetGatesAsync_NullStore_ThrowsArgumentNullException()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var act = () => provider.GetGatesAsync(null!, BuiltInWorkflowProfiles.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Fact]
    public async Task GetGatesAsync_NullProfile_ThrowsArgumentNullExceptionWithoutReading()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var provider = CreateProvider();

        // Act
        var act = () => provider.GetGatesAsync(store, null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("profile");

        A.CallTo(store)
            .MustNotHaveHappened();
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature)]
    [InlineData(WorkflowProfileNames.Bugfix)]
    [InlineData(WorkflowProfileNames.Quick)]
    public async Task GetGatesAsync_MissingFile_ReturnsDefaultGatesOfProfileWithFileMissingWarning(
        string profileName)
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), GetProfile(profileName),
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates
            .Should().BeEquivalentTo(DefaultGates(profileName));

        result.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Theory]
    [InlineData(WorkflowPhaseNames.Quick, false, null)]
    [InlineData(WorkflowPhaseNames.Finish, true, GatesConfig.DefinitionOfDoneChecklist)]
    public async Task GetGatesAsync_MissingFileQuickProfile_ReturnsDefaultGatesOfQuickProfile(string targetPhase,
        bool expectedApproval, string? expectedChecklist)
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates[targetPhase]
            .Should().Be(new TransitionGate(expectedApproval, expectedChecklist));
    }

    [Fact]
    public async Task GetGatesAsync_FileWithoutProfilesButOtherFields_ReturnsDefaultsWithFieldMissingWarning()
    {
        // Arrange
        WriteGates($$"""{ {{TestGatesJson.Header}}, "team": "core" }""");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates
            .Should().BeEquivalentTo(DefaultGates(WorkflowProfileNames.Feature));

        result.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: false));
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGatesAsync_ExplicitEmptyProfiles_ReturnsNoGatesWithoutWarning()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates
            .Should().BeEmpty();

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetGatesAsync_KnownEntries_ReturnsEntriesOfProfileAsReadWithoutWarning()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "feature": { "plan": { "approval": false, "checklist": "checks/team/ready.md" } },
              "quick": { "quick": { "approval": true }, "finish": { } }
            }
            """);
        var provider = CreateProvider();
        var store = CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var feature = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var quick = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Quick, cancellationToken);

        // Assert
        feature.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Plan);

        feature.Gates[WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(Approval: false, Checklist: "checks/team/ready.md"));

        quick.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Quick, WorkflowPhaseNames.Finish);

        quick.Gates[WorkflowPhaseNames.Finish]
            .Should().Be(new TransitionGate(Approval: true, Checklist: null));

        feature.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("Requirement", "FP05-AC6")]
    public async Task GetGatesAsync_PhasesWithoutEntry_ReturnsOnlyConfiguredGatesOfProfile()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "bugfix": {
                "diagnose": { "approval": false },
                "plan": { "approval": true, "checklist": "checks/team-ready.md" }
              }
            }
            """);
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Bugfix,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Diagnose, WorkflowPhaseNames.Plan);

        result.Gates[WorkflowPhaseNames.Diagnose]
            .Should().Be(new TransitionGate(false, null));

        result.Gates[WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(true, "checks/team-ready.md"));

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [Trait("Requirement", "FP05-AC6")]
    [InlineData("""{ "feature": { "plan": { "approval": false } } }""")]
    [InlineData("""{ "bugfix": { } }""")]
    public async Task GetGatesAsync_ProfileWithoutGates_ReturnsNoGatesWithoutWarning(string profiles)
    {
        // Arrange
        WriteGatesWithProfiles(profiles);
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Bugfix,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates
            .Should().BeEmpty();

        result.Warnings
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "deploy": { "approval": true } }""")]
    [InlineData("{}")]
    public async Task GetGatesAsync_UnknownProfile_ReturnsOneWarningForProfileAndNoGates(string gates)
    {
        // Arrange
        WriteGatesWithProfiles($$"""{ "release": {{gates}} }""");
        var provider = CreateProvider();
        var release = new WorkflowProfile("release", BuiltInWorkflowProfiles.Quick.Phases);

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), release, TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        result.Gates
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetGatesAsync_ProfileNameInOtherCase_ReturnsUnknownProfileWarningAndNoGates()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "Feature": { "plan": { "approval": false } } }""");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("Feature", null, GateEntryIgnoredReason.UnknownProfile));

        result.Gates
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Diagnose)]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Refine)]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Plan)]
    [InlineData(WorkflowProfileNames.Feature, "Plan")]
    [InlineData(WorkflowProfileNames.Feature, "deploy")]
    public async Task GetGatesAsync_PhaseNotInProfile_ReturnsUnknownPhaseWarningAndNoGateForEntry(string profileName,
        string phaseName)
    {
        // Arrange
        WriteGatesWithProfiles(
            $$"""{ "{{profileName}}": { "{{phaseName}}": { "approval": false }, "finish": { "approval": false } } }""");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), GetProfile(profileName),
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(profileName, phaseName, GateEntryIgnoredReason.UnknownPhase));

        result.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Finish);
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature)]
    [InlineData(WorkflowProfileNames.Bugfix)]
    [InlineData(WorkflowProfileNames.Quick)]
    public async Task GetGatesAsync_FirstPhaseOfProfile_ReturnsFirstPhaseWarningAndNoGateForEntry(string profileName)
    {
        // Arrange
        WriteGatesWithProfiles($$"""{ "{{profileName}}": { "new": { "approval": false } } }""");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), GetProfile(profileName),
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(profileName, WorkflowPhaseNames.New,
                GateEntryIgnoredReason.FirstPhase));

        result.Gates
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetGatesAsync_SeveralIgnoredEntries_ReturnsWarningsForWholeFileInFileOrder()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "quick": { "plan": { }, "finish": { }, "new": { } },
              "release": { "ship": { } },
              "feature": { "new": { }, "refine": { }, "deploy": { } },
              "bugfix": { "diagnose": { } },
              "archive": { }
            }
            """);
        var provider = CreateProvider();
        var store = CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var bugfix = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Bugfix, cancellationToken);
        var feature = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var quick = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Quick, cancellationToken);

        // Assert
        bugfix.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning("quick", "plan", GateEntryIgnoredReason.UnknownPhase),
                new GateEntryIgnoredWarning("quick", "new", GateEntryIgnoredReason.FirstPhase),
                new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile),
                new GateEntryIgnoredWarning("feature", "new", GateEntryIgnoredReason.FirstPhase),
                new GateEntryIgnoredWarning("feature", "deploy", GateEntryIgnoredReason.UnknownPhase),
                new GateEntryIgnoredWarning("archive", null, GateEntryIgnoredReason.UnknownProfile));

        bugfix.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Diagnose);

        feature.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Refine);

        quick.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Finish);
    }

    [Fact]
    public async Task GetGatesAsync_ExistingFile_ReturnsGatesThatCannotBeChanged()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "feature": { "plan": { "approval": false } } }""");
        var provider = CreateProvider();
        var store = CreateStore();
        var result = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);
        var gates = (IDictionary<string, TransitionGate>)result.Gates;

        // Act
        var changeGate = () => gates[WorkflowPhaseNames.Plan] = TransitionGate.Default;
        var removeGate = () => gates.Remove(WorkflowPhaseNames.Plan);

        // Assert
        changeGate.Should().Throw<NotSupportedException>();
        removeGate.Should().Throw<NotSupportedException>();

        (await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, TestContext.Current.CancellationToken))
            .Gates[WorkflowPhaseNames.Plan]
            .Should().Be(new TransitionGate(Approval: false, Checklist: null));
    }

    [Fact]
    public async Task GetGatesAsync_ProfileWithoutEntry_ReturnsGatesThatCannotBeChanged()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        var provider = CreateProvider();
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);
        var gates = (IDictionary<string, TransitionGate>)result.Gates;

        // Act
        var addGate = () => gates.Add(WorkflowPhaseNames.Plan, TransitionGate.Default);

        // Assert
        addGate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task GetGatesAsync_TwoGatesShareChecklist_WarnsLaterGateAndIgnoresOnlyItsChecklist()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "feature": {
                "implement": { "approval": true, "checklist": "checks/security.md" },
                "finish": { "approval": false, "checklist": "checks/security.md" }
              }
            }
            """);
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(WorkflowProfileNames.Feature, WorkflowPhaseNames.Finish,
                GateEntryIgnoredReason.SharedChecklist));

        result.Gates[WorkflowPhaseNames.Implement]
            .Should().Be(new TransitionGate(Approval: true, Checklist: "checks/security.md"));

        result.Gates[WorkflowPhaseNames.Finish]
            .Should().Be(new TransitionGate(Approval: false, Checklist: null));
    }

    [Theory]
    [InlineData("checks/security.md", "checks/Security.md")]
    [InlineData("checks/team/dor.md", "checks/TEAM/DoR.md")]
    public async Task GetGatesAsync_TwoGatesNameChecklistInOtherCase_WarnsLaterGateAsSharedChecklist(
        string earlierChecklist, string laterChecklist)
    {
        // Arrange
        WriteGatesWithProfiles(
            $$"""
            {
              "feature": {
                "implement": { "checklist": "{{earlierChecklist}}" },
                "finish": { "checklist": "{{laterChecklist}}" }
              }
            }
            """);
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new GateEntryIgnoredWarning(WorkflowProfileNames.Feature, WorkflowPhaseNames.Finish,
                GateEntryIgnoredReason.SharedChecklist));

        result.Gates[WorkflowPhaseNames.Implement]
            .Should().Be(new TransitionGate(Approval: true, Checklist: earlierChecklist));

        result.Gates[WorkflowPhaseNames.Finish]
            .Should().Be(new TransitionGate(Approval: true, Checklist: null));
    }

    [Fact]
    public async Task GetGatesAsync_SharedChecklistInOtherFileOrder_GateIntoEarliestPhaseKeepsChecklist()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "bugfix": {
                "finish": { "checklist": "checks/review.md" },
                "verify": { "checklist": "checks/review.md" },
                "plan": { "checklist": "checks/review.md" }
              }
            }
            """);
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Bugfix,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Verify,
                    GateEntryIgnoredReason.SharedChecklist),
                new GateEntryIgnoredWarning(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Finish,
                    GateEntryIgnoredReason.SharedChecklist));

        result.Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Finish, WorkflowPhaseNames.Verify, WorkflowPhaseNames.Plan);

        result.Gates.Values.Select(gate => gate.Checklist)
            .Should().Equal(null, null, "checks/review.md");
    }

    [Fact]
    public async Task GetGatesAsync_SharedChecklistWithIgnoredEntries_WarnsOnlyGatesThatApplyAfterOtherWarnings()
    {
        // Arrange
        WriteGatesWithProfiles(
            """
            {
              "quick": {
                "new": { "checklist": "checks/x.md" },
                "deploy": { "checklist": "checks/x.md" },
                "finish": { "checklist": "checks/x.md" },
                "quick": { "checklist": "checks/x.md" }
              },
              "release": { "ship": { "checklist": "checks/x.md" }, "done": { "checklist": "checks/x.md" } }
            }
            """);
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Quick,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning(WorkflowProfileNames.Quick, WorkflowPhaseNames.New,
                    GateEntryIgnoredReason.FirstPhase),
                new GateEntryIgnoredWarning(WorkflowProfileNames.Quick, "deploy", GateEntryIgnoredReason.UnknownPhase),
                new GateEntryIgnoredWarning(WorkflowProfileNames.Quick, WorkflowPhaseNames.Finish,
                    GateEntryIgnoredReason.SharedChecklist),
                new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        result.Gates
            .Should().Equal(new Dictionary<string, TransitionGate>
            {
                [WorkflowPhaseNames.Finish] = new(Approval: true, Checklist: null),
                [WorkflowPhaseNames.Quick] = new(Approval: true, Checklist: "checks/x.md")
            });
    }

    [Theory]
    [InlineData("""{ "feature": { "plan": { "checklist": "checks/a.md" } }, "bugfix": { "plan": { "checklist": "checks/a.md" } } }""")]
    [InlineData("""{ "feature": { "plan": { "checklist": "checks/a.md" }, "finish": { "checklist": "checks/team/a.md" } } }""")]
    public async Task GetGatesAsync_ChecklistsDifferOrBelongToOtherProfiles_ReturnsNoSharedChecklistWarning(
        string profiles)
    {
        // Arrange
        WriteGatesWithProfiles(profiles);
        var provider = CreateProvider();
        var store = CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var feature = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var bugfix = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Bugfix, cancellationToken);

        // Assert
        feature.Warnings
            .Should().BeEmpty();

        feature.Gates.Values.Concat(bugfix.Gates.Values)
            .Should().OnlyContain(gate => gate.Checklist != null);
    }

    [Fact]
    public async Task GetGatesAsync_OutdatedFileWithIgnoredEntry_ReturnsFormatWarningBeforeGateWarning()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "release": { } }""");
        var provider = CreateProvider();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var result = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, GatesDisplayPath, 1, 2),
                new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));
    }

    [Fact]
    public async Task GetGatesAsync_OutdatedFileWithoutProfiles_ReturnsFormatWarningBeforeDefaultsWarning()
    {
        // Arrange
        WriteGates(TestGatesJson.HeaderOnly);
        var provider = CreateProvider();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var result = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new FormatWarning(FormatWarningKind.UpgradeNeeded, GatesDisplayPath, 1, 2),
                new GatesDefaultsUsedWarning(IsFileMissing: false));

        result.Gates
            .Should().BeEquivalentTo(DefaultGates(WorkflowProfileNames.Feature));
    }

    [Fact]
    public async Task GetGatesAsync_OutdatedFileWithKnownEntries_ReturnsOnlyFormatWarning()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "feature": { "plan": { } } }""");
        var provider = CreateProvider();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var result = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(new FormatWarning(FormatWarningKind.UpgradeNeeded, GatesDisplayPath, 1, 2));
    }

    [Fact]
    public async Task GetGatesAsync_OutdatedFileWithStopSetting_ThrowsUpgradeNeededException()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        _repository.WriteFile(StoreTestFormats.Config(1, "stop"), KitStorePaths.RootPath, KitStorePaths.ConfigFileName);
        var provider = CreateProvider();
        var store = CreateStore(FeatureStoreTestContext.CreateRegistryWithVersion2(FormatIds.Gates));

        // Act
        var act = () => provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<UpgradeNeededException>()).Which.FilePath
            .Should().Be(GatesDisplayPath);
    }

    [Theory]
    [InlineData($$"""{ {{TestGatesJson.Header}}, "profiles": null }""")]
    [InlineData($$"""{ {{TestGatesJson.Header}}, "profiles": { "feature": { "plan": { "checklist": "plan.md" } } } }""")]
    [InlineData("not json")]
    public async Task GetGatesAsync_InvalidFile_ThrowsInvalidKitFileException(string content)
    {
        // Arrange
        WriteGates(content);
        var provider = CreateProvider();

        // Act
        var act = () => provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<InvalidKitFileException>()).Which.FilePath
            .Should().Be(GatesDisplayPath);
    }

    [Fact]
    public async Task GetGatesAsync_FileNewerThanEngine_ThrowsFormatTooNewException()
    {
        // Arrange
        WriteGates("""{ "schemaVersion": 2, "minEngineVersion": "0.2.0", "profiles": {} }""");
        var provider = CreateProvider();

        // Act
        var act = () => provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        var exception = (await act.Should().ThrowAsync<FormatTooNewException>()).Which;

        exception.FilePath
            .Should().Be(GatesDisplayPath);

        exception.FileVersion
            .Should().Be(2);
    }

    [Fact]
    public async Task GetGatesAsync_DefaultFileAsInitWritesIt_ReturnsDefaultsWithoutWarning()
    {
        // Arrange
        WriteGates(new VersionedJsonSerializer(FormatRegistry.CreateDefault())
            .Serialize(GatesConfig.CreateDefault(), FormatIds.Gates));
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Gates
            .Should().BeEquivalentTo(DefaultGates(WorkflowProfileNames.Feature));

        result.Warnings
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetGatesAsync_EmptyProfileAndPhaseNames_ReturnsUnknownProfileAndUnknownPhaseWarnings()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "": { "plan": { } }, "feature": { "": { } } }""");
        var provider = CreateProvider();

        // Act
        var result = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        result.Warnings
            .Should().Equal(
                new GateEntryIgnoredWarning("", null, GateEntryIgnoredReason.UnknownProfile),
                new GateEntryIgnoredWarning("feature", "", GateEntryIgnoredReason.UnknownPhase));

        result.Gates
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetGatesAsync_SameStoreTwiceAndOtherProfile_ReadsFileOnceWithReadAccessAndReturnsSameResult()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "release": { }, "feature": { "plan": { } } }""");
        var provider = CreateProvider();
        var realStore = CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        WriteGatesWithProfiles("""{ "archive": { } }""");
        var second = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var otherProfile = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Bugfix, cancellationToken);

        // Assert
        second.Gates
            .Should().BeSameAs(first.Gates);

        second.Warnings
            .Should().BeSameAs(first.Warnings)
            .And.Equal(new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        otherProfile.Warnings
            .Should().BeSameAs(first.Warnings);

        // The shared read is not tied to the token of the call that starts it.
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates,
                FormatAccess.Read, CancellationToken.None))
            .MustHaveHappenedOnceExactly();

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_MissingFileWithSameStoreTwice_ReturnsSameDefaultsWarning()
    {
        // Arrange
        var provider = CreateProvider();
        var store = CreateStore();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        WriteGatesWithProfiles("{}");
        var second = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        second.Gates
            .Should().BeSameAs(first.Gates);

        second.Warnings
            .Should().BeSameAs(first.Warnings)
            .And.Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    public async Task GetGatesAsync_OtherStoreInstance_ReadsFileAgain()
    {
        // Arrange
        WriteGatesWithProfiles("""{ "release": { } }""");
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var first = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature, cancellationToken);
        WriteGatesWithProfiles("""{ "archive": { } }""");
        var second = await provider.GetGatesAsync(CreateStore(), BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        first.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("release", null, GateEntryIgnoredReason.UnknownProfile));

        second.Warnings
            .Should().Equal(new GateEntryIgnoredWarning("archive", null, GateEntryIgnoredReason.UnknownProfile));
    }

    [Fact]
    public async Task GetGatesAsync_ConcurrentCallsWithSameStore_ReadFileOnceAndShareResult()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        Task<WorkflowProfileGates>[] reads =
        [
            .. Enumerable.Range(0, 8).Select(_ => Task.Run(
                () => provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken),
                cancellationToken))
        ];

        await WaitUntilReadStartedAsync(store, cancellationToken);
        var config = new GatesConfig
        {
            Profiles = new Dictionary<string, Dictionary<string, GateConfig>>
            {
                [WorkflowProfileNames.Feature] = new() { [WorkflowPhaseNames.Plan] = new GateConfig() }
            }
        };
        pendingRead.SetResult(new VersionedReadResult<GatesConfig>(config, []));
        var results = await Task.WhenAll(reads);

        // Assert
        results
            .Should().AllSatisfy(result =>
            {
                result.Gates.Should().BeSameAs(results[0].Gates);
                result.Warnings.Should().BeSameAs(results[0].Warnings);
            });

        results[0].Gates.Keys
            .Should().Equal(WorkflowPhaseNames.Plan);

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_ReadFails_ThrowsAndNextCallReadsAgain()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var failure = new InvalidKitFileException(GatesDisplayPath, "The file is not valid JSON.");
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .ThrowsAsync(failure).Once()
            .Then.Returns(Task.FromResult<VersionedReadResult<GatesConfig>?>(null));
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var firstAct = () => provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var firstException = (await firstAct.Should().ThrowAsync<InvalidKitFileException>()).Which;
        var second = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var third = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        firstException
            .Should().BeSameAs(failure);

        second.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        third.Warnings
            .Should().BeSameAs(second.Warnings);

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_ConcurrentCallsWhileReadFails_ShareFailureAndLaterCallReadsAgain()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task).Once()
            .Then.Returns(Task.FromResult<VersionedReadResult<GatesConfig>?>(null));
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        var failure = new IOException("The disk is gone.");

        // Act
        var firstRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var secondRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        pendingRead.SetException(failure);
        var firstAct = () => firstRead;
        var secondAct = () => secondRead;
        var firstException = (await firstAct.Should().ThrowAsync<IOException>()).Which;
        var secondException = (await secondAct.Should().ThrowAsync<IOException>()).Which;
        var laterResult = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        firstException
            .Should().BeSameAs(failure);

        secondException
            .Should().BeSameAs(failure);

        laterResult.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_StoreThrowsSynchronously_ReturnsFaultedTaskAndNextCallReadsAgain()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var failure = new IOException("The disk is gone.");
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Throws(failure).Once()
            .Then.Returns(Task.FromResult<VersionedReadResult<GatesConfig>?>(null));
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var firstRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var firstAct = () => firstRead;
        var firstException = (await firstAct.Should().ThrowAsync<IOException>()).Which;
        var second = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        firstException
            .Should().BeSameAs(failure);

        second.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));
    }

    [Fact]
    public async Task GetGatesAsync_CanceledToken_ThrowsOperationCanceledExceptionWithoutReading()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var provider = CreateProvider();

        // Act
        var act = () => provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            new CancellationToken(canceled: true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(store)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GetGatesAsync_ReadCanceled_ThrowsOperationCanceledExceptionAndNextCallReadsAgain()
    {
        // Arrange
        WriteGatesWithProfiles("{}");
        var provider = CreateProvider();
        var realStore = CreateStore();
        var store = A.Fake<IKitStore>(options => options.Wrapping(realStore));
        using var cancellation = new CancellationTokenSource();
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Invokes(() => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token)).Once();

        // Act
        var firstAct = () => provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellation.Token);
        await firstAct.Should().ThrowAsync<OperationCanceledException>();
        var second = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        second.Gates
            .Should().BeEmpty();

        second.Warnings
            .Should().BeEmpty();

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_WaitingCallCanceled_ThrowsForThatCallAndKeepsSharedRead()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var waitCancellation = new CancellationTokenSource();

        // Act
        var startingRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        var waitingRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, waitCancellation.Token);
        await waitCancellation.CancelAsync();
        var waitingAct = () => waitingRead;
        await waitingAct.Should().ThrowAsync<OperationCanceledException>();
        pendingRead.SetResult(null);
        var started = await startingRead;
        var later = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        later.Gates
            .Should().BeSameAs(started.Gates);

        later.Warnings
            .Should().BeSameAs(started.Warnings);

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_StartingCallCanceled_ThrowsForThatCallAndJoiningCallGetsResult()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var provider = CreateProvider();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var startCancellation = new CancellationTokenSource();

        // Act
        var startingRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, startCancellation.Token);
        var joiningRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);
        await startCancellation.CancelAsync();
        var startingAct = () => startingRead;
        await startingAct.Should().ThrowAsync<OperationCanceledException>();
        pendingRead.SetResult(null);
        var joined = await joiningRead;
        var later = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, cancellationToken);

        // Assert
        joined.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        later.Warnings
            .Should().BeSameAs(joined.Warnings);

        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(KitStorePaths.GatesFileName, FormatIds.Gates,
                FormatAccess.Read, CancellationToken.None))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_StartingCallCanceledButReadCompletes_LaterCallReturnsCompletedResult()
    {
        // Arrange
        var store = A.Fake<IKitStore>();
        var pendingRead = new TaskCompletionSource<VersionedReadResult<GatesConfig>?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        A.CallTo(() => store.ReadJsonAsync<GatesConfig>(A<string>._, A<string>._, A<FormatAccess>._,
                A<CancellationToken>._))
            .Returns(pendingRead.Task);
        var provider = CreateProvider();
        using var startingCancellation = new CancellationTokenSource();

        // Act
        var startingRead = provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            startingCancellation.Token);
        await startingCancellation.CancelAsync();
        var startingAct = () => startingRead;
        await startingAct.Should().ThrowAsync<OperationCanceledException>();
        pendingRead.SetResult(null);
        var later = await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            TestContext.Current.CancellationToken);

        // Assert
        later.Warnings
            .Should().Equal(new GatesDefaultsUsedWarning(IsFileMissing: true));

        A.CallTo(store)
            .Where(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetGatesAsync_CachedResultWithCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var provider = CreateProvider();
        var store = CreateStore();
        await provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature, TestContext.Current.CancellationToken);

        // Act
        var act = () => provider.GetGatesAsync(store, BuiltInWorkflowProfiles.Feature,
            new CancellationToken(canceled: true));

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // The gates that the provider returns for a profile of the built-in defaults, built independently of the provider.
    private static Dictionary<string, TransitionGate> DefaultGates(string profileName)
    {
        return GatesConfig.CreateDefault().Profiles![profileName].ToDictionary(gate => gate.Key,
            gate => new TransitionGate(gate.Value.Approval, gate.Value.Checklist));
    }

    private static GatesConfigTransitionGateProvider CreateProvider()
    {
        return new GatesConfigTransitionGateProvider(new BuiltInWorkflowProfileCatalog());
    }

    private static WorkflowProfile GetProfile(string profileName)
    {
        return new BuiltInWorkflowProfileCatalog().GetProfile(profileName);
    }

    // Bounded on its own, so a read that never starts fails the test instead of hanging until the test token is
    // canceled.
    private static async Task WaitUntilReadStartedAsync(IKitStore store, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!Fake.GetCalls(store).Any(call => call.Method.Name == nameof(IKitStore.ReadJsonAsync)))
        {
            if (stopwatch.Elapsed > ReadStartTimeout)
            {
                throw new TimeoutException($"The read of gates.json did not start within {ReadStartTimeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }
    }

    private KitStore CreateStore(FormatRegistry? registry = null)
    {
        return TestKitStores.Create(_kitStorePath, formatRegistry: registry);
    }

    private void WriteGates(string content)
    {
        _repository.WriteFile(content, KitStorePaths.RootPath, KitStorePaths.GatesFileName);
    }

    private void WriteGatesWithProfiles(string profiles)
    {
        WriteGates(TestGatesJson.WithProfiles(profiles));
    }
}
