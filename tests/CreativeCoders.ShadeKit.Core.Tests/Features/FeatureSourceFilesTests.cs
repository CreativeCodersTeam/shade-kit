using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Features;
using CreativeCoders.ShadeKit.Tests.Shared;

namespace CreativeCoders.ShadeKit.Core.Tests.Features;

public sealed class FeatureSourceFilesTests : IDisposable
{
    private readonly TempKitDirectory _directory = new TempKitDirectory();

    public void Dispose()
    {
        _directory.Dispose();
    }

    [Fact]
    public void FindFilePath_ExistingFileRelativeToWorkingDirectory_ReturnsFullPath()
    {
        // Arrange
        var filePath = _directory.WriteFile("# Login SSO\n", "docs", "login.md");

        // Act
        var result = FeatureSourceFiles.FindFilePath(Path.Combine("docs", "login.md"), _directory.RootPath);

        // Assert
        result
            .Should().Be(Path.GetFullPath(filePath));
    }

    [Fact]
    public void FindFilePath_NormalizablePath_ReturnsFullPathWithoutDotSegments()
    {
        // Arrange
        var filePath = _directory.WriteFile("# Login SSO\n", "docs", "login.md");

        // Act
        var result = FeatureSourceFiles.FindFilePath(Path.Combine("docs", "..", "docs", "login.md"),
            _directory.RootPath);

        // Assert
        result
            .Should().Be(Path.GetFullPath(filePath));
    }

    [Theory]
    [InlineData("missing.md")]
    [InlineData("docs")]
    [InlineData("Login SSO via Azure")]
    [InlineData("https://example.com/requests/login-sso")]
    public void FindFilePath_SourceThatNamesNoFile_ReturnsNull(string source)
    {
        // Arrange
        _directory.WriteFile("# Login SSO\n", "docs", "login.md");

        // Act
        var result = FeatureSourceFiles.FindFilePath(source, _directory.RootPath);

        // Assert
        result
            .Should().BeNull();
    }

    [Fact]
    public void FindFilePath_SourceWithNullCharacter_ReturnsNullInsteadOfThrowing()
    {
        // Act
        var result = FeatureSourceFiles.FindFilePath("login\0.md", _directory.RootPath);

        // Assert
        result
            .Should().BeNull();
    }
}
