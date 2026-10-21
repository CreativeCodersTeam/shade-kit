using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Git;

namespace CreativeCoders.ShadeKit.Core.Tests.Git;

public class GitIdentityNotConfiguredExceptionTests
{
    [Fact]
    public void Constructor_OneMissingKey_MessageNamesRepositoryAndKey()
    {
        // Act
        var exception = new GitIdentityNotConfiguredException("/repo", ["user.email"]);

        // Assert
        exception.Message
            .Should().Be("The git identity for the repository '/repo' is not configured: user.email is not set. " +
                         "Set each missing key with 'git config <key> <value>'.");

        exception.RepositoryRootPath
            .Should().Be("/repo");

        exception.MissingKeys
            .Should().Equal("user.email");
    }

    [Fact]
    public void Constructor_TwoMissingKeys_MessageNamesBothKeys()
    {
        // Act
        var exception = new GitIdentityNotConfiguredException("/repo", ["user.name", "user.email"]);

        // Assert
        exception.Message
            .Should().Be("The git identity for the repository '/repo' is not configured: user.name and user.email " +
                         "are not set. Set each missing key with 'git config <key> <value>'.");

        exception.MissingKeys
            .Should().Equal("user.name", "user.email");
    }

    [Fact]
    public void Constructor_MissingKeysChangedAfterwards_KeepsCopyOfMissingKeys()
    {
        // Arrange
        var missingKeys = new List<string> { "user.name" };

        // Act
        var exception = new GitIdentityNotConfiguredException("/repo", missingKeys);
        missingKeys.Add("user.email");

        // Assert
        exception.MissingKeys
            .Should().Equal("user.name");
    }

    [Fact]
    public void Constructor_NullMissingKeys_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GitIdentityNotConfiguredException("/repo", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
