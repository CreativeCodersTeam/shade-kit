using AwesomeAssertions;
using CreativeCoders.ShadeKit.Core.Integrations;
using CreativeCoders.ShadeKit.Core.Integrations.Exceptions;

namespace CreativeCoders.ShadeKit.Core.Tests.Integrations;

public class MarketplaceSourceTests
{
    private static readonly string CurrentDirectory =
        Path.TrimEndingDirectorySeparator(Path.Combine(Path.GetTempPath(), "shade-kit-tests", "cwd"));

    [Theory]
    [InlineData("CreativeCodersTeam/shade-kit#v0.8.0", "CreativeCodersTeam/shade-kit", "v0.8.0")]
    [InlineData("owner/repo#main", "owner/repo", "main")]
    [InlineData("owner/repo#feature/login-sso", "owner/repo", "feature/login-sso")]
    [InlineData("my-org/my.repo_name#v1.0.0-beta.1", "my-org/my.repo_name", "v1.0.0-beta.1")]
    public void Parse_GitHubRepoWithRef_ReturnsGitHubSourceWithRef(string value, string expectedRepo,
        string expectedRef)
    {
        // Act
        var source = MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        source.Text
            .Should().Be(value);

        source.Repo
            .Should().Be(expectedRepo);

        source.Ref
            .Should().Be(expectedRef);

        source.Path
            .Should().BeNull();
    }

    [Fact]
    public void Parse_GitHubRepoWithoutRef_ReturnsGitHubSourceWithoutRef()
    {
        // Act
        var source = MarketplaceSource.Parse("owner/repo", CurrentDirectory);

        // Assert
        source
            .Should().Be(MarketplaceSource.ForGitHub("owner/repo"));

        source.Text
            .Should().Be("owner/repo");

        source.Ref
            .Should().BeNull();
    }

