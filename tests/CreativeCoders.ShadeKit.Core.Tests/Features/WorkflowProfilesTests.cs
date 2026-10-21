using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class WorkflowProfilesTests
{
    [Fact]
    public void All_Contract_ListsFeatureBugfixAndQuickInOrder()
    {
        // Assert
        WorkflowProfiles.All
            .Should().Equal("feature", "bugfix", "quick");
    }

    [Theory]
    [InlineData(WorkflowProfiles.Feature)]
    [InlineData(WorkflowProfiles.Bugfix)]
    [InlineData(WorkflowProfiles.Quick)]
    public void IsKnown_KnownProfile_ReturnsTrue(string profile)
    {
        // Act
        var isKnown = WorkflowProfiles.IsKnown(profile);

        // Assert
        isKnown
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Feature")]
    [InlineData("QUICK")]
    [InlineData(" feature")]
    [InlineData("feature ")]
    [InlineData("hotfix")]
    public void IsKnown_UnknownProfile_ReturnsFalse(string? profile)
    {
        // Act
        var isKnown = WorkflowProfiles.IsKnown(profile);

        // Assert
        isKnown
            .Should().BeFalse();
    }
}
