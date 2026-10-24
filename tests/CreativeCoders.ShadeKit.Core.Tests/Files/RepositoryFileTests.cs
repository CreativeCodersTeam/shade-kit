using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Files;

namespace CreativeCoders.ShadeKit.Core.Tests.Files;

public class RepositoryFileTests
{
    private const string RepositoryRootPath = "/repo";

    private static readonly string FullRepositoryRootPath = Path.GetFullPath(RepositoryRootPath);

    [Theory]
    [InlineData("docs/readme.md")]
    [InlineData("./docs/readme.md")]
    [InlineData("docs/../docs/readme.md")]
    [InlineData("docs//readme.md")]
    public void Resolve_RelativePath_ReturnsNormalizedPaths(string relativePath)
    {
        // Act
        var file = RepositoryFile.Resolve(RepositoryRootPath, relativePath);

        // Assert
        file.RepositoryRootPath
            .Should().Be(FullRepositoryRootPath);

        file.FullPath
            .Should().Be(Path.Combine(FullRepositoryRootPath, "docs", "readme.md"));

        file.RelativePath
            .Should().Be(Path.Combine("docs", "readme.md"));

        file.GitPath
            .Should().Be("docs/readme.md");
    }

    [Fact]
    public void Resolve_RelativePathWithOperatingSystemSeparator_ReturnsGitPathWithForwardSlashes()
    {
        // Act
        var file = RepositoryFile.Resolve(RepositoryRootPath, Path.Combine("docs", "sub", "readme.md"));

        // Assert
        file.GitPath
            .Should().Be("docs/sub/readme.md");
    }

    [Theory]
    [InlineData("/repo/")]
    [InlineData("/repo/.")]
    [InlineData("/repo/../repo")]
    public void Resolve_OtherSpellingOfRoot_EqualsFileOfNormalizedRoot(string repositoryRootPath)
    {
        // Act
        var file = RepositoryFile.Resolve(repositoryRootPath, "readme.md");

        // Assert
        file
            .Should().Be(RepositoryFile.Resolve(RepositoryRootPath, "readme.md"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/absolute/notes.txt")]
    [InlineData("../notes.txt")]
    [InlineData("docs/../../notes.txt")]
    [InlineData(".")]
    public void Resolve_InvalidRelativePath_ThrowsArgumentExceptionNamingCallersParameter(string lineEndingPath)
    {
        // Act
        var act = () => RepositoryFile.Resolve(RepositoryRootPath, lineEndingPath);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(lineEndingPath));
    }

    [Fact]
    public void Resolve_NullRelativePath_ThrowsArgumentException()
    {
        // Act
        var act = () => RepositoryFile.Resolve(RepositoryRootPath, null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_InvalidRepositoryRootPath_ThrowsArgumentException(string? repositoryRootPath)
    {
        // Act
        var act = () => RepositoryFile.Resolve(repositoryRootPath!, "readme.md");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(repositoryRootPath));
    }

    [Fact]
    public void FromCheckedFullPath_FullPathBelowRoot_ReturnsPathRelativeToRoot()
    {
        // Arrange
        var fullPath = Path.Combine(FullRepositoryRootPath, ".shadekit", "config.json");

        // Act
        var file = RepositoryFile.FromCheckedFullPath(RepositoryRootPath + "/", fullPath);

        // Assert
        file
            .Should().Be(RepositoryFile.Resolve(RepositoryRootPath, ".shadekit/config.json"));
    }

    [Fact]
    public void ToString_File_ReturnsGitPath()
    {
        // Act
        var text = RepositoryFile.Resolve(RepositoryRootPath, Path.Combine("docs", "readme.md")).ToString();

        // Assert
        text
            .Should().Be("docs/readme.md");
    }
}
