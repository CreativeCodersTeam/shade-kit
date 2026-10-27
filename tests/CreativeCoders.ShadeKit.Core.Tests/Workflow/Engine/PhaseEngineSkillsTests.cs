using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Engine;
using CreativeCoders.ShadeKit.Core.Workflow.Engine.Exceptions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using static CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine.PhaseEngineTestData;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Engine;

public class PhaseEngineSkillsTests
{
    [Theory]
    [Trait("Requirement", "FP04-AC10")]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Refine, "shade-kit-refine",
        new[] { "shade-kit-interview", "shade-kit-research" })]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Map, "shade-kit-map",
        new[] { "shade-kit-research" })]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Diagnose, "shade-kit-diagnose",
        new[] { "shade-kit-interview", "shade-kit-research" })]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Plan, "shade-kit-plan",
        new[] { "shade-kit-interview" })]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Quick, "shade-kit-quick",
        new[] { "shade-kit-interview", "shade-kit-review" })]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Finish, "shade-kit-finish",
        new[] { "shade-kit-report" })]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.New, "shade-kit-new", new string[0])]
    public void GetSkills_PhaseOfProfile_ReturnsPhaseSkillAndGeneralSkills(string profileName, string phase,
        string expectedPhaseSkill, string[] expectedGeneralSkills)
    {
        // Act
        var skills = new PhaseEngine().GetSkills(GetBuiltInProfile(profileName), phase);

        // Assert
        skills.PhaseSkill
            .Should().Be(expectedPhaseSkill);

        skills.GeneralSkills
            .Should().Equal(expectedGeneralSkills);
    }

    [Theory]
    [Trait("Requirement", "FP04-AC9")]
    [Trait("Requirement", "FP04-AC10")]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Refine)]
    [InlineData(WorkflowProfileNames.Bugfix, WorkflowPhaseNames.Map)]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Refine)]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Plan)]
    [InlineData(WorkflowProfileNames.Quick, WorkflowPhaseNames.Implement)]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Diagnose)]
    [InlineData(WorkflowProfileNames.Feature, WorkflowPhaseNames.Quick)]
    public void GetSkills_PhaseOfAnotherProfile_ThrowsPhaseNotInProfile(string profileName, string phase)
    {
        // Arrange
        var profile = GetBuiltInProfile(profileName);

        // Act
        var act = () => new PhaseEngine().GetSkills(profile, phase);

        // Assert
        act.Should().Throw<PhaseNotInProfileException>()
            .Which.Should().BeEquivalentTo(new
            {
                Phase = phase,
                Profile = profileName,
                ProfilePhases = profile.Phases.Select(x => x.Name).ToArray()
            });
    }

    [Fact]
    public void GetSkills_SyntheticProfile_ReturnsSkillsOfProfileDefinition()
    {
        // Act
        var skills = new PhaseEngine().GetSkills(Greek, "gamma");

        // Assert
        skills.Should().BeEquivalentTo(new PhaseSkills("skill-gamma", ["general-one", "general-two"]));
    }

    [Fact]
    public void GetSkills_NullArguments_ThrowArgumentNullException()
    {
        // Arrange
        var engine = new PhaseEngine();

        // Act
        var withoutProfile = () => engine.GetSkills(null!, "alpha");
        var withoutPhase = () => engine.GetSkills(Greek, null!);

        // Assert
        withoutProfile.Should().Throw<ArgumentNullException>();
        withoutPhase.Should().Throw<ArgumentNullException>();
    }
}