    [Fact]
    public void Parse_AbsolutePath_ReturnsDirectorySourceWithNormalizedPath()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "shade-kit-tests", "kit") + Path.DirectorySeparatorChar;
        var expectedPath = Path.TrimEndingDirectorySeparator(path);

        // Act
        var source = MarketplaceSource.Parse(path, CurrentDirectory);

        // Assert
        source.Text
            .Should().Be(expectedPath);

        source.Path
            .Should().Be(expectedPath);

        source.Repo
            .Should().BeNull();

        source.Ref
            .Should().BeNull();
    }

    [Theory]
    [InlineData("./kit")]
    [InlineData("kit")]
    [InlineData("./dir/kit")]
    [InlineData("dir/sub/kit")]
    [InlineData("../cwd/kit")]
    public void Parse_RelativePath_ReturnsDirectorySourceResolvedAgainstCurrentDirectory(string value)
    {
        // Arrange
        var expectedPath = Path.GetFullPath(value, CurrentDirectory);

        // Act
        var source = MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        source.Path
            .Should().Be(expectedPath);

        source.Text
            .Should().Be(expectedPath);
    }

    [Theory]
    [InlineData("~/kit")]
    [InlineData("dir\\kit")]
    [InlineData("owner\\repo")]
    [InlineData("./dir#x/kit")]
    [InlineData("./mirror/https://example.com/repo")]
    [InlineData("./git@host:repo")]
    [InlineData("dir/sub/git@host:repo")]
    public void Parse_ValueThatIsNoGitHubForm_ReturnsDirectorySource(string value)
    {
        // Act
        var source = MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        source.Path
            .Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(value, CurrentDirectory)));

        source.Repo
            .Should().BeNull();
    }

    [Theory]
    [InlineData("/kit")]
    [InlineData("/kit#x")]
    [InlineData("/srv/git@host:repo")]
    [InlineData("/mirror/https://example.com/repo")]
    public void Parse_AbsoluteUnixPath_ReturnsDirectorySource(string value)
    {
        // Arrange
        Assert.SkipWhen(OperatingSystem.IsWindows(), "On Windows a path that starts with '/' is not fully qualified.");

        // Act
        var source = MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        source.Path
            .Should().Be(Path.GetFullPath(value));

        source.Repo
            .Should().BeNull();
    }

    [Fact]
    public void Parse_AbsolutePathWithHash_KeepsHashInPath()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "a#b");

        // Act
        var source = MarketplaceSource.Parse(path, CurrentDirectory);

        // Assert
        source.Path
            .Should().Be(Path.GetFullPath(path));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("owner/.github")]
    public void Parse_ShortOrDotLeadingRepo_ReturnsGitHubSource(string value)
    {
        // Act
        var source = MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        source.Repo
            .Should().Be(value);
    }

    [Fact]
    public void Parse_NullValue_ThrowsArgumentNullException()
    {
        // Act
        var act = () => MarketplaceSource.Parse(null!, CurrentDirectory);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-x")]
    [InlineData("--scope")]
    [InlineData("owner/repo#")]
    [InlineData("owner/repo#with space")]
    [InlineData("owner/repo#a#b")]
    [InlineData("owner/")]
    [InlineData("-owner/repo")]
    [InlineData("owner/..")]
    [InlineData("owner/.")]
    [InlineData("owner-/repo")]
    [InlineData(" owner/repo")]
    public void Parse_InvalidValue_ThrowsInvalidMarketplaceSourceException(string value)
    {
        // Act
        var act = () => MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        act.Should().Throw<InvalidMarketplaceSourceException>()
            .Which.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("https://github.com/CreativeCodersTeam/shade-kit.git")]
    [InlineData("https://github.com/owner/repo#main")]
    [InlineData("http://example.com/owner/repo")]
    [InlineData("HTTPS://GITHUB.COM/OWNER/REPO")]
    [InlineData("ssh://git@github.com/owner/repo.git")]
    [InlineData("git://github.com/owner/repo")]
    [InlineData("git+ssh://git@github.com/owner/repo")]
    [InlineData("file:///Users/dev/shade-kit")]
    [InlineData("file://C:/work/shade-kit")]
    [InlineData("ab://x")]
    public void Parse_UrlWithScheme_ThrowsInvalidMarketplaceSourceExceptionNamingGitHubForm(string value)
    {
        // Act
        var act = () => MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        var exception = act.Should().Throw<InvalidMarketplaceSourceException>().Which;

        exception.Value
            .Should().Be(value);

        exception.Reason
            .Should().Be("It is a URL, and SHADE kit takes no URL as a marketplace source; a GitHub repository is " +
                         "written as owner/repo#ref or owner/repo.");
    }

    [Theory]
    [InlineData("git@github.com:owner/repo")]
    [InlineData("git@github.com:CreativeCodersTeam/shade-kit.git")]
    [InlineData("git@github.com:owner/repo#main")]
    [InlineData("user@host.example:path/to/repo")]
    [InlineData("github.com:owner/repo")]
    [InlineData("ab:c")]
    public void Parse_ScpLikeGitUrl_ThrowsInvalidMarketplaceSourceExceptionNamingGitHubForm(string value)
    {
        // Act
        var act = () => MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        var exception = act.Should().Throw<InvalidMarketplaceSourceException>().Which;

        exception.Value
            .Should().Be(value);

        exception.Reason
            .Should().Be("It is an scp-like git URL (user@host:path), and SHADE kit takes no URL as a marketplace " +
                         "source; a GitHub repository is written as owner/repo#ref or owner/repo.");
    }

    [Theory]
    [InlineData("C:\\work\\shade-kit")]
    [InlineData("C:/work/shade-kit")]
    [InlineData("c:\\work\\shade-kit")]
    [InlineData("C://work/shade-kit")]
    [InlineData("D:\\")]
    [InlineData("C:\\work\\git@host:x")]
    public void Parse_WindowsDrivePathOnWindows_ReturnsDirectorySource(string value)
    {
        // Arrange
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows reads a drive path as a fully qualified path.");

        // Act
        var source = MarketplaceSource.Parse(value, CurrentDirectory);

        // Assert
        source.Path
            .Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(value)));

        source.Repo
            .Should().BeNull();
    }

    [Theory]
    [InlineData("C:\\work\\shade-kit")]
    [InlineData("C:/work/shade-kit")]
    [InlineData("c:\\work\\shade-kit")]
    [InlineData("C://work/shade-kit")]
    [InlineData("D:\\")]
    public void Parse_WindowsDrivePath_IsNotRefusedAsUrl(string value)
    {
        // Act
        var exception = Record.Exception(() => MarketplaceSource.Parse(value, CurrentDirectory));

        // Assert: on Windows every value is a path; elsewhere C:/work/shade-kit looks like a GitHub repository and is
        // refused as one, but no value is refused as a URL.
        if (exception is InvalidMarketplaceSourceException invalidSource)
        {
            invalidSource.Reason
                .Should().NotContain("URL");
        }
        else
        {
            exception
                .Should().BeNull();
        }
    }

    [Theory]
    [InlineData("v0.8.0", "0.8.0")]
    [InlineData("v1.2.3-beta.1", "1.2.3-beta.1")]
    [InlineData("main", null)]
    [InlineData("0.8.0", null)]
    [InlineData("V0.8.0", null)]
    [InlineData("v0.8", null)]
    [InlineData("v", null)]
    [InlineData(null, null)]
    public void GetKitVersion_Ref_ReturnsSemVerOfVTagOrNull(string? @ref, string? expectedKitVersion)
    {
        // Act
        var kitVersion = MarketplaceSource.GetKitVersion(@ref);

        // Assert
        kitVersion
            .Should().Be(expectedKitVersion);
    }

    [Fact]
    public void KitVersion_GitHubSourceWithVTag_ReturnsVersionWithoutV()
    {
        // Act
        var source = MarketplaceSource.ForGitHub("CreativeCodersTeam/shade-kit", "v0.8.0");

        // Assert
        source.KitVersion
            .Should().Be("0.8.0");
    }

    [Fact]
    public void KitVersion_DirectorySource_ReturnsNull()
    {
        // Act
        var source = MarketplaceSource.ForDirectory(CurrentDirectory);

        // Assert
        source.KitVersion
            .Should().BeNull();
    }

    [Theory]
    [InlineData("repo")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner/repo#v1")]
    public void ForGitHub_InvalidRepo_ThrowsArgumentException(string value)
    {
        // Act
        var act = () => MarketplaceSource.ForGitHub(value);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("repo");
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("a#b")]
    public void ForGitHub_InvalidRef_ThrowsArgumentException(string value)
    {
        // Act
        var act = () => MarketplaceSource.ForGitHub("owner/repo", value);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("ref");
    }

    [Fact]
    public void ForDirectory_RelativePath_ThrowsArgumentException()
    {
        // Act
        var act = () => MarketplaceSource.ForDirectory("kit");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("path");
    }
}
