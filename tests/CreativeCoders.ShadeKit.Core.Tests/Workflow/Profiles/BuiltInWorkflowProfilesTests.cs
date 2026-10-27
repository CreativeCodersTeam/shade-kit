using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Profiles;

public class BuiltInWorkflowProfilesTests
{
    public static TheoryData<string> AllProfileNames { get; } =
        [WorkflowProfileNames.Feature, WorkflowProfileNames.Bugfix, WorkflowProfileNames.Quick];

    [Fact]
    public void All_Contract_ListsFeatureBugfixAndQuickInOrder()
    {
        // Assert
        BuiltInWorkflowProfiles.All
            .Should().Equal(BuiltInWorkflowProfiles.Feature, BuiltInWorkflowProfiles.Bugfix,
                BuiltInWorkflowProfiles.Quick);

        BuiltInWorkflowProfiles.All.Select(x => x.Name)
            .Should().Equal("feature", "bugfix", "quick");
    }

    [Fact]
    public void Feature_Contract_HasSevenPhasesWithTheirSkills()
    {
        // Assert
        Describe(BuiltInWorkflowProfiles.Feature)
            .Should().Equal(
                "new: shade-kit-new []",
                "refine: shade-kit-refine [shade-kit-interview, shade-kit-research]",
                "map: shade-kit-map [shade-kit-research]",
                "plan: shade-kit-plan [shade-kit-interview]",
                "implement: shade-kit-implement [shade-kit-review]",
                "verify: shade-kit-verify []",
                "finish: shade-kit-finish [shade-kit-report]");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC9")]
    public void Bugfix_Contract_HasDiagnoseAndNoRefineOrMap()
    {
        // Assert
        Describe(BuiltInWorkflowProfiles.Bugfix)
            .Should().Equal(
                "new: shade-kit-new []",
                "diagnose: shade-kit-diagnose [shade-kit-interview, shade-kit-research]",
                "plan: shade-kit-plan [shade-kit-interview]",
                "implement: shade-kit-implement [shade-kit-review]",
                "verify: shade-kit-verify []",
                "finish: shade-kit-finish [shade-kit-report]");

        BuiltInWorkflowProfiles.Bugfix.Phases.Select(x => x.Name)
            .Should().Contain(WorkflowPhaseNames.Diagnose)
            .And.NotContain(WorkflowPhaseNames.Refine)
            .And.NotContain(WorkflowPhaseNames.Map);
    }

    [Fact]
    [Trait("Requirement", "FP04-AC9")]
    public void Quick_Contract_HasOnlyNewQuickAndFinish()
    {
        // Assert
        Describe(BuiltInWorkflowProfiles.Quick)
            .Should().Equal(
                "new: shade-kit-new []",
                "quick: shade-kit-quick [shade-kit-interview, shade-kit-review]",
                "finish: shade-kit-finish [shade-kit-report]");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC10")]
    public void PhaseSkills_ProfilesWithDifferentPhases_DifferPerProfile()
    {
        // Act
        var featureSkills = BuiltInWorkflowProfiles.Feature.Phases.Select(x => x.PhaseSkill).ToArray();
        var bugfixSkills = BuiltInWorkflowProfiles.Bugfix.Phases.Select(x => x.PhaseSkill).ToArray();
        var quickSkills = BuiltInWorkflowProfiles.Quick.Phases.Select(x => x.PhaseSkill).ToArray();

        // Assert
        featureSkills
            .Should().Contain(["shade-kit-refine", "shade-kit-map"])
            .And.NotContain(["shade-kit-diagnose", "shade-kit-quick"]);

        bugfixSkills
            .Should().Contain("shade-kit-diagnose")
            .And.NotContain(["shade-kit-refine", "shade-kit-map", "shade-kit-quick"]);

        quickSkills
            .Should().Equal("shade-kit-new", "shade-kit-quick", "shade-kit-finish");
    }

    [Fact]
    [Trait("Requirement", "FP04-AC10")]
    public void GeneralSkills_SecondPhaseOfEachProfile_DifferPerProfileWhereThePhasesDiffer()
    {
        // Act
        var feature = BuiltInWorkflowProfiles.Feature.Phases[1];
        var bugfix = BuiltInWorkflowProfiles.Bugfix.Phases[1];
        var quick = BuiltInWorkflowProfiles.Quick.Phases[1];

        // Assert
        (feature.Name, feature.PhaseSkill)
            .Should().Be((WorkflowPhaseNames.Refine, "shade-kit-refine"));

        (bugfix.Name, bugfix.PhaseSkill)
            .Should().Be((WorkflowPhaseNames.Diagnose, "shade-kit-diagnose"));

        quick.GeneralSkills
            .Should().Equal(WorkflowSkillNames.Interview, WorkflowSkillNames.Review)
            .And.NotEqual(feature.GeneralSkills);
    }

    [Theory]
    [MemberData(nameof(AllProfileNames))]
    public void Phases_EveryBuiltInProfile_StartWithNewThatCannotBeReopened(string profileName)
    {
        // Arrange
        var profile = BuiltInWorkflowProfiles.All.Single(x => x.Name == profileName);

        // Act
        var firstPhase = profile.Phases[0];

        // Assert
        firstPhase.Name
            .Should().Be(WorkflowPhaseNames.New);

        firstPhase.CanReopen
            .Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(AllProfileNames))]
    public void Phases_EveryBuiltInProfile_EndWithFinishAndEveryPhaseButNewCanBeReopened(string profileName)
    {
        // Arrange
        var profile = BuiltInWorkflowProfiles.All.Single(x => x.Name == profileName);

        // Assert
        profile.Phases[^1].Name
            .Should().Be(WorkflowPhaseNames.Finish);

        profile.Phases.Skip(1)
            .Should().OnlyContain(x => x.CanReopen);
    }

    [Theory]
    [MemberData(nameof(AllProfileNames))]
    public void Phases_EveryBuiltInProfile_RunThroughSkillNamedAfterPhaseAndNeverLoadGateCheck(string profileName)
    {
        // Arrange
        var profile = BuiltInWorkflowProfiles.All.Single(x => x.Name == profileName);

        // Assert
        profile.Phases
            .Should().OnlyContain(x => x.PhaseSkill == WorkflowSkillNames.ForPhase(x.Name)
                                       && !x.GeneralSkills.Contains(WorkflowSkillNames.GateCheck));
    }

    private static string[] Describe(WorkflowProfile profile)
    {
        return
        [
            .. profile.Phases.Select(x => $"{x.Name}: {x.PhaseSkill} [{string.Join(", ", x.GeneralSkills)}]")
        ];
    }
}
