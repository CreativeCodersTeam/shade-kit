using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles;
using CreativeCoders.ShadeKit.Core.Workflow.Profiles.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Workflow.Profiles;

public class BuiltInWorkflowProfileCatalogTests
{
    [Fact]
    public void Profiles_DefaultCatalog_ListsBuiltInProfilesInOrder()
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog();

        // Act
        var profiles = catalog.Profiles;

        // Assert
        profiles
            .Should().Equal(BuiltInWorkflowProfiles.All);
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature)]
    [InlineData(WorkflowProfileNames.Bugfix)]
    [InlineData(WorkflowProfileNames.Quick)]
    public void TryGetProfile_KnownProfile_ReturnsTrueAndProfile(string name)
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog();

        // Act
        var found = catalog.TryGetProfile(name, out var profile);

        // Assert
        found
            .Should().BeTrue();

        profile
            .Should().BeSameAs(BuiltInWorkflowProfiles.All.Single(x => x.Name == name));
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
    public void TryGetProfile_UnknownProfile_ReturnsFalseAndNull(string? name)
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog();

        // Act
        var found = catalog.TryGetProfile(name, out var profile);

        // Assert
        found
            .Should().BeFalse();

        profile
            .Should().BeNull();
    }

    [Theory]
    [InlineData(WorkflowProfileNames.Feature)]
    [InlineData(WorkflowProfileNames.Bugfix)]
    [InlineData(WorkflowProfileNames.Quick)]
    public void GetProfile_KnownProfile_ReturnsProfile(string name)
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog();

        // Act
        var profile = catalog.GetProfile(name);

        // Assert
        profile.Name
            .Should().Be(name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Feature")]
    [InlineData("feature ")]
    [InlineData("hotfix")]
    public void GetProfile_UnknownProfile_ThrowsUnknownProfileExceptionListingKnownProfiles(string name)
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog();

        // Act
        var act = () => catalog.GetProfile(name);

        // Assert
        var exception = act.Should().Throw<UnknownProfileException>().Which;

        exception.Profile
            .Should().Be(name);

        exception.KnownProfiles
            .Should().Equal("feature", "bugfix", "quick");
    }

    [Fact]
    public void GetProfile_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog();

        // Act
        var act = () => catalog.GetProfile(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetProfile_CustomProfiles_ListsTheirNamesAsKnownProfiles()
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog([CreateProfile("alpha"), CreateProfile("beta")]);

        // Act
        var act = () => catalog.GetProfile("feature");

        // Assert
        act.Should().Throw<UnknownProfileException>()
            .Which.KnownProfiles
            .Should().Equal("alpha", "beta");
    }

    [Fact]
    public void GetProfile_CustomProfiles_ReturnsSameInstance()
    {
        // Arrange
        var beta = CreateProfile("beta");
        var catalog = new BuiltInWorkflowProfileCatalog([CreateProfile("alpha"), beta]);

        // Act
        var profile = catalog.GetProfile("beta");

        // Assert
        profile
            .Should().BeSameAs(beta);
    }

    [Fact]
    public void Constructor_SourceListChangedAfterwards_KeepsProfilesAndKnownNames()
    {
        // Arrange
        var profiles = new List<WorkflowProfile> { CreateProfile("alpha") };
        var catalog = new BuiltInWorkflowProfileCatalog(profiles);

        // Act
        profiles.Add(CreateProfile("beta"));

        // Assert
        catalog.Profiles.Select(x => x.Name)
            .Should().Equal("alpha");

        catalog.TryGetProfile("beta", out _)
            .Should().BeFalse();

        catalog.Invoking(x => x.GetProfile("beta"))
            .Should().Throw<UnknownProfileException>()
            .Which.KnownProfiles
            .Should().Equal("alpha");
    }

    [Fact]
    public void Constructor_ProfilesNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new BuiltInWorkflowProfileCatalog(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ProfilesContainNull_ThrowsArgumentException()
    {
        // Act
        var act = () => new BuiltInWorkflowProfileCatalog([CreateProfile("alpha"), null!]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("profiles");
    }

    [Fact]
    public void Constructor_DuplicateProfileName_ThrowsArgumentExceptionNamingProfile()
    {
        // Act
        var act = () => new BuiltInWorkflowProfileCatalog(
            [CreateProfile("alpha"), CreateProfile("beta"), CreateProfile("alpha")]);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("profiles")
            .WithMessage("*'alpha'*");
    }

    [Fact]
    public void Constructor_NoProfiles_CreatesEmptyCatalogThatKnowsNoProfile()
    {
        // Arrange
        var catalog = new BuiltInWorkflowProfileCatalog([]);

        // Act
        var act = () => catalog.GetProfile("feature");

        // Assert
        catalog.Profiles
            .Should().BeEmpty();

        act.Should().Throw<UnknownProfileException>()
            .Which.KnownProfiles
            .Should().BeEmpty();
    }

    private static WorkflowProfile CreateProfile(string name)
    {
        return new WorkflowProfile(name, [new WorkflowPhase("alpha", "skill-alpha", [])]);
    }
}
