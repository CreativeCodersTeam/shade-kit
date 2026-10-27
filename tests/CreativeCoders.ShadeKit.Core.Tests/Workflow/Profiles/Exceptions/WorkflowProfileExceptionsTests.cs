using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Profiles.Exceptions;

public class WorkflowProfileExceptionsTests
{
    [Fact]
    public void UnknownProfileException_ProfileAndKnownProfiles_ExposesBothAndListsKnownProfilesInMessage()
    {
        // Act
        var exception = new UnknownProfileException("hotfix", ["feature", "bugfix", "quick"]);

        // Assert
        exception.Profile
            .Should().Be("hotfix");

        exception.KnownProfiles
            .Should().Equal("feature", "bugfix", "quick");

        exception.Message
            .Should().Be("The workflow profile 'hotfix' is unknown. Known profiles: feature, bugfix, quick.");
    }

    [Fact]
    public void UnknownProfileException_WithoutFeatureFilePath_HasNoFeatureFilePath()
    {
        // Act
        var exception = new UnknownProfileException("hotfix", ["feature"]);

        // Assert
        exception.FeatureFilePath
            .Should().BeNull();
    }

    [Fact]
    public void UnknownProfileException_FeatureFilePath_ExposesPathAndNamesItInMessage()
    {
        // Act
        var exception = new UnknownProfileException("hotfix", ["feature", "quick"],
            ".shadekit/features/gh-142-login-sso/feature.json");

        // Assert
        exception.FeatureFilePath
            .Should().Be(".shadekit/features/gh-142-login-sso/feature.json");

        exception.Message
            .Should().Be("The workflow profile 'hotfix' in '.shadekit/features/gh-142-login-sso/feature.json' is " +
                         "unknown. Known profiles: feature, quick.");
    }

    [Fact]
    public void UnknownProfileException_NoKnownProfiles_SaysSoInMessage()
    {
        // Act
        var exception = new UnknownProfileException("hotfix", []);

        // Assert
        exception.KnownProfiles
            .Should().BeEmpty();

        exception.Message
            .Should().Be("The workflow profile 'hotfix' is unknown. No profiles are known.");
    }

    [Fact]
    public void UnknownProfileException_KnownProfiles_KeepsCopy()
    {
        // Arrange
        var knownProfiles = new List<string> { "feature" };

        // Act
        var exception = new UnknownProfileException("hotfix", knownProfiles);
        knownProfiles.Add("bugfix");

        // Assert
        exception.KnownProfiles
            .Should().Equal("feature");
    }

    [Fact]
    public void UnknownProfileException_KnownProfilesNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new UnknownProfileException("hotfix", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UnknownProfileException_CoreConvention_IsNotSealed()
    {
        // Assert
        typeof(UnknownProfileException).IsSealed
            .Should().BeFalse();
    }
}
