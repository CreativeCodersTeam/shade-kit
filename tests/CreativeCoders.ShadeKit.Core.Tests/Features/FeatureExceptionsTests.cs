using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Core.Git;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public class FeatureExceptionsTests
{
    [Fact]
    public void InvalidFeatureIdException_Id_ExposesIdAndNamesItInMessage()
    {
        // Act
        var exception = new InvalidFeatureIdException("../GH-142");

        // Assert
        exception.FeatureId
            .Should().Be("../GH-142");

        exception.Message
            .Should().Be("'../GH-142' is not a valid feature id.");
    }

    [Fact]
    public void InvalidFeatureSourceException_SourceAndReason_ExposesBothAndNamesThemInMessage()
    {
        // Act
        var exception = new InvalidFeatureSourceException("https://github.com/acme/shop", "No issue number.");

        // Assert
        exception.SourceText
            .Should().Be("https://github.com/acme/shop");

        exception.Reason
            .Should().Be("No issue number.");

        exception.Message
            .Should().Be("The feature source 'https://github.com/acme/shop' cannot be used: No issue number.");
    }

    [Fact]
    public void InvalidFeatureSourceException_InnerException_KeepsInnerExceptionAndMessage()
    {
        // Arrange
        var innerException = new UnauthorizedAccessException("Access denied.");

        // Act
        var exception = new InvalidFeatureSourceException("request.md", "The file cannot be read.", innerException);

        // Assert
        exception.InnerException
            .Should().BeSameAs(innerException);

        exception.Message
            .Should().Be("The feature source 'request.md' cannot be used: The file cannot be read.");
    }

    [Fact]
    public void InvalidFeatureSourceException_WithoutInnerException_HasNoInnerException()
    {
        // Act
        var exception = new InvalidFeatureSourceException("request.md", "The file cannot be read.");

        // Assert
        exception.InnerException
            .Should().BeNull();
    }

    [Fact]
    public void InvalidFeatureTitleException_Reason_ExposesReasonAndNamesItInMessage()
    {
        // Act
        var exception = new InvalidFeatureTitleException("Pass the title with --title.");

        // Assert
        exception.Reason
            .Should().Be("Pass the title with --title.");

        exception.Message
            .Should().Be("The feature title cannot be used: Pass the title with --title.");
    }

    [Fact]
    public void UnknownProfileException_Profile_ExposesProfileAndListsKnownProfilesInMessage()
    {
        // Act
        var exception = new UnknownProfileException("hotfix");

        // Assert
        exception.Profile
            .Should().Be("hotfix");

        exception.Message
            .Should().Be("The workflow profile 'hotfix' is unknown. Known profiles: feature, bugfix, quick.");
    }

    [Fact]
    public void FeatureAlreadyExistsException_Id_ExposesIdAndNamesFolderInMessage()
    {
        // Act
        var exception = new FeatureAlreadyExistsException("gh-142-login-sso");

        // Assert
        exception.FeatureId
            .Should().Be("gh-142-login-sso");

        exception.Message
            .Should().Be("The feature 'gh-142-login-sso' already exists: its folder " +
                         "'.shadekit/features/gh-142-login-sso/' exists.");
    }

    [Fact]
    public void FeatureNotFoundException_Id_ExposesIdAndNamesFolderInMessage()
    {
        // Act
        var exception = new FeatureNotFoundException("gh-7-checkout");

        // Assert
        exception.FeatureId
            .Should().Be("gh-7-checkout");

        exception.Message
            .Should().Be("The feature 'gh-7-checkout' does not exist: there is no folder " +
                         "'.shadekit/features/gh-7-checkout/'.");
    }

    [Fact]
    public void FeatureIoFailedException_IdOperationAndCause_ExposesThemAndNamesFolderInMessage()
    {
        // Arrange
        var cause = new IOException("Access to the path is denied.");

        // Act
        var exception = new FeatureIoFailedException("gh-7-checkout", FeatureIoFailedException.DropOperation, cause);

        // Assert
        exception.FeatureId
            .Should().Be("gh-7-checkout");

        exception.FolderPath
            .Should().Be(".shadekit/features/gh-7-checkout/");

        exception.Operation
            .Should().Be("drop");

        exception.InnerException
            .Should().BeSameAs(cause);

        exception.Message
            .Should().Be("Could not drop the feature 'gh-7-checkout', because its folder " +
                         "'.shadekit/features/gh-7-checkout/' cannot be changed: Access to the path is denied.");
    }

    [Fact]
    public void FeatureIoFailedException_NullInnerException_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FeatureIoFailedException("gh-7-checkout", FeatureIoFailedException.CreateOperation,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(typeof(InvalidFeatureIdException))]
    [InlineData(typeof(FeatureIoFailedException))]
    [InlineData(typeof(FeatureAlreadyExistsException))]
    [InlineData(typeof(FeatureNotFoundException))]
    [InlineData(typeof(InvalidFeatureSourceException))]
    [InlineData(typeof(InvalidFeatureTitleException))]
    [InlineData(typeof(UnknownProfileException))]
    [InlineData(typeof(GitUnavailableException))]
    [InlineData(typeof(GitIdentityNotConfiguredException))]
    public void ExceptionType_CoreConvention_IsNotSealed(Type exceptionType)
    {
        // Assert
        exceptionType.IsSealed
            .Should().BeFalse();
    }
}
